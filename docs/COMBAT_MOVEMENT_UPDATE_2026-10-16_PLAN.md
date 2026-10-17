# Combat and movement update: Fighter Special timing, perfect block, tumble, short hop, ledge get-ups, device follow and the Story feel fixes (2026-10-16)

**Status:** planned 2026-10-16, revised the same day against the plan critique. Base commit: `e2c655a` (the 2026-10-12 gameplay-update closeout) plus this plan's commit. Baseline **3228** passing tests. I counted the `[TestCase]` attributes under `tests/` again at `e2c655a` and got 3228.

**Pre-flight (orchestrator, before the plan commit).** The main checkout has uncommitted work that is not part of this package:
- `scenes/menus/MainMenu.tscn`
- `resources/UI/ftt_theme.tres`
- `resources/SpriteFrames/Bosses/apex_eraser_frames.tres` and `archive_prime_frames.tres`
- eight Tubman `*.png.import` sidecars, which are line-ending churn (see CLAUDE.md)

The user decides whether to commit, stash or leave them. Either way:
- no worktree branches from a commit that contains them unless the user says so;
- both bot batches run in a dedicated clean worktree (§4.3), so the user's edits never reach either one.

**Questions.** §0.1 lists eight questions where a decision's text conflicted with itself, with the code, or with another decision. **All eight were ruled on 2026-10-16 (§0.2)**; Q1, Q5, Q6 and Q7 went against the drafted default, and the dossiers were edited to match. The closeout records each ruling as a ledger row.

---

## 0. Sources and authority

**Decisions.** `D:/Projects/genre_review_2026-10-13/DECISIONS_combat_movement.md`, rounds 1–10 and the follow-ups 11.1–11.3 (2026-10-15/16). This plan cites decision IDs as they appear there (1.1(1) … 11.3). Where a decision gives a number, this plan uses it. Every other number is **Provisional**: an agent's choice under the user's ruling, which the closeout records as a `PROVISIONAL-*` ledger row so the user can reverse it.

**Source reports.** `REPORT_1_PLATFORMERS.md` and `REPORT_2_FIGHTERS.md`, in the same folder. Finding IDs such as PFC-08, FGS-03, KIT-02, CMB-01, MOV-13 and TWO-04 point there.

**Recon and review.** The orchestrator holds the six area dossiers this plan was written from (2026-10-16): Fighter Special timing; Ultimate and input; defence; movement; Story feel and plumbing; sim state budget and tests. It also holds the critique this revision answers. I spot-checked every file and member reference below against `e2c655a`. Before you start, re-read the code you will change, because line numbers drift.

**Authority.** `design-godot.md` and the adopted contracts define intent (the AGENTS.md authority rule). This package changes intent in the places listed below. Workstreams do not edit the design, the contracts or the ledger. The closeout writes them (§1.1).

### Rulings this package amends or reopens

Each one gets a closeout ledger amendment.

| Ruling / row | Amended by | How |
|---|---|---|
| `USER-COMBAT-FLOW-NOT-NOW` (2026-10-11: "closing `DEFER-SIM-SPECIAL-PHASES`" deferred) | 1.2 | Reopened. Sim Specials get their authored timeline. |
| `DEFER-SIM-SPECIAL-PHASES`, `SIM-SPECIAL-POSE-THIRDS` | 1.2 | Closed. |
| `VERIFY-TUNING-SPECIAL-TOTAL-FOLD` | 1.2 | Amended. The generic melee branch applies the folded total once per execution. Only single-hit Splitting Strike reaches that branch in the production roster (2.1.6). |
| `USER-SPECIAL-RECOVERY-CANCEL` / `PROVISIONAL-SPECIAL-RECOVERY-CANCEL-RULES` (D3, Story only) | 11.1 | D3 becomes one rule in both modes. |
| `USER-HIT-CONFIRM-CANCEL` (its "Fighter consequence" sentence), `PROVISIONAL-ABILITY-LOCKOUT-RULES` (1) and (6), `PROVISIONAL-HIT-CONFIRM-RULES` (6) and (7) | 1.2, 9.3 | Sim Specials no longer fire on press. The out-of-reach melee flash goes. The CPU reads its own cast. |
| `PROVISIONAL-MOVEMENT-FEEL-RULES` (5) (aerial Specials) | 1.2 | Sim aerial Specials gain Story's half gravity, through one shared constant. |
| `PROVISIONAL-MOVEMENT-FEEL-RULES` (9) (CPU recovery-jump delivery) | 1.1(5) | Recovery jumps become held full hops. A queued second jump can arrive one tick later because of the release gap. |
| `PROVISIONAL-MOVEMENT-FEEL-RULES` (3) (the apex hang "once the fighter has jumped"), `USER-MOVEMENT-FEEL` | 6.1 | The hang applies only while Jump is held, in both modes. |
| `VERIFY-SAME-TICK-VERB-PARITY` (1), (5) | 2.3, 1.1(3), 1.1(2) | Closed. |
| D04 (`DEFENSIVE_EFFECTS.md` :238; `design-godot.md` :1323, "attacking does not cancel it") | 4.3 | Defy protection ends on the first attack or Special, in both modes. |
| D01/D02b (`DEFENSIVE_EFFECTS.md`, the layer order) | 3.4 | Perfect block becomes a variant of the ordinary block layer. The order is unchanged. |
| D03c (`DEFENSIVE_EFFECTS.md`, blockable hazard ticks, Fighter) | 7.4 | Extended to Story with the ruled source list. |
| A01 (`design-godot.md` :1432, "ordinary 8-frame shieldstun"), F09 shieldstun (:1434 "8 frames"; `COMBAT_VALIDATION.md` :5) | 4.1 / 11.2 | Shieldstun scales with damage, in both modes. |
| The V7.3 block-model closure (audit H-5: every non-shatter block spends a charge and sets shieldstun) | 3.4, 4.1 | A perfect block spends no charge and sets no shieldstun. An ordinary block's shieldstun scales. |
| F15 / A01 Shield-Breaker text (`design-godot.md` :1432 and the F15 rows) | 1.3, 3.4 | Fighter Shield-Breakers gain their startup. A perfect block does not stop the shatter (Q3 default). |
| A03 (`design-godot.md` :829; the air dodge refreshed by landing, a ledge grab or a stock loss) | 5.3 | It is also refreshed when hit, in both modes. |
| M05/M07 Launch Flag & Knockdown and Landing Tech (`design-godot.md` :1286–1287), `VERIFY-M05-KNOCKDOWN-SCOPE` | 3.2 | Tumble outlives hitstun, and the landing resolution keys on tumble. The Story hero is in scope; Story mobs stay out. |
| `DEFER-H7-STORY-TERMINAL` (the short-hop half), `design-godot.md` :957 / :977 ("Story only") | 5.1 | The sim gains the ×2.5 short hop. |
| M-18 (`design-godot.md` :1027, a deliberate drop-through divergence) | 6.3 | Closed. Both inputs work in both modes. |
| Spawn Invincibility (`design-godot.md` :2147–2150, 3.0 s) | 4.2 | Now 120 frames shrinking to 60, ended by the first attack. |
| The 2026-08-10 sim ledge grab (`GAMEPLAY_FEEL_2026-08-10_PLAN.md` §2.11; `design-godot.md` :790, "Up/toward-stage is not a separate climb input") | 5.2 | Fighter gains ledge get-ups. |
| `CPU_RECOVERY.md`, `MIRROR_PARADOX.md` | 1.1(5), 5.2 | Recovery jumps are held full hops. The CPU stays climb-jump only at a ledge. |
| `VERIFY-STORY-HAZARD-BLOCK` (held for a ruling) | 7.4 | Ruled. A raised block absorbs a hazard tick. |
| `USER-STORY-METER-SCALE` / `PROVISIONAL-STORY-METER-SCALE-RULES` (5) ("spends are unchanged"), `PROVISIONAL-STORY-METER-GAIN-SCOPE` (2) | 7.3 | Story Echo Step costs ×0.35. |
| `PROVISIONAL-BASICS-PACING` (S4) | 8.1 | Adds a Standard-tier basic floor. |
| `F9-DEALT-HIT-FEEDBACK`, `PROVISIONAL-STORY-CONFIRM-PRORATION` | 7.1, 8.2 | Ability-hit shake scales with damage. Prorated numbers are dimmed. |
| `VERIFY-GAMEPAD-ULTIMATE-CHORD` (a), (c); `PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION` | 9.2 | Closed and amended. |
| Keyboard-as-P1 (`design-godot.md` :1834–1837, the join workflow) | 9.1 | P1 follows the last-used device. Press-to-join on Fighter select. |
| `DEFER-ULT-TELEGRAPH` | 2.1 | Closed. |
| `VERIFY-SIM-STRING-FACING-PER-TICK` | 10.1 | Closed. |
| `VERIFY-SIM-HURTBOX-WIDTH` (copies), `VERIFY-STORY-HURTBOX-UNIVERSAL` | 8.4 | The copies are merged. The target size stays open. |
| `VERIFY-STORY-MIRROR-HOLD` (a record only) | 2.4 | The clone's post-contact Story Ultimate gains the same startup armour. |
| `USER-BOT-COMBAT-TELEMETRY` | 7.2 | Gains the re-hit counter. |

### Rulings kept as they are

Do not touch these:
- the 2026-10-09 "Document both builds" gravity ruling (6.4);
- the single air strike (`USER-AERIAL-SINGLE-STRIKE`, 10.3);
- the −20 % speed cut (`USER-MOVEMENT-SPEED-CUT`, 5.4);
- F22 Sudden Death (10.2);
- the held-Block landing tech (3.1);
- the D15 Ultimate totals (2.2);
- hitstop 4–11 (`PROVISIONAL-HITSTOP-WEIGHT`, 8.3);
- no combo scaling in Fighter (1.4);
- no counter-hit reward in Fighter (4.4).

### 0.1 Questions for the user (ask before the phase named; the default applies without a ruling)

| Q | Decision | The conflict | Default (built if no ruling) | Alternative | Ask before |
|---|---|---|---|---|---|
| Q1 | 1.2 "matching Story" (cast motion) | Story's `ProcessUsingSpecial` (`PlayerController.cs:3214`) applies only `ApplyGravity`, which writes Y, and the cast never zeroes X (`TransitionTo` has no `UsingSpecial` case; the cast sites at ≈ :5458/:5471 set no velocity). So a running Story caster slides at its entry speed with no friction for the whole cast. | **Match Story.** A sim caster keeps its entry horizontal velocity, grounded and airborne, with no stick and no friction. CM-SIM's Story probe confirms the reading first (2.1.6). | Both modes stop with the 4-frame stop ramp. This changes Story's `ProcessUsingSpecial` in the same declared line region. | Phase A |
| Q2 | 2.4 "Armour the startup: `GrantsHyperArmor` on all nine" | Under `StoryCombatRules.HyperArmorPreventsInterruption` the flag armours Startup **and** Active, which is 84–210 frames per Ultimate. The decision's two halves disagree. | **Startup only.** A Story rule armours the hero's Ultimate while its own cast clock is in Startup. The `.tres` flags stay false. | Set the flag on all nine (Startup + Active). | Phase A |
| Q7 | 1.1(1) "audit every movement ability's vertical path" | Lincoln's Story Rail Charge carries 600 px/s through its 18 recovery frames, ≈ 180 px past the authored 300. The sim zeroes X at the end of the charge. This is horizontal and outside the decided scope. | **Unchanged.** Only the gravity half ships. The carry is recorded as `VERIFY-RAIL-CHARGE-RECOVERY-CARRY`. | Zero X at Story's `OnRecovery`, as the sim does. | Phase A |
| Q3 | 3.4 ("costs no charge") against 1.3 ("keep the full shatter") | A Shield-Breaker landing inside the perfect-block window. | **Still shatters.** 1.3 keeps the Shield-Breaker identity, and Foresight stays the counter. | The perfect block absorbs it with no charge spent. | Phase B |
| Q4 | 5.1 "about 45–55 % of full height" | The recon's arithmetic puts a 1–2-tick tap under ×2.5 at about 42–47 %. | **Keep ×2.5** (decided). Measure every hero. Record any tap below 45 % per hero in `PROVISIONAL-SIM-SHORT-HOP-HEIGHT` without retuning. | Retune the shared multiplier, in both modes. | Phase B |
| Q5 | 5.1 "gravity ×2.5 while rising with Jump released" | Taken literally (and as Story's `SelectBand` does at `StoryAirPhysics.cs:168`), this also cuts rises the fighter did not jump into. That covers a post-hitstun rise, the Tempest owner lift and an airborne non-launching hit. | **As decided, both modes.** Any normal-control rise with Jump released takes ×2.5, as Story already does. Launch states keep `StepLaunch`. Pinned with `PlayerInputFrame.Create`-built input. | Gate both modes on "rising from this airtime's own Jump press". This also changes Story's band. | Phase B |
| Q6 | 6.2 "both modes" (the corner forgiveness) | The sim has nothing for either nudge to act on. Walls and the ceiling are full-width, platforms are one-way from below, and floor segments snap from any depth. The only candidate "sim lip pop" (landing a falling fighter within 0.2 u below a one-way surface) is new behaviour. It would turn near-edge ledge captures into landings, because landing resolves before `TryGrabLedge` (`FighterSimulationSystems.cs:716–744`). | **Story only. The sim half is a documented no-op**, with a geometry guard that pins the premise. | The 0.2 u one-way apex pop, ledgered as new Fighter behaviour. | Phase B |
| Q8 | 4.2 "**Both**" | Read here as both levers (the shrink and the early end), Fighter only, because Story has no respawn platform. The alternative reading is both modes, which would apply the early end to Story's 120-frame post-rewind protection. | **Fighter only, both levers.** Story's post-rewind protection is untouched. | Story's post-rewind protection also ends on the first attack or Special. | Phase B |

### 0.2 Rulings received (2026-10-16) — these override every "default" sentence below

All eight questions are ruled. Where a dossier below still says "Q*n* default", the ruling here wins; the dossiers named in the last column were edited to match.

| Q | Ruling | Default? | What changes from the draft |
|---|---|---|---|
| Q1 | **Brake to a stop, both modes.** A grounded Special cast brakes its horizontal velocity to 0 over the run-stop ramp (`UniversalMovementRules.RunDecelerationFrames`, 4 frames, the same per-tick step as the run stop); an aerial cast keeps its air drift (no stick input, no added friction). A kit that writes its own velocity (Divine Piercing's lunge, the Tempest lift, kit motions) overrides it. Story's `ProcessUsingSpecial` gets the same rule, so Story Specials stop sliding. | No | 2.1.6 Motion; CM-SIM +2 cases (Story grounded stop, Story aerial drift); risk row 6's "sliding" item is gone. |
| Q2 | **Startup only.** A Story rule armours the hero's Ultimate while its own cast clock is in Startup; the `.tres` flags stay false. | Yes | None. |
| Q3 | **Still shatters.** A Shield-Breaker inside the perfect-block window shatters as it does today. | Yes | None. |
| Q4 | **Keep ×2.5, measure every hero; ledger any tap below 45 %.** | Yes | None. |
| Q5 | **Own jump only, both modes.** The ×2.5 short-hop band applies only to a rise that started with this airtime's own Jump press (ground jump, air jump, ledge climb jump). A post-hitstun rise, a kit lift (the Tempest owner lift), an airborne non-launching hit and any other rise are never cut. Story's `StoryAirPhysics.SelectBand` gains the same gate, so Story changes too. | No | 2.5.1 Scope, latch and tests; §1.5 `ActionFlags` bit 3 `RiseFromOwnJump` (CM-MOVE); CM-MOVE +1 Story case; risk row 4's CPU post-launch item is gone. |
| Q6 | **Add the Fighter lip pop.** In the sim, a fighter whose feet are within 0.2 u **below** a one-way platform's top at the tick its vertical velocity turns from rising to falling (or while falling past it within that band) is placed on the platform and lands. It resolves before `TryGrabLedge`, so some near-edge ledge captures become landings; that is accepted and recorded. The sim head-clip nudge stays a no-op (no partial ceilings exist). | No | 2.6.2 Sim; Fighter hash moves; CM-MOVE +3 sim cases; re-run `CpuRecoveryMatrixTests` on the Open stages; `PROVISIONAL-SIM-LIP-POP`. |
| Q7 | **Stop it like the sim.** Story's Rail Charge zeroes its horizontal velocity when its recovery starts (`OnRecovery`), as the sim does at the end of the charge, so it travels the authored 300 px. | No | 2.1.1; CM-STORY +1 case; `VERIFY-RAIL-CHARGE-RECOVERY-CARRY` is not opened. |
| Q8 | **Fighter only, both levers** (the shrink and the early end). Settled by the round-4 option text. | Yes | None. |

**Declared deltas after the rulings:** CM-SIM **+45**, CM-STORY **+21**, CM-MOVE **+36**; Phase A **+104 → 3332**; Phase B **+96 → 3428 = 3228 + 200**, plus any review-fix deltas. Each handoff still reconciles its exact figure.

---

## 1. Standing decisions (every workstream)

### 1.1 Authority and docs

- Workstream agents do **not** edit any of these: `design-godot.md`, the design master `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`, `AGENTS.md`, `CLAUDE.md`, `docs/design-contracts/*.md`, the docs-3 contract masters, or the ledger.
- Each handoff lists the lines owed under "For the closeout". Cite `design-godot.md` line numbers at `e2c655a`.

**The design master and its mirror.** The closeout edits them together. This returns to the 2026-10-09 convention, because this package's workflow names "design master + mirror"; the 2026-10-11/12 convention left the mirror untouched. Apply each hunk to the master, apply the same hunk to `design-godot.md`, then `diff --strip-trailing-cr` the two. The only lines allowed to differ are the `DOC-MASTER-UNBOUND-BINDING` lines: `:2286` and `:2288–2289` (the Controls table's Ultimate / Grab / Echo Step rows) and `:4141` (the C01c bullet). An edit to one of those lines (9.2 touches `:2286`) is made **by hand in each copy**, keeping each copy's own Unbound wording. Never copy the master over the mirror.

**The contract masters.** `D:/Projects/fighters-through-time-docs-3/docs/` holds masters of the contracts. Treat them like the design master: edit both copies with the same hunk, then `diff --strip-trailing-cr` each pair. The copies' line endings differ (the repo's `COMBAT_VALIDATION.md` is CRLF), so preserve each file's own.
- `DEFENSIVE_EFFECTS`, `ABILITY_DATA`, `CPU_RECOVERY`, `CPU_COMBAT_POLICY`, `MIRROR_PARADOX`, `FIGHTER_MATCH_RULES` and `MULTIPLAYER_DELIVERY` are identical today (ignoring line endings), and must still be identical afterwards.
- These already differ, and afterwards must differ only on these lines:
  - `COMBAT_VALIDATION`: the repo's Package 12 W3b note at :37–38.
  - `COMFORT_SETTINGS`: the Unbound lines :101–103, :128, :130 and :136.
  - `ROLLBACK_STATE_CONTRACT`: :37.

  Edit these three by hand in both copies.
- The docs-3 copy of `DESIGN_BUILD_DEVIATIONS.md` is already 396 lines behind the repo's. It is not edited; the repo ledger is the live ledger.

**The docs-3 repository** already has unrelated uncommitted edits (`design-godot-v7.md` and three HTML pages, on top of `d695d7b`). The closeout does not commit that repository unless the user asks. It reports the hunks it applied, per file.

**Lines owed by earlier packages.** Lines owed by the 2026-10-11/12 indexes stay owed, with one exception: if this package rewrites a paragraph that holds an owed line, apply that line too and strike it from its index. Example: `:1282`'s "Low-HP scaling … applies to every hit in both modes", owed by `USER-STORY-STATIC-KNOCKBACK`.

### 1.2 Mode scope and Story/Fighter isolation

- Every decision states its mode in §2.
- Story-only rules live in `StoryCombatRules`, `StoryEnemyCombatRules`, `StoryAirPhysics` or a Story controller.
- Nothing under `scripts/FighterSim/` may name any of these: `StoryCombatRules`, `StoryAirPhysics`, `StoryEnemyCombatRules`, `StoryEngagement*`, `EncounterLockRules`, a Resonance type, or the Story meter scale. That holds today (`grep` returns nothing). CM-CANCEL adds a source scan that keeps it true (2.11.1).
- Before the sim reads a cross-mode number that lives in a Story-named rulebook, move the number to `BasicComboRules` or `UniversalMovementRules` (`scripts/Core/UniversalMovementRules.cs`) and forward the Story name to it. Example: the D3 table, `StoryCombatRules.cs:72–99`.

### 1.3 Determinism

- `scripts/FighterSim/` runs on FP64/FPVector2 only.
- A shared float constant reaches the sim once, as a `static readonly` `FP64.FromDouble(...)` field. Never convert per tick.
- New sim rules are integer formulas (shieldstun, the respawn shrink, frame windows) or such constants (×2.5, ×0.5).
- No `Random` without `match.RandomState`, and nothing read back from InputManager, the CPU's local latches or Godot state.
- A `KLOTHO_DET002` warning is never pre-existing (CLAUDE.md). The expected build warnings remain CS8632, `KLSG_ECS004` on `FighterTuningComponent` and CS0649. A new `KLSG_ECS004` naming component 323 means the layout below grew past 128 B.

### 1.4 One canonical value

Every new number lives once:
- in a rulebook: `BasicComboRules`, `UniversalMovementRules`, `FighterMatchFlowRules`, `StoryCombatRules` or `StoryEnemyCombatRules`;
- or in a `.tres`.

Pure rulebooks stay engine-free (CLAUDE.md failure signature 7). A Story literal that a sim rule must match moves to the shared rulebook, and the Story site forwards to it. For example, `ProcessUsingSpecial`'s `0.5f` becomes `UniversalMovementRules.AerialSpecialGravityMultiplier` (2.1.6).

### 1.5 New sim state: one Klotho component, allocated here, and nowhere else

The inventory at `e2c655a`:
- in use: 300–310 and 312–322;
- retired: 311;
- free: **323**. No doc or code reserves it; I checked with `grep -rn "KlothoComponent("` and a repo-wide search for `323`.

The plan allocates it. No workstream allocates another ID, grows 312 or 320, or adds a field anywhere else. A workstream that finds it needs more state records the need in its handoff and uses only the reserved bits in `ActionFlags`.

```csharp
// scripts/FighterSim/FighterSpecialCast.cs (new; owner CM-SIM, Phase A0)
[KlothoComponent(323, MaxCount = 2)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct FighterActionStateComponent : IComponent {
    public int SpecialSlot;          // 0 none, 1, 2                                      (CM-SIM)
    public int SpecialPhase;         // 0 none, 1 Startup, 2 Active, 3 Recovery,
                                     // 4 Delegated (Foresight runs on 322)              (CM-SIM)
    public int SpecialPhaseFrames;   // frames left in SpecialPhase                       (CM-SIM)
    public int SpecialPhaseElapsed;  // frames elapsed in SpecialPhase (0 on its first)   (CM-SIM)
    public int SpecialFlags;         // 1 StartedAerial, 2 EffectFired, 4 MeleeResolved,
                                     // 8 LandedDirectHit                                 (CM-SIM; bit 8 CM-CANCEL)
    public int SpecialCancelBuffer;  // bits 0-2 verb (0 none,1 roll,2 jump,3 basic,4 movement),
                                     // bits 3-6 age, bit 7 Up held, bit 8 Down held      (CM-CANCEL)
    public int BlockTiming;          // bits 0-7 perfect-block frames left; bits 8-15 frames since
                                     // the stance last dropped (0 = never raised this stock,
                                     // otherwise 1..255 saturating); bit 16 stance up last tick (CM-BLOCK)
    public int DownTapFrames;        // 0..18, the double-tap Down window                 (CM-MOVE)
    public int ActionFlags;          // bit 0 RespawnProtection (CM-HIT); bit 1 JumpReleasedSincePress
                                     // (CM-MOVE); bit 2 LedgeStickRearmed (CM-MOVE); bit 3 RiseFromOwnJump
                                     // (CM-MOVE, Q5); bits 4-31 reserved, 0
}
```

- **Size.** 9 ints = **36 B per fighter, 72 B per snapshot.** All-zero means idle, and that includes `BlockTiming`: a zero lowered-age reads as "never raised", which is eligible (2.3.2).
- **Lifecycle.**
  - The component is added in `FighterWorldSystem.SpawnFighter`, beside `FighterCounterComponent`.
  - It is held at zero in every field while `FighterMatchFlowRules.IsOnRespawnPlatform` is true. `FighterSimulationRules.ApplyStockLoss` has no `Frame`, so this is the stock-loss reset; it works the same way `AirDodgeUsed` is refreshed.
  - `RespawnProtection` is set at the platform drop (CM-HIT).
  - The Sudden Death setup reset in `FighterMatchSystem` zeroes it.
- **Read seam.** `FighterSimulation.TryGetFighterActionState(int playerID, out FighterActionStateComponent)`, following the `TryGetFighterCounter` pattern.

**Zero-byte reuses, reserved here.** I verified each value is free at `e2c655a`.

| Where | Value | Meaning | Owner |
|---|---|---|---|
| 305 `AttackFlags` | 256 | `FlagActiveFacingRight` (the box facing, latched at active start) | CM-SIM |
| 305 `AttackFlags` | 512 | `FlagLedgeAttack` | CM-MOVE |
| 305 universal-movement slot | 8, 9, 10 | `LedgeClimb`, `LedgeRoll`, `LedgeAttackClimb`. Values 8–15 are free; the air dodge owns 5–7 and kit phases own 16–31. | CM-MOVE |
| 310 `Tumble` | 2 | Tumbling after hitstun | CM-HIT |

**The action chokepoint (A0).** `FighterActionRules.OnActionAccepted(ref …, FighterActionKind kind)`. Every acceptance site passes 300, 310, 312 and 323.
- Kinds: `Swing`, `Special`, `Ultimate`, `Grab`, `Jump`, `AirJump`, `Roll`, `AirDodge`, `MovementAbility`, `EchoStep`, `LedgeGrab`, `LedgeGetUp`.
- The **offensive** kinds are `Swing`, `Special`, `Ultimate` and `Grab`.
- The body is empty in Phase A; CM-HIT fills it in Phase B.

`MAX_COMPONENT_TYPES` in the Klotho runtime could not be read without running anything. Component 322 registers today, so 323 should too; CM-SIM's first build confirms it. The 2,699 B snapshot figure in `docs/PERFORMANCE_BASELINE.md` (2026-08-08) is stale, and the closeout re-measures it.

### 1.6 No save schema change

Schema v8 stays. Nothing in this package is persisted:
- Device follow and join state are runtime only.
- Chord leniency is local input.
- The Story Echo Step cost is a constant.
- The stale Ultimate pad row is repaired at load by a pure function, as `PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION` already does.
- The `project.godot` default is not save data.

### 1.7 No protocol change

- `PlayerInputFrame` stays 12 B. `RollbackInputPacket` stays 47 B. `GameplayButtons` is unchanged. `ProtocolVersion` stays **4**.
- The short-hop latch reads the existing `Released` field, which is already on the wire (bytes 10–11). The Down tap reads the existing Down bit.
- Chord leniency and press-to-join run in InputManager before `PlayerInputFrame.Create`.
- Every Fighter hash moves, so builds from before and after this package would desync on LAN. LAN is de-scoped to Package 7, and every hash move since Package 12 shipped without a bump. The closeout records this decision.

### 1.8 Tests

- One `[TestSuite]` per file (signature 6).
- A pure-C# suite carries no `[RequireGodotRuntime]` and touches no engine API (signature 7). That includes `CharacterData` `.tres` loads, `FighterLoadoutFactory` on production data, `CharacterRoster` and `ContentManifest`. Source scans use `System.IO`, never Godot `FileAccess`.
- A suite that writes `GameManager.CurrentSession` snapshots it and restores it in `finally` (signature 8).
- A suite that sets `SceneTree.Paused` restores it in `finally` (signature 4).
- Grouped nodes added to the root are freed in `finally`.
- A real-scene suite that walks a hero through a level and does not test locks sets `EncounterLock.SuppressForTesting` in `try`/`finally`.
- Use the CLAUDE.md idiom instead of `OverrideFailureMessage("")`: `if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");`.
- **Hand-built frames hide real-input behaviour.** Sim harnesses build frames with `Held = Pressed = buttons` and never set `Released`. Any rule that reads an edge (the short-hop latch, the perfect-block arming, the Down double tap, the ledge stick rearm) must also be pinned with `PlayerInputFrame.Create`-built input.
- Every agent reports its **exact** delta: new cases, and rewritten cases named before → after. Reconcile the full-suite `Total:` against the declared running total (§4.2).
- No suite pins an absolute Fighter hash. Keep it that way: pin replay identity and snapshot round trips.

### 1.9 GdUnit and Godot launches are serialized across every checkout (signature 5)

Before **any** Godot launch (`dotnet test`, `--import`, `--quit`, a smoke run, a bot run):
1. Drain orphans. Run `Get-Process testhost,Godot*`, then stop `testhost` and `Godot*_console` only. Never stop the user's non-console `Godot_v4.7.1-stable_mono_win64` editor.
2. Take the lock with `mkdir "D:/Projects/.ftt-gdunit-lock"`. Retry every 15–30 s while it exists. If it is older than 20 minutes **and** no `testhost` or `Godot*_console` process is running, remove it.
3. Release it with `rmdir` immediately after the run, even on failure.
4. Batch your work: filtered runs first, and one full run at the end of the workstream.
5. Never accept a `Total:` without reconciling it.

### 1.10 Worktree hygiene

- A fresh worktree has no `.godot/` cache. Copy the main one, holding no lock: `robocopy "D:\Projects\Fighters Through Time - V3\.godot" "<worktree>\.godot" /E /XD mono`. `.godot/` is git-ignored; never commit it.
- Then run `--headless --import` once, holding the lock.
- Export `GODOT_BIN=D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe` explicitly.
- Run `git checkout -- assets` before you commit, to drop the `*.png.import` churn.
- A new `.cs` file gets a `.uid` sidecar on import. Commit it with the file.

### 1.11 Commit and report

- Commit on your worktree branch with the message `CM-<id>: <summary>`, ending with the trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Do not merge into `main`.
- Write `docs/handoffs/CM_<id>.md` (format in §6) and commit it with your work.
- Report the branch name, the final commit SHA and the worktree path. CM-SIM also reports the `cm-sim-a0` tag's SHA.

### 1.12 Shared files (the conflict policy)

- Edit only the files and regions your workstream owns (§3).
- Put new behaviour in **new partial files or new members**: `PlayerController.<Topic>.cs`, `FighterMovementSystem.<Topic>.cs`, `FighterCombatSystem.<Topic>.cs`.
- Touch a shared method only to insert a **call line** at the site declared in §3.
- In `BasicComboRules.cs` and `UniversalMovementRules.cs`, add constants only at your declared anchor, never at the end of the class. Two appends at the same place conflict.
- If you must touch something you don't own, keep it minimal and list it in the handoff under "outside my ownership".
- A defect in another workstream's region goes in your handoff with a minimal repro. The orchestrator fixes it.
- **Test files shared by two workstreams** (named in the §3 maps): add your cases as separate methods, never rewrite another workstream's case, and name every case you touched in your handoff. The later-merging branch resolves the union.

### 1.13 Localization

- Every visible string is a key in `localization/en.csv`, under a `# CM-<id>` comment block.
- Run `--headless --import` after editing `en.csv` and commit the regenerated `localization/en.en.translation`. `--quit` does not reimport it.
- A new key must be referenced (`UnusedTranslationKeyTests`).
- `en.en.translation` is a binary that two branches in each phase both regenerate, so their merges conflict. **The orchestrator resolves each such conflict by taking either side and re-running `--import`** on the merged `en.csv`, then commits the result. Agents never hand-merge it.

### 1.14 Provisional numbers

- Every number the user did not state is marked *Provisional* here.
- Every number an agent chooses beyond this plan goes in the handoff's "Decisions" with one line of reasoning.
- The closeout writes each as a `PROVISIONAL-*` ledger row (stable ID, value, file, owner `Unassigned`, acceptance criteria).
- Nothing provisional is quoted as measured.

### 1.15 Nobody plays the build

Headless tests, smoke runs and bot runs are the evidence, and each handoff says so. §5 is the checklist for the first two-player pad session, which is the user's tool for every "judge by hand" item (9.4).

---

## 2. Per-decision dossiers

Each dossier gives:
- the decided rule;
- the current build;
- the design, with its numbers;
- the change points;
- the tests;
- the docs;
- the acceptance criteria.

"WS" names the owning workstream (§3).

### 2.1 Round 1 — bugs and Fighter Specials

#### 2.1.1 — 1.1(1) Story movement abilities get gravity; Leonardo's glide sinks · WS **CM-STORY** · Story only · Fighter hash: no

**Decided.** Make Leonardo's glide sink. Audit every movement ability's vertical path. Joan's Wings rise is intended.

**Now.**
- `PlayerController.ProcessUsingMovementAbility` (`PlayerController.cs:3884`) never calls `ApplyGravity`. Each kit writes `Owner.Velocity` after the controller's `MoveAndSlide`.
- Leonardo (`LeonardoOrnithopterFlight`, `LeonardoAbilities.cs`):
  - `OnStartup` sets vy −380 px/s.
  - The glide only clamps descent (`vel.Y = Min(vel.Y, GlideSinkSpeedPixels /*40*/)`), so the boost carries for the whole 3 s. That is about 1,250 px of rise, or a hover under a ceiling.
  - The sim glide rises about 0.84 u (≈ 50 px), then sinks.
- The audit's other findings, against the sim's `FighterKitMotion.SuspendsGravity` / `ReachSuspendsGravity`:
  - Tesla's Blink recovery hovers for 10 frames.
  - Tubman's Leap recovery hovers for 8 frames.
  - Cleopatra's Mirage carries its entry velocity through a 12-frame startup and hovers for 18 recovery frames.
  - Mozart's Drift holds 12 startup frames (the "opening beat") and hovers 6 px above its own staff for 18 recovery frames.
  - Lincoln's Rail Charge flies level for 30 frames, then carries 600 px/s through its 18 recovery frames (≈ 180 px past the authored 300; that horizontal half is Q7).
  - Einstein (fold startup hold), Shakespeare (gust) and Joan (Wings) already match the sim.

**Design** (*Provisional* scope, `PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`).
- Add `BaseSpecial.SuspendsOwnerGravityNow` (virtual, default `false`), beside `PassesThroughProjectilesNow`.
- `ProcessUsingMovementAbility` applies Story gravity whenever it is false, through `ApplyGravity(dt, ignoreShortHop: true)`.
  - The new optional parameter passes `jumpHeld: true` to `StoryAirPhysics.StepVertical`, so an ability boost is never cut ×2.5.
  - After 6.1, a held read also means a movement ability that started from a jump keeps the apex hang. That matches the sim, where every kit step runs as a held jump (2.5.1).
- Overrides return true exactly where the sim suspends gravity, or where the kit rewrites velocity every tick:

  | Kit | Gravity suspended during |
  |---|---|
  | Joan Wings | every phase and the dive |
  | Tesla Blink | Startup, Active |
  | Tubman Leap | Startup, Active, first Recovery frame |
  | Einstein Warp | Startup, Active |
  | Shakespeare Prospero | Active |
  | Cleopatra Mirage | Active |
  | Mozart Drift | Startup (the opening beat is kept) and Active |

- Leonardo has no override. The −380 boost decays under gravity (the peak is about 58 px), then the glide caps descent at 40 px/s.
- Lincoln's charge writes X only, so gravity applies throughout. **Q7 ruled "stop it like the sim" (§0.2):** `LincolnRailCharge` zeroes the owner's horizontal velocity at `OnRecovery`, as the sim does at the end of the charge, so the charge travels the authored 300 px and no further.

**Change points.**
- `scripts/Combat/BaseSpecial.cs`.
- `PlayerController.ProcessUsingMovementAbility`.
- `PlayerController.ApplyGravity` (the optional `ignoreShortHop` parameter; a CM-STORY declared region).
- The movement classes in `scripts/Characters/Abilities/*Abilities.cs`.

**Tests.**
- New: `StoryMovementAbilityGravityTests`, 6 cases (5 + the Q7 case), in real frames:
  1. Leonardo peaks at ≤ 70 px and glides down at exactly 40 px/s.
  2. Joan rises 36 × 7 px, unchanged.
  3. Tesla, Tubman, Cleopatra and Mozart fall during recovery.
  4. Lincoln's charge falls once it leaves the floor; its 300 px travel is unchanged.
  6. (Q7) Lincoln's charge stops at the start of its recovery: horizontal velocity is 0 on recovery frame 0 and the total travel is 300 px ± 2 (the suite is therefore **6** cases).
  5. `SuspendsOwnerGravityNow` agrees with `FighterKitMotion.SuspendsGravity`, phase by phase, for the five kit-phase abilities.
- May change in place:
  - `MozartSonataDriftChargeTests.AStaffLandingRefundsHalfTheCooldownOncePerAirtime`
  - `TubmanKitStoryTests.TheLeapSnapsToALedgeHalfAUnitBeyondTheNormalCapture`
  - `TeslaLightningBlinkStoryTests`
  - `StoryReachKitTests`
  - `ResonanceTraversalTests`
  - `CleopatraSandDecoyTests`
  - `StoryProsperoGustTests`
  - `StoryRelativityWarpFoldTests`
  - `LeonardoMidGlideBoltTests`
  - the `AbilityInterruptionTests` `UsingMovementAbility` cases

**Docs and ledger.**
- `FIX-STORY-MOVEMENT-ABILITY-GRAVITY` (MOV-13), citing `VERIFY-WING-DIVE-LEAP` as the intended exception.
- `PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE` (the per-kit dispositions).
- `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY` (Q7, closed in this package).
- Design master: `:989–991`, §5 Leonardo and §5 Lincoln (Rail Charge distance).
- AGENTS.md: "Package 13 kit changes, in brief".

**Acceptance.** No Story movement ability gains height after its authored rise. Leonardo descends from his first glide frame. Joan's rise is unchanged. The suites above are green.

#### 2.1.2 — 1.1(2) Every accepted Special press spends · WS **CM-SIM** · Fighter · hash: moves

**Decided.** An accepted Special press always spends its cooldown and lockout. This is subsumed by 1.2: an out-of-range press plays and whiffs.

**Now.**
- `FighterCombatSystem.BuildIntent` (`FighterSimulationSystems.cs:3494`) returns `default` in two cases: when Splitting Strike's arc does not reach (`SplittingArcReaches`, :3531), and when a generic melee target is beyond `AttackRange` or behind.
- Cooldown and lockout arm only in `ApplyIntent`, so a miss is free.
- The same path loses the tick to the movement ability (`VERIFY-SAME-TICK-VERB-PARITY` (5)).

**Design.**
- Lincoln S2 and every Melee-execution slot are accepted on press in `FighterAbilityEntitySystem.Update`. Acceptance runs: the cooldown, `ArmAbility`, the Momentum reset, `CancelString`, `OnActionAccepted(Special)` and `FighterSpecialCastRules.Begin`.
- Acceptance happens **before** the movement-ability branch, so the same-tick `armedSlot` guard applies.
- Contact is tested on the active frames (2.1.6).

**Tests.** `SplittingStrikeSpikeTests` +2:
- an out-of-range press arms the cooldown and the 30-frame lockout, plays 14/6/22, and lands nothing;
- a same-tick Splitting + movement-ability press casts Splitting.

**Docs and ledger.** `FIX-SPLITTING-STRIKE-FREE-WHIFF` (KIT-02, TWO-04). Close `VERIFY-SAME-TICK-VERB-PARITY` (5). Delete the out-of-reach melee flash note from `PROVISIONAL-HIT-CONFIRM-RULES` (6).

**Acceptance.** No accepted press is free. CPU Lincoln's unchecked Special 2 now whiffs and spends. That is the accepted consequence (9.3), and it goes on the §5 checklist.

#### 2.1.3 — 1.1(3) Lock the sim landing tech against every action, from its landing tick · WS **CM-SIM** (A0) · Fighter · hash: moves

**Decided.** The 12 invulnerable tech frames allow no action (`design-godot.md` :1286, "in both modes").

**Now.**
- `FighterVerbRules.TryLandingTech` fires inside the normal branch's landing resolution (`FighterSimulationSystems.cs:716–720`). The tech branch (:412–417) zeroes velocity and `continue`s from L+1 on, but leaves `PressedButtons` live.
- On the landing tick L itself, the ability and combat systems read that tick's presses.
- `CanReadAbilityPress`, `UltimatePressPayable`, `BuildIntent` and `FighterGrabRules.CanStartGrab` (`FighterGrabRules.cs:75–91`) have no `TechLockoutFrames` term. So Specials, the movement ability, a paid Ultimate and a grab can all start during the tech, including on L.
- Story is correct: its `_techLockoutSeconds` branch returns early.

**Design.**
1. On the landing tick, immediately after a successful `TryLandingTech` in the landing resolution, call `FighterUltimateActivationRules.DiscardInput(ref runtime)`.
2. Call the same `DiscardInput` in the tech branch before `TickCounters`/`continue`. This is the knockdown branch's pattern, and the D10 classifier then sees no press.
3. Add the gate `verb.TechLockoutFrames <= 0` to `CanReadAbilityPress`, to the Ultimate predicate (2.2.2) and to `FighterGrabRules.CanStartGrab`. This is the second gate.
4. CM-SIM also checks whether a knockdown begun on L leaves L's presses live in the same way. If it does, the agent records it in the handoff and does not fix it (§8).

**Tests.** `FighterVerbLayerTests` +3:
- each of Special 1, Special 2, the movement ability, both grab forms, BasicAttack and a paid Ultimate starts nothing during the 12 frames, and the first press after is accepted;
- the same set pressed **on the landing tick L itself** starts nothing;
- no D10 refusal is classified inside the lock.

**Docs and ledger.** `FIX-SIM-TECH-LOCK-ACTIONS` (PFC-05, FGS-05). This closes the tech half of `VERIFY-SAME-TICK-VERB-PARITY` (1).

**Acceptance.** The three cases are green. The Story tech-lock case, `FighterKnockdownTests` and `CalibrationDrillScriptTests` (the landing-tech drill) stay green.

#### 2.1.4 — 1.1(4) A Venom tick in Sudden Death respects `DefyBarred` · WS **CM-HIT** · Fighter · hash: moves

**Now.**
- `FighterDamageRules.ApplyUnattributedDamage` (`FighterEntitySystems.cs:823`) tests `CurrentHP <= 0 && DefyHistoryUsed == 0 && Influence >= MaxInfluence`, with no `DefyBarred == 0` term.
- The direct-hit path has the check, in `ResolveFighterHit` (≈ :614–622).
- The comment there ("Sudden Death pre-marks it used") is stale.

**Design.** Add one shared `FighterDefenseRules.CanDefy(in state, in verb, in defense)` and have both paths read it. CM-HIT's declared region covers both sites. Fix the comment.

**Tests.** `FighterHitPipelineTests` +1: with `DefyBarred = 1` and a full meter, a lethal Venom tick knocks out the fighter. The stock is lost, Defy stays unspent, and no window opens.

**Docs and ledger.** `FIX-SIM-VENOM-DEFY-BARRED` (FGS-18, PFC-21).

**Acceptance.** F22's "no Defy survival proc" holds for damage-over-time ticks.

#### 2.1.5 — 1.1(5) The CPU holds Jump for a full hop · WS **CM-INPUT** · both (the CPU drives the Mirror) · hash: CPU-driven runs move

**Decided.** Verify headlessly that the Level 13 Mirror short-hops, then hold Jump for a full hop.

**Now.**
- `FighterCpuController.EdgeHoldFrames = 2` pulses every edge button, Jump included. `_sustainedHeld` masks edge buttons out.
- The Story clone's `_jumpHeld` latch therefore drops on airborne tick 3, and `StoryAirPhysics.SelectBand` returns ShortHop (×2.5). This is inferred from the code, not run.
- Fighter CPUs are unaffected until 5.1.
- `MirrorParadoxDecisionAdapter`'s only constructor (`MirrorParadoxController.cs:574–578`) hard-wires `tuningOverride: CpuBandTuning.BossOverride(difficulty)`, so a test cannot set `ApproachJumpPercent`.

**Design** (*Provisional*, `FIX-CPU-JUMP-HOLD`).
1. **Add the seam.** Add an `internal MirrorParadoxDecisionAdapter(int seed, CpuDifficulty difficulty, CpuBandTuning tuningOverride)` overload. The existing constructor forwards to it with `BossOverride(difficulty)`. This is CM-INPUT's declared region in `MirrorParadoxController.cs`; nothing else in that file changes in Phase A.
2. **Then add a failing case.** A clone driven through an adapter built with `ApproachJumpPercent = 100` reaches an apex ≥ 95 % of `StoryAirPhysics.SingleJumpRise` for its hero. Record the measured short-hop apex in the handoff before fixing.
3. Add an input-side `_jumpSustained`, outside the snapshot like `_pendingEdges`. Set it when a Jump-carrying pulse finishes its hold. While it is set, OR `GameplayButtons.Jump` into `held`.
4. Clear it when any of these holds:
   - `SelfVelocityYRaw ≤ −2 u/s` (just past the apex band, so 6.1's hang is kept);
   - grounded;
   - hitstun, daze or out of stocks;
   - the exclusive Block hold of `ApplyHitstunDefense` takes over.
5. Cap it at 45 ticks.
6. When a new Jump edge is pending, release Jump for `EdgeReleaseGapFrames` before the press, so double jumps still get an edge. A queued second jump may arrive one tick later than today. Amend `PROVISIONAL-MOVEMENT-FEEL-RULES` (9), whose delivery revalidation meets the new gap.

**Change points.**
- `FighterCpuController.cs`: `Sample`, `AdvanceEdgePulse`, `ApplyHitstunDefense`, and the `EdgeHoldFrames` doc.
- `MirrorParadoxController.cs`: the constructor overload only.

**Tests.**
- `MirrorParadoxTests` +1: the apex case above.
- `FighterCpuBehaviorTests` +2:
  - Jump stays held until vy ≤ −2 u/s;
  - two queued jumps still produce two Pressed edges, with a release tick between them.
- Re-run `CpuRecoveryMatrixTests` and `FighterLedgeTests`' CPU cases.

**Docs and ledger.**
- `FIX-CPU-JUMP-HOLD`.
- Amend `PROVISIONAL-MOVEMENT-FEEL-RULES` (9).
- `CPU_RECOVERY.md` (recovery jumps are full-height holds) and `MIRROR_PARADOX.md`, both copies (§1.1).

**Acceptance.** The Mirror's jumps reach full height. Every CPU suite is green. This must merge before 5.1's sim short hop, and it does: it lands in Phase A.

#### 2.1.6 — 1.2 Fighter Specials get their authored startup, active and recovery · WS **CM-SIM** · Fighter · hash: moves (all)

**Decided.** Every Fighter Special gets its authored startup, active and recovery frames, matching Story. This reopens the 2026-10-11 "not now".

**Now.**
- Sim Specials resolve on the press tick, in three ways:
  1. `FighterAbilityEntitySystem.Update` spawns the projectile, construct or zone, or starts Divine Piercing's 12-frame lunge or the Foresight stance, and arms the cooldown and lockout.
  2. Melee intents resolve in `FighterCombatSystem.BuildIntent`/`ApplyIntent`. That covers Splitting Strike and any slot the ability system did not take; in the production roster that is only Lincoln S2.
  3. Only Foresight has a sim timeline (component 322, 4/20/24).
- `FighterLoadoutFactory.HitData` projects `ActiveFrames` and `TotalFrames`, but not startup or recovery.
- `FighterCombatContractW3Tests.ASpecialLeavesNoRecoveryLockSoTheStanceRisesOnTheNextTick` pins "no recovery lock".
- Story runs every Special on `BaseSpecial`'s phase clock.
  - In `UsingSpecial`, `PlayerController.ProcessUsingSpecial` (:3214) applies only gravity: the literal `0.5f` at :3225 during startup/active if the cast started airborne (design :1097). Velocity X is untouched, so a Story caster keeps its entry X velocity with no friction (Q1).
  - No other verb is polled except the grounded recovery Block cancel and D3.

**Authored frames.** Startup / active / recovery, from `resources/Abilities/*/special_*.tres`. Execution types: 0 melee, 1 projectile, 2 area, 3 persistent. BC 4 = Shield-Breaker.

| Kit | Special 1 | Special 2 |
|---|---|---|
| Einstein | E=mc² 14/6/20 (proj) | Rift 12/18/18 (area) |
| Joan | Smite 12/6/20 (proj) | Divine Piercing 13/12/20 (melee, BC 4) |
| Leonardo | Golden Ratio 12/9/18 (area) | Turret 12/9/18 (persistent) |
| Lincoln | Emancipator 16/6/24 (proj, BC 4) | Splitting Strike 14/6/22 (melee, BC 4) |
| Mozart | Requiem 10/6/14 (proj) | Fortissimo 16/6/22 (proj) |
| Shakespeare | Yorick 10/6/14 (proj) | Tempest 8/12/16 (area) |
| Tesla | Coil 12/6/18 (persistent) | Lorentz 14/6/20 (area) |
| Cleopatra | Serpent Nest 12/6/18 (persistent) | Vortex 12/15/16 (area) |
| Tubman | Conductor's Call 12/30/18 (proj) | Foresight 4/20/24 (melee, on 322) |

Every cast is at least 30 frames long, so the cast itself covers the 30-frame Special → Special lockout.

**Design.** Rules are *Provisional* unless authored; ledger row `PROVISIONAL-SIM-SPECIAL-PHASE-RULES`.

| Tick | What happens |
|---|---|
| T (press) | Accepted in `FighterAbilityEntitySystem` with the unchanged gates and order (basic swing → paid Ultimate → S1 → S2 → movement ability). Cooldown, lockout A+S, `CancelString`, the Momentum counter reset and `OnActionAccepted(Special)` all run here. `FighterSpecialCastRules.Begin(slot, contract, startedAerial)` writes 323: Startup, `PhaseFrames = S`. |
| T+1 … T+S−1 | `FighterMovementSystem` advances the clock **after the hitstop gate**, so a cast pauses in its own hitstop exactly as Story's does with `CastClockSuspended`. Input is discarded. |
| T+S | Startup → Active. A new fire pass in `FighterAbilityEntitySystem` (after the press loop) fires the slot's existing call **once**, reading position and facing at this tick: `SpawnSpecialProjectile` (ground waves and the Requiem contact included), `SpawnPersistent`, `SpawnZone` (the Rift placement and Vortex anchor are read now, and the Tempest lift fires now), or `StartPiercingLunge`, aligned so its 12 moving ticks are the 12 active ticks. Sets `EffectFired`. |
| T+S … T+S+A−1 | Active. A Melee slot is tested on **every** active tick in `FighterCombatSystem.BuildIntent` (`SplittingArcReaches`, the 2-unit `AttackRange`, front-only) until it resolves one contact (`MeleeResolved`). It never arms cooldown or lockout there. |
| T+S+A | Recovery frame 0. Grounded held Block, with every other stance condition met (`IsBlockStance` is true with the live input), clears the cast and keeps the input (Story's Package 12 W3 cancel). D3 comes in Phase B (2.11.1). |
| T+S+A+R | The cast ends. The fighter is free. |

**S = 0 and unprojected contracts.** With S = 0 the cast enters Active on T and fires in the same tick's fire pass.

A contract with `ActiveFrames == 0` runs as S 0 / A 1 / R 0, which reproduces today's press-tick timing. This covers the hand-built `FighterAbilityLoadout.Default` / `FighterLoadout.Default`. It keeps about 25 lockout, hit-confirm and refusal cases valid, along with the `RollbackReadinessTests` KitShape harness.

Ledger the fallback. The rollback gate also gets a production-loadout case (tests below), so it never relies on the fallback.

**The folded total.** The generic melee branch applies `tuning.SpecialOneDamage`/`SpecialTwoDamage`, which are the folded totals of `VERIFY-TUNING-SPECIAL-TOTAL-FOLD`. Only single-hit Splitting Strike (and hand-built default contracts) reach that branch, so one contact per execution equals the authored damage. Amend that row: a future multi-hit generic Melee slot would need the per-hit contract.

**Interrupts.** Any of these calls `Clear`:
- hitstun > 0;
- daze > 0;
- grab busy (grabbing or held);
- captured by an activation strike;
- a stock loss or the respawn platform.

Cooldown and lockout stay spent. What already spawned survives. An unfired effect never fires. No Special authors `GrantsHyperArmor`, and the sim reads that flag only for the movement ability (`FighterLoadoutFactory.cs:67`), so nothing armours a cast.

**The cast lock.** While `FighterSpecialCastRules.IsBusy` is true (Startup, Active, Recovery or Delegated), the caster's input is discarded on **every** tick. That includes its own hitstop ticks. Three places do this:

1. **In the normal branch**, after `TickCounters`, the cast region discards input with the `DiscardInput` pattern (≈ :435–445), except on the recovery Block-cancel tick. This blocks walk, jump, roll, air dodge, swing, block, grab, Echo Step (A06), the other Special, the Ultimate (Story does not poll it in `UsingSpecial`) and the movement ability.
2. **In the hitstop branch (≈ :312–325).** That branch `continue`s before :435, and on its thaw tick the ability and combat systems read live input (ledger `VERIFY-SAME-TICK-VERB-PARITY` (7)(b)). A landed Special freezes its caster: `FighterVerbRules.ApplyHitstop` raises both parties, reached from `ResolveFighterHit` for projectile and melee hits (`FighterEntitySystems.cs:2039`, :793). So the hitstop branch calls `DiscardInput` for a busy caster on every hitstop tick. The call goes after the existing chain and freeze buffers, and before the thaw-tick release. Phase B's D3 feed call goes before it (2.11.1).

   This also keeps a frozen caster's live `HeldButtons` from raising `IsBlockStance` (`FighterSimulationSystems.cs:1889`, which reads Held with no cast term) when a third-party hit arrives during the freeze. CM-SIM verifies that no system reads `IsBlockStance` for a fighter before `FighterMovementSystem` has run that tick, and pins it (tests below).
3. **The second gates.** `FighterGrabRules.CanStartGrab` gains `!FighterSpecialCastRules.IsBusy(...)`. It takes `in FighterActionStateComponent`, and `AdvanceGrabs` passes it from `frame` (`FighterSimulationSystems.cs:2504/2510`). `CanReadAbilityPress` and the Ultimate predicate refuse while busy.

`IsBlockStance` keeps its signature (about 15 callers). The discard keeps it false for a busy caster, and the recovery Block cancel reads it with the live input on purpose.

The D10 classifier then flashes nothing during a cast, matching Story. The only exits are the recovery Block cancel and, in Phase B, D3.

CM-SIM also checks whether the A02 Ultimate caster has the same thaw-tick leak (its discard at ≈ :435 sits after the hitstop branch too). It records the result and does not fix it (§8). Foresight is covered by the Delegated marker.

**Motion** (Q1 ruled 2026-10-16: **brake to a stop, both modes**; §0.2).
- **Grounded cast:** horizontal velocity brakes toward 0 by the run-stop step each tick (the same per-tick step `ApplyNormalMovement` uses for a released stick, from `UniversalMovementRules.RunDecelerationFrames` = 4), so a running caster stops within 4 frames. The stick is discarded.
- **Aerial cast:** horizontal velocity keeps its entry value (its air drift), with no stick input and no added friction.
- A kit that writes its own velocity overrides this (the Piercing lunge, the Tempest lift). A wall still clamps.
- **Story gets the same rule.** `PlayerController.ProcessUsingSpecial` (CM-SIM's declared line region) applies the grounded brake through one shared helper beside the forwarded aerial-gravity line; Story kits that write their own velocity are unaffected. Add two Story cases (CM-SIM's `StorySpecialCastMotionTests`, 2): a running grounded caster stops within 4 ticks; an airborne caster keeps its drift.
- Vertical:
  - When `StartedAerial`, gravity during startup/active is the normal step scaled by `UniversalMovementRules.AerialSpecialGravityMultiplier` (0.5, the single canonical value; design :1097).
  - In recovery, it is the normal `FighterAirPhysics` step.
  - There is no fast-fall, because Down is discarded.
  - A grounded cast that leaves the floor falls on the normal step.
- **The probe comes first.** CM-SIM's commit 6 (§3.3) adds `StorySpecialTimingProbeTests`. Its two cases are Story's effect tick for one projectile Special, and Story's cast motion (a running grounded caster's X velocity across the cast, and an airborne caster's). Run them and record the results **before** writing the motion code.
- If the probe contradicts this reading (Story holds X), CM-SIM stops and reports to the orchestrator before building motion.
- The probe records Story's **pre-change** motion (expected: the slide). The stop rule then lands in both modes in the same commit series; the probe's motion case is rewritten in place to the new rule once the change is in.

**Tick convention.** The sim fires at T+S, its basic-swing convention. The probe measures Story's tick. The recon infers T+S−1 from tree order: the child `BaseSpecial` processes after its owner on the press tick. Record any 1-tick offset under `VERIFY-SAME-TICK-VERB-PARITY` instead of diverging silently.

**Ledge.** `TryGrabLedge` refuses capture while busy. Story grabs only from `Airborne`.

**Root.** The rooted branch cancels 305's universal-movement slot every tick, so Divine Piercing's lunge dies under Root, as it does today on press+1. Keep this and record it.

**Foresight.** It keeps its 322 timeline. 323 records `Slot 2, Phase Delegated` while 322 is busy, so the cast lock, the CPU and D3 have one owner. Its action lock is untouched in Phase A. The Story-parity Block cancel and D3 on its whiff recovery and sidestep come in Phase B (2.11.1).

**Data.** `FighterAbilityHitData` gains `StartupFrames`/`RecoveryFrames`. This is loadout data, not snapshot data. `FighterLoadoutFactory.HitData` projects `max(0, …)` from `AbilityData`.

**Presentation** (read-only).
- `FighterSimulationDriver.ResolveAbilityPresentation` poses `special_1`/`special_2` from 323 through `AttackPoseRules.PoseFor` for the whole cast, with no trailing hold: Startup → WindUp, Active → Strike, Recovery → FollowThrough.
- Keep the 18-frame thirds path only for the fallback.
- Cast VFX fire at `Begin`, impact VFX at the fire tick.
- The movement-ability reel path must not regress (`TheFighterMovementAbilityPlaysItsReelRatherThanAPose`).

**The foundation, Phase A0** (CM-SIM commits 1–5, tag `cm-sim-a0`; the contract is in §3.3):
1. Make `FighterMovementSystem` and `FighterCombatSystem` `partial`. Move `FighterLedgeRules`, `FighterUniversalMovementRules` + `FighterAirDodgeRules`, `FighterBasicAttackRules` and `FighterAbilityLockoutRules` out of `FighterSimulationSystems.cs` (4,237 lines) into their own files under `scripts/FighterSim/`, unedited.
2. Add `FighterActionRules.OnActionAccepted` (§1.5) with an empty body. Place it at every acceptance site: `StartSwing`, the Special acceptance, `TryCharacterUltimate`, the grab start in `AdvanceGrabs`, the ground jump and the air jump in `ApplyNormalMovement`, the roll start, `FighterAirDodgeRules.TryStart`, the Echo Step start, the movement-ability branch and `FighterLedgeRules.Grab`.
3. Add component 323 with every field declared and inert, its resets and its accessor.
4. The 8.4 sim hurtbox merge (2.8.3).
5. 1.1(3) (2.1.3), 2.3 (2.2.2) and 10.1 (2.10.1). Also, all inert until commit 7:
   - the cast-busy plumbing;
   - the `CanStartGrab` cast and tech terms;
   - the hitstop-branch busy-caster discard;
   - the `UniversalMovementRules.AerialSpecialGravityMultiplier` anchor, with `ProcessUsingSpecial`'s `0.5f` replaced by it (one forwarding line);
   - the `FighterAirPhysics` cast-step overload (`StepMovement` with a gravity scale), unused until commit 7.

This foundation is what lets three Phase B workstreams start before the phase machine is done (§3).

**Change points.**
- New: `scripts/FighterSim/FighterSpecialCast.cs` (component 323 and `FighterSpecialCastRules`: `Begin`, `Advance`, `IsBusy`, `IsActive`, `RecoveryFrame`, `Clear`), `FighterActionRules.cs`, the split files.
- Edited:
  - `FighterSimulationComponents.cs`, `FighterLoadoutFactory.cs`, `FighterSimulation.cs`.
  - `FighterSimulationSystems.cs`: `SpawnFighter`, the hitstop branch, the movement cast region, `BuildIntent`/`ApplyIntent`, `AdvanceGrabs`, the landing resolution, the Sudden Death reset.
  - `FighterEntitySystems.cs`: `FighterAbilityEntitySystem.Update` and the fire pass.
  - `FighterGrabRules.cs` (`CanStartGrab`).
  - `FighterKitMotion.Reach.cs` (lunge alignment).
  - `FighterCounter.cs` (the Delegated marker only).
  - `FighterAirPhysics.cs` (the cast-step overload).
  - `scripts/Core/UniversalMovementRules.cs` (the anchor only).
  - `FighterLockoutRefusalRules.cs`.
  - `FighterSimulationDriver.cs` (`ResolveAbilityPresentation` only).
  - `PlayerController.ProcessUsingSpecial` (the one forwarding line).

**Tests.**

New suites:
- `FighterSpecialCastRulesTests` (pure, 6): the phase walk; S = 0; the 0/1/0 fallback; `Clear` on each interrupt; `PhaseElapsed` indexing; Delegated.
- `FighterActionStateTests` (4, A0): all-zero on spawn; zero on the respawn platform after a stock loss; zeroed by Sudden Death; a mid-cast snapshot round trip.
- `FighterSpecialTimingTests` (14). Production loadouts through `FighterLoadoutFactory`, `[RequireGodotRuntime]`:
  1. For all 18 Specials, the effect is absent on T…T+S−1 and present on T+S, and the fighter is locked through T+S+A+R−1 and free on T+S+A+R (one table case).
  2. A hit in startup cancels with the cost spent and no effect.
  3. Hitstop pauses the clock exactly N ticks.
  4. Aerial half gravity reads `UniversalMovementRules.AerialSpecialGravityMultiplier`.
  5. The cast holds its entry horizontal velocity, grounded and airborne; the stick and Down do nothing (the probe's Story behaviour).
  6. The recovery Block cancel works only from recovery frame 0.
  7. No ledge capture while casting.
  8. Sudden Death and stock loss clear the cast.
  9. A melee Special resolves one contact on any active tick.
  10. The zero-contract fallback reproduces press-tick timing.
  11. Every cast-lock refusal, per verb.
  12. **The thaw tick.** After a landed Special freezes its caster, the grab chord, held Block or the Ultimate on the thaw tick start nothing. A projectile arriving during the caster's own freeze, with Block held, is not blocked.
  13. Rollback convergence when a remote press is corrected mid-startup across the active-entry tick.
  14. A cross-mode frame table: every sim contract's S/A/R equals its `.tres`, and `TubmanKitRules` 4/20/24 equals `tubman/special_2.tres`.
- `StorySpecialTimingProbeTests` (2): Story's effect tick for one projectile Special; Story's cast motion. Both are recorded in the handoff.

Additions to existing suites:
- `DivinePiercingLungeTests` +1: 13 in place, 12 lunge, 20 in place; a startup hit gives no lunge.
- `FeelPassSimTests` +1: the cast is posed from its phases.
- `FighterLockoutRefusalRulesTests` +1: a press during one's own cast never flashes.
- `RollbackReadinessTests` +1: production loadouts cast Specials through full phases inside the measured window, so the depth-7 resim and the snapshot cover 323.

**Changed in place.** About 16 suites assert an effect within a few ticks of the press, and about 45 Determinism suites press Specials. Step their fixtures through startup.
- `FighterCombatContractW3Tests.ASpecialLeavesNoRecoveryLockSoTheStanceRisesOnTheNextTick` becomes "Block cancels a Special only from recovery frame 0".
- `FighterSimulationTests`: `ProjectileAbilityUsesAuthoritativeEntityLifecycle`, and the zone, persistent and projectile-flight cases.
- `FighterLedgeTests.HangingSuppresses…`, `FighterMatchFlowTests` (respawn, dual Defy, Sudden Death sweep), `FighterStockLossDespawnTests`, `FighterHitPipelineTests`, `FighterTimeSystemsTests`, `UltimateActivationTests.TheHeldVictimCannotAct`, `FighterGrabTests` (the `CanStartGrab` signature).
- Every `*KitTests`, plus `GroundWaveTests`, `ProjectileBurstSimTests`, `RiftCollapseSimTests`, `RequiemCooldownShaveTests`, `FortissimoWallTests`, `LeonardoTurretSimTests`, `LeonardoUltimateTests`, `TeslaLorentzChainTests`, `TeslaUltimateTests`, `TeslaBlinkSimTests`, `FighterConstructAttackTests`, `SplittingStrikeSpikeTests`, and `FighterKnockdownTests.TheLaunchFlagTableIs…`.
- The helpers that fire an opponent's Special as a hit source: `FighterCpuHitstunDefenseTests.RunLauncherAgainstCpu`, `FighterVerbLayerTests.LandLauncher`, `FighterMovementSpeedCutTests.MeasureRollGetUp`, `RelativityWarpFoldSimTests`, `FighterKnockdownTests.LandWithoutTech`.
- `FeelPassSimTests.ThePresentationPosesFollowTheSimPhases` and `FighterPresentationSyncTests.AcceptedFighterSpecialHoldsTheAuthoredAbilityPose`. CM-ULT adds cases to the latter's suite (§3.1 map).
- CPU suites that assert a Special's effect: re-time the assertions only. The behaviour adaptation belongs to CM-CANCEL.

**Docs and ledger.**
- Close `DEFER-SIM-SPECIAL-PHASES` and `SIM-SPECIAL-POSE-THIRDS`.
- Add `USER-SIM-SPECIAL-TIMING` and `PROVISIONAL-SIM-SPECIAL-PHASE-RULES`. The latter covers the tick convention, the motion rule (Q1), the fallback, one melee contact per execution, the interrupt set, the hitstop-branch discard, Root, the Tempest lift timing and ledge refusal.
- Amend:
  - `USER-COMBAT-FLOW-NOT-NOW`;
  - `PROVISIONAL-ABILITY-LOCKOUT-RULES` (1);
  - `PROVISIONAL-MOVEMENT-FEEL-RULES` (5): aerial Specials now have a sim counterpart through one constant;
  - `VERIFY-TUNING-SPECIAL-TOTAL-FOLD`;
  - `USER-HIT-CONFIRM-CANCEL`'s Fighter sentence. Hit 1's 30-frame hitstun still covers every startup ≤ 16, so hit 1 → a reaching Special remains a true confirm in PvP.
- Design master: FSM rows :783–786, and :1097 ("both modes").
- `ABILITY_DATA.md`: startup and recovery are now honoured in both modes.
- `ROLLBACK_STATE_CONTRACT.md` :37 (by hand, both copies).
- AGENTS.md:
  - the Combat bullets ("The sim has no special recovery lock …", "the sim waits on `DEFER-SIM-SPECIAL-PHASES`");
  - "Sim Specials still fire on press";
  - the Klotho inventory (323, highest ID 323).
- CLAUDE.md: the "Character ability" and "Fighter simulation / netcode" rows.

**Acceptance.**
- Every authored Special honours S/A/R to the frame in the sim, and a whiff is punishable.
- No input reaches a busy caster on any tick, its hitstop thaw included.
- All lockout, hit-confirm and refusal suites are green.
- `RollbackReadinessTests` is green with production loadouts.
- No `KLOTHO_DET002`.
- The full `Total:` reconciles.

#### 2.1.7 — 1.3 Shield-Breakers keep the full shatter and gain their startup · WS **CM-SIM** · Fighter · hash: moves

**Decided.** Keep the full shatter. The startup comes from 1.2.

**Now.** Divine Piercing, The Emancipator and Splitting Strike (BC 4) empty a full shield: a 16-frame freeze, a 60-frame daze and a 300-frame lockout. All three resolve on press, except Piercing's lunge.

**Design.**
- Delivered by 2.1.6. `BlockRules.ChargeCost` and the shatter response are untouched.
- A blocker sees 13–16 frames of startup and can roll, jump or air-dodge out.
- A blocked Shield-Breaker is a `Blocked` outcome, so it never arms D3: the attacker eats its full 20–24-frame recovery.
- Against a perfect block it still shatters (Q3 default, 2.3.2).

**Tests.** `ShieldBreakerTimingTests` (3). For each of the three:
- a block held from T shatters only on or after T+S;
- a roll, jump or air dodge during startup keeps all three charges;
- after a blocked shatter, the attacker stays locked through its whole recovery.

Also log, without asserting, the earliest Ultimate strike after a blocked shatter against the end of the daze. The recon estimates the guarantee survives by 0–12 frames. This goes on the §5 checklist.

**Docs and ledger.** `USER-SHIELD-BREAKER-STARTUP` (records 1.3, FGS-03). Amend the F15/A01 text (§0).

**Acceptance.** The three cases are green. The shatter numbers are unchanged.

### 2.2 Round 2 — the Ultimate

#### 2.2.1 — 2.1 Activation wind-up telegraph, Fighter and the Level 13 Mirror · WS **CM-ULT** · both (presentation) · hash: no

**Decided.** A red flash and the Unblockable X glyph on the 20-frame wind-up, in Fighter and on the Mirror, plus a critical-cue row ready for audio.

**Now.**
- Neither mode draws one (`DEFER-ULT-TELEGRAPH`). The intent is already `design-godot.md` :1275.
- The driver only holds the `ultimate` pose (`ResolvePresentationAnimation`, `ActivationStrikePose`).
- The clone only poses (`PlayerController.ProcessUsingUltimate` / `BeginUltimateActivationStrike`, ≈ :5583).
- `TelegraphGlyph` (`scripts/Enemies/TelegraphGlyph.cs`, `FTT.Enemies`) is created only in code; no scene references it.
- The class colours are private in `EnemyAbilityExecutor`: `UnblockableTelegraph` (1, 0.25, 0.2) at :840 and `GuardCrushTelegraph` at :839. `BasicTelegraph` (:845) is public.
- `CoreMechanicCueIDs` has 29 rows and no wind-up row.
- Latent defect: nothing resets the clone's `_ultimateActivationPhase` when it leaves `UsingUltimate` through a stun or a death.

**Design** (*Provisional* look, `PROVISIONAL-ULT-TELEGRAPH-LOOK`).
- Move `TelegraphGlyph` (with its `.uid`) to `scripts/Combat/` (`FTT.Combat`). Make the three class colours its public static readonly members (`BasicColor`, `GuardCrushColor`, `UnblockableColor`). `EnemyAbilityExecutor` reads them, and `BasicTelegraph` forwards to `BasicColor`, so each colour has one canonical value.
- **Fighter.** While component 321 is in `PhaseWindup`:
  - a steady Unblockable-red tint on the tint-override channel, re-asserted each frame only when `!glow.HasTintOverride`, so a `FlashHit` on the armoured caster does not erase it;
  - a per-player `TelegraphGlyph` at (0, −120), clear of the slot badge at −150, radius 14, `Present(Unblockable, red)`.

  Hide both on the first `PhaseActive` frame. Derive both from state every frame, so rollback and rematch stay correct. Tolerate a null Glow. There is no pulse, so it is C01a-safe.
- **Mirror.** The same, on the clone's `GlowPresentationController` with a child glyph. It is gated on `UsesFighterUltimateActivation`, so the hero's own Story Ultimate never draws it. Add a `TransitionTo` exit hook: leaving `UsingUltimate` clears the telegraph and resets `_ultimateActivationPhase`/`_ultimateActivationFrames`.
- **Cue.** Add `CoreMechanicCueIDs.UltimateActivationWindup = "ultimate_activation_windup"` to `All` and `Critical`, with a Critical row in `core_mechanic_cues.tres` on the CriticalCues path and a silent stream (the audio directive).
  - It plays once, on the wind-up's first frame.
  - In the driver, through an edge cache in `SyncCoreMechanicCues`.
  - For the clone, through `AudioManager.Instance?.CoreCues?.Play`.

**Tests.**
- `FighterPresentationSyncTests` +3:
  - the telegraph shows on every wind-up frame and hides on the first active frame, with the hash identical with and without presentation bodies;
  - a `FlashHit` mid-wind-up does not leave it cleared;
  - the cue routes once and as Critical.
- `StoryUltimateActivationTests` +3:
  - the clone shows it;
  - a stunned or killed clone shows none afterwards, and its phase returns to 0;
  - a player's own Story Ultimate never creates a glyph.
- `CoreMechanicCueCatalogTests` +1.
- In place: `EnemyAbilityExecutorTests.TheTelegraphGlyphShowsTheClassShape…` (namespace).

**Docs and ledger.**
- Close `DEFER-ULT-TELEGRAPH`.
- Design master: Section 8 "7. Core Mechanic Cues" (:3721–3740, + :4939). The catalog test ties Critical rows to the GDD, so the line is owed in the same closeout.
- AGENTS.md: the A02 bullet, and Package 13's "not delivered → Ultimates".

**Acceptance.** Both modes show the telegraph for exactly the wind-up. Presentation never changes a sim hash. The catalog sweep is green.

#### 2.2.2 — 2.3 No Ultimate from the block stance or shieldstun · WS **CM-SIM** (A0) · Fighter (Story already matches) · hash: moves

**Now.**
- `FighterCombatSystem.UltimatePressPayable` never checks `IsBlockStance` (which already covers `ShieldStunFrames > 0`) or `TechLockoutFrames`.
- Story's `ProcessBlocking` never polls the Ultimate, and the design FSM has no Blocking → UsingUltimate edge.

**Design.**
- `UltimatePressPayable`, `UltimatePressAccepted` and `UltimatePressHeldBackByLockout` take `in FighterVerbComponent` and the 323 state.
- They refuse in the block stance, during the tech lock (2.1.3), and while casting (2.1.6). The cast term is inert until the phase machine lands.
- All callers read the one predicate: the combat `Update`, `FighterAbilityEntitySystem` and the refusal classifier. So a refused stance Ultimate also claims nothing: no Special 2 or movement ability beside it, and no flash.
- The Block-release tick is recorded, not changed. On that tick the sim reads Held without Block, while Story's `ProcessBlocking` returns to Idle and drops the press.

**Tests.**
- `UltimateActivationTests` +2: in the stance, and in shieldstun. Each is refused, keeps the meter, and claims nothing.
- `StoryAbilityLockoutTests` +1 (Story parity): Blocking with a full meter stays in Blocking.

**Docs and ledger.**
- `USER-NO-ULT-FROM-BLOCK`.
- Close `VERIFY-SAME-TICK-VERB-PARITY` (1).
- Remove the block-stance miss from `PROVISIONAL-HIT-CONFIRM-RULES` (6).
- Fix the doc comment in `FighterLockoutRefusalRules.cs` (:80–86).

**Acceptance.** The cases are green. No Ultimate leaves the stance in either mode.

#### 2.2.3 — 2.4 Story Ultimates armoured through their startup · WS **CM-STORY** · Story · hash: no

**Decided.** "Armour the startup: `GrantsHyperArmor` on all nine Story Ultimates." The two halves conflict (Q2).

**Now.**
- All nine `resources/Abilities/<hero>/ultimate.tres` have `GrantsHyperArmor = false`.
- The Story reader `AbilityHasActiveHyperArmor` → `StoryCombatRules.HyperArmorPreventsInterruption` (`StoryCombatRules.cs:51–57`) armours **Startup and Active** against any non-Ultimate class.
- `ApplyStoryHyperArmor(seconds)` (`PlayerController.cs:731`) is a free-running frame timer.
- Leak: a StaticCharge status applied after the armour gate still interrupts an armoured cast. The application is `_statusController?.ApplyStatus` in `ResolveIncomingHit` (≈ :1799). It runs `StaticChargeStrategy.OnApply` → `ApplyStun` → `InterruptActiveAbilities`.

**Design** (Q2 default, *Provisional*, `PROVISIONAL-STORY-ULTIMATE-ARMOR`).
- **Startup only.**
  - Add a Story rule, `StoryCombatRules.UltimateStartupArmorPreventsInterruption(phase, incomingClass) = phase == Startup && incomingClass != Ultimate`.
  - Read it for `_ultimate` in `HasActiveHyperArmor` / `HasActiveHyperArmorAgainst`.
  - It is keyed to the Ultimate's own cast clock, so hitstop and Time Freeze pause it. `ApplyStoryHyperArmor`'s timer would not, which is why it is not used.
  - The nine `.tres` stay false, so nothing reaches the sim. If Q2 is ruled "flag", set `GrantsHyperArmor` on the nine instead (Startup + Active) and drop the rule.
- **Close the StaticCharge leak for the hero.** While `HasActiveHyperArmorAgainst(hit.AttackClass)` is true, a StaticCharge from a hit applies without its stun half. This also covers Rail Charge.
- **The clone.** The Mirror clone's post-contact Story Ultimate (`StartUltimateFromActivation`) runs through the same controller, so its startup is armoured too. Record this beside `VERIFY-STORY-MIRROR-HOLD`. CM-STORY runs `MirrorParadoxTests` and owns any in-place change this causes.

**Tests.** `StoryUltimateArmorTests` (5):
1. Every roster hero's Ultimate is armoured for exactly its Startup phase. The roster is enumerated through `CharacterRoster`, never a literal list.
2. A Basic stun in Startup deals damage and does not interrupt; the same stun in Active interrupts.
3. An Ultimate-class hit still interrupts.
4. A StaticCharge-bearing hit does not interrupt an armoured cast.
5. Isolation: the sim loadout's `MovementGrantsHyperArmor` and every Ultimate's `GrantsHyperArmor` are unaffected.

In place: `AbilityInterruptionTests.UltimateInterruptedMidChargeStopsFiringAndReleasesSteering` (interrupt in Active, or with an Ultimate-class hit or a death).

**Docs and ledger.** `USER-STORY-ULTIMATE-ARMOR` (CMB-01, TWO-06). `PROVISIONAL-STORY-ULTIMATE-ARMOR` (Q2). Design master :1117–1121 and :1275. `ABILITY_DATA.md`, the Ultimates paragraph (both copies).

**Acceptance.** No paid Story Ultimate is lost to a mob or boss stun during its startup. Fighter is unchanged: the hash suites are green and untouched.

### 2.3 Round 3 — defence and landing

#### 2.3.1 — 3.2 Tumble outlives hitstun (the Smash rule) · WS **CM-HIT** · both, the Story hero included · hash: moves

**Decided.** After hitstun ends, a launched fighter may act (jump, air dodge, attack) and leave tumble. A fighter who does nothing lands in tumble, which means tech or knockdown.

**Now.**
- Sim: `verb.Tumble = 0` the moment `HitstunFrames` reaches 0 (`FighterSimulationSystems.cs` ≈ :330–337). Tech, knockdown and the A12 bounce all require `HitstunFrames > 0 && Tumble == 1` (`FighterVerbRules.cs` :102, :168, :274, :294, :312).
- Story: `ProcessStunned` clears `_stunTumble` at expiry (≈ :3800) and goes to Airborne.

**Design** (*Provisional*, `PROVISIONAL-TUMBLE-EXIT-ACTIONS`).

*Sim:*
- When hitstun ends with Tumble 1 while airborne, set Tumble = 2 (no bytes). Grounded, set 0.
- The landing resolution (bounce → tech → knockdown) keys on `Tumble != 0`.
- An owed slam bounce survives while `Tumble != 0`.
- `FighterActionRules.OnActionAccepted` clears Tumble 2 for every kind. Drift and fast-fall do not.
- A non-launching hit on a Tumble 2 fighter leaves it. A fresh launch sets 1.
- **Gravity.** Hitstun arcs keep `StepLaunch`, so DI, launch arcs and tech timing during hitstun do not move. After hitstun ends, the fighter takes the normal-control step, as it does today. After CM-MOVE (Q5 ruled own-jump-only), a post-hitstun rise is never cut by the short hop, in either mode. CM-MOVE pins that interaction (2.5.1).

*Story hero:*
- Add a `_tumbleAfterHitstun` bool, set at stun expiry while airborne. The hero stays in `Airborne`, so Defy's actionable test starts on time, matching the sim.
- The `ProcessAirborne` landing check, run before roll and jump:
  1. the owed slam bounce first;
  2. then held Block → the tech path (12-frame lock, invulnerable);
  3. otherwise `TransitionTo(Stunned)` and `BeginKnockdown`.

  A buffered jump on that landing does not cancel the knockdown.
- The latch clears:
  - in `TransitionTo` to any state other than Airborne, Stunned or Dead;
  - in `PerformJump`;
  - in `ApplyStun`;
  - on Defy;
  - on death;
  - on a rewind restore.
- It animates as `hitstun`. Mobs are out of scope (`VERIFY-M05-KNOCKDOWN-SCOPE`).

*CPU:* its hitstun-defense Block hold already lasts until grounded, so it techs. If it does not, a one-line edit in `ApplyHitstunDefense` is CM-HIT's declared region in `FighterCpuController.cs` (§3.2).

Sim throws launch and tumble, while Story throws never tech (F23). That divergence is kept and recorded.

**Tests.**
- `FighterTumbleTests` (6):
  1. A long arc whose hitstun ends before landing knocks down if passive, and techs with Block.
  2. Tumble reads 2 in between.
  3. Each exit kind leaves tumble and lands plainly.
  4. Drift and fast-fall keep it.
  5. An owed bounce is still forced.
  6. A depth-7 rollback replays it.
- `StoryTumbleTests` (4):
  1. passive → knockdown;
  2. Block → tech;
  3. the air jump, air dodge, air strike and Special each clear it;
  4. a Defy mid-tumble clears it.
- In place:
  - `DownAirSlamTests.TheBounceNeverRepeatsAndIsDroppedWhenTheTumbleEnds`
  - `FighterKnockdownTests` (the Tumble asserts)
  - `FighterVerbLayerTests.LandingTechEndsTumble…`
  - `FighterCombatContractW3Tests.TheBlockEscapeGateRefusesTumble…`
  - `FighterKnockbackScalingTests` (velocity at touchdown)
  - `CalibrationDrillScriptTests` (the drop-tumble miss)
  - `StoryKnockdownTests`, `StoryCombatContractW3Tests`, `TutorialCalibrationV76Tests`
  - `CpuRecoveryMatrixTests`, `FighterCpuHitstunDefenseTests`, `FighterLedgeTests`
  - the Story elite/boss launch cases in `EnemyControllerTests` and `BossControllerTests`
  - `MirrorParadoxTests`, if it moves.

**Docs and ledger.**
- `USER-TUMBLE-AFTER-HITSTUN` (PFC-04), amending M05/M07.
- Design master :1286–1287, FSM :819–820.
- AGENTS.md: the M05/M07 bullet and the 310 inventory row.
- `systems_card_tech_body` (`en.csv` :953), under `# CM-HIT`.

**Acceptance.** In both modes, a passive launched fighter always resolves its landing (tech or knockdown). Acting out of tumble is clean. The rollback replay is identical.

#### 2.3.2 — 3.4 Perfect block · WS **CM-BLOCK** · both · hash: moves

**Decided.** A block raised within about 4–6 frames before a hit costs no charge and no shieldstun, so the blocker acts at once. There is no reflect and no attacker stun, so Foresight stays the true counter.

**Now.**
- The sim stance is recomputed every tick from Held Block (`IsBlockStance`), with no raise record. Its block branch in `FighterDamageRules.ResolveFighterHit` (≈ :548–599) always spends.
- Story's `CheckBlockInput` enters Blocking on Held. `ProcessBlocking` re-asserts `BlockSystem.StartBlock` each tick, so the stance is live one tick after the press.

**Design** (*Provisional*, `PROVISIONAL-PERFECT-BLOCK-RULES`).

*Shared constants* in `BasicComboRules`, block section:
- `PerfectBlockWindowFrames = 5`: contact ages 0–4 from the first live stance tick.
- `PerfectBlockRearmFrames = 20`: the stance must have been down for at least 20 frames before the raise. This is an anti-mash guard.

*Arming, sim.* The window arms only on a tick where all three hold:
1. **The stance comes up.** It is up this tick and was down last tick; 323 `BlockTiming` bit 16 records the stance each tick.
2. **Block is pressed this tick** (`Pressed & Block`). The Toggle Block toggle-on counts, because `PlayerInputFrame.Create` computes Pressed from the latched Held.
3. **The lowered age is eligible**: 0 ("never raised this stock") or ≥ 20.

So none of these ever arms the window, because none of them is a down → up tick with a press:
- a stance held up through a recovery;
- a stance raised by the hit-2 hitstun escape;
- a stance locked up by shieldstun.

A harness that sets `Pressed = Held = Block` on every tick arms once, on its first stance tick. Component 323 is zeroed on the respawn platform, so a fresh stock is eligible at once.

The lowered age (bits 8–15) is set to 1 when the stance drops and counts up to 255, saturating. The window (bits 0–7) is set to 5 on arming. It decrements on unfrozen ticks (pausing in the fighter's hitstop) and clears when the stance drops. The arming code goes in a new `FighterMovementSystem.Block.cs`, called once after `blockStance` is computed (≈ :545); the decrement goes in `TickCounters`.

*Arming, Story.* `_perfectBlockFrames` and `_blockLoweredFrames` (0 = never raised) live in a new `PlayerController.PerfectBlock.cs`. The window arms on entering Blocking from another state on a tick where Block is pressed and the lowered age is 0 or ≥ 20. It ticks in one call inside `_PhysicsProcess`'s `if (!TimeFrozen)` counter block (≈ :2185), so hitstop's early return (:2135) and Time Freeze both pause it.

*Effect.*
- Every contact the ordinary block layer would absorb inside the window spends no charge, sets no shieldstun and does not re-arm regen.
- It keeps the 2-frame blocked hitstop as feedback.
- The outcome stays `Blocked`, so there is no D1a confirm and no Momentum.
- The D01/D02b order is unchanged: D04 → projectile immunity → Aegis → barrier → (perfect or ordinary) block → HP.

*Exclusions.*
- A Shield-Breaker still shatters (Q3 default, honouring 1.3).
- The Story elite Guard-Crush class keeps its effect.
- The Ultimate activation stays unblockable.
- Hazard ticks can be perfect-blocked.

*Story mechanics.*
- `BlockSystem.ResolveHit` gains a perfect branch and reports it through a `LastBlockWasPerfect` property. No new `BlockResult` value is needed, and every `== Blocked` site stays.
- A perfect block opens the D8 Punish window, because it is a block.
- Henry's Bastion still grants (it is block-triggered).
- Shield of Orléans does not pay; design :2561 requires a consumed charge.

*Feedback,* placeholder in both modes: a bright, brief outline flash through the glow arbiter.
- Fighter: a new `FighterSimulationDriver.SyncPerfectBlockFlash`, read-only over 323, with one call line in `SyncPresentationFeedback`.
- Story: the hero's glow.
- No cue row, because the catalog test ties rows to the GDD.

*CPU.* A reactive Hard CPU (4–8-frame reaction, pulsed Block) will perfect-block often. 9.3 leaves the CPU alone, so this is accepted. CM-CANCEL's smoke records how often it happens (2.9.3), and it goes on the §5 checklist.

*Calibration Drill 1.* Count a perfect block as a block (`CalibrationDrillDefinition.ObserveBlockString`, and the block sample in `CalibrationDrillRunner.Sample`).

**Tests.**
- `FighterPerfectBlockTests` (7):
  1. A raise 0–4 ticks before contact spends nothing and sets no shieldstun; 5+ is ordinary.
  2. A held raise, an escape-raised stance, or a press during shieldstun never arms.
  3. A re-raise inside 20 frames is ordinary.
  4. A `Pressed = Held` stream arms once, and a fresh stock is eligible at once (`PlayerInputFrame.Create`-built and hand-built).
  5. A Shield-Breaker still shatters.
  6. A hazard tick perfect-blocks.
  7. A snapshot replays.
- `StoryPerfectBlockTests` (4):
  1. a mob hit;
  2. the held, escape and Toggle cases;
  3. the Punish window opens;
  4. Guard-Crush and Siphon Snare.
- `CalibrationDrillScriptTests` +1.
- **Audit.** Check every simulation-level block suite for Block pressed within 5 ticks of contact; those now perfect-block. For example, `FighterBasicStringParityTests.ABlockedHitLocksTheBlockerIntoTheStanceForShieldstun` presses Block on the attacker's press tick. Re-time each such fixture in place so it still tests an ordinary block. Report which ones you re-timed. The audit includes `FighterHitPipelineTests`, `StoryDefenceOrderTests`, `CpuVerbPolicyTests` and `FighterCpuBehaviorTests`.

**Docs and ledger.**
- `USER-PERFECT-BLOCK` (PFC-07, FGS-06, CMB-13), amending H-5.
- Design master: block section :1423–1450.
- `DEFENSIVE_EFFECTS.md` D01/D02b: a variant of the ordinary-block layer (both copies).
- `COMBAT_VALIDATION.md`: a perfect-block row in the on-block table (by hand, both copies).
- AGENTS.md: the block bullet.
- `systems_card_block_body` (`en.csv` :947), under `# CM-BLOCK`.

**Acceptance.** The window is 5 frames in both modes, with the stated arming rule. Ordinary blocks are unchanged apart from 4.1. The parity cases are green.

### 2.4 Round 4 — neutral and protection windows

#### 2.4.1 — 4.1 / 11.2 Shieldstun scales with the blocked hit's damage, both modes · WS **CM-BLOCK** · hash: moves

**Decided.** The example shape is 6 + damage / 4. Both modes (11.2). No landing lag.

**Now.** `BasicComboRules.ShieldstunFrames = 8` (:540) has two writers:
- the sim `ResolveFighterHit` non-shatter branch;
- Story's `BlockSystem.ResolveHit` (`ShieldStunSeconds = 8/60`, :45).

**Design** (*Provisional*, `PROVISIONAL-SHIELDSTUN-CURVE`).

`BasicComboRules.ShieldstunFramesFor(int blockedDamage) = clamp(ShieldstunBaseFrames 6 + blockedDamage / ShieldstunDamageDivisor 4, 6, ShieldstunMaxFrames 16)`, with integer division. The cap equals `ShatterFreezeFrames`, so a non-shatter block never locks longer than a shatter freeze.

| Blocked hit | Damage | Shieldstun (frames) |
|---|---|---|
| Basic hits | 4–7 | 7 (one less than today) |
| Lincoln's finisher | 13 | 9 |
| Smite | 28 | 13 |
| The Emancipator | 40 | 16 |
| A blocked cast with no damage (Siphon Snare) | 0 | 6 |
| Any hit, perfect-blocked | — | 0 |

Damage basis:
- **Sim:** the post-barrier remainder before Radiant Burn (an integer).
- **Story:** the delivered damage after partial absorption, so it includes the difficulty multiplier. Hard mob blocks therefore run longer, and that is recorded.

Delete `ShieldstunFrames` and `ShieldStunSeconds`. Every reader takes the function or a test-side expected value: the CPU observation, the drill and the tests.

**Tests.**
- New: `ShieldstunCurveTests` (pure, 2): the table 0, 3, 4, 8, 12, 20, 40, 100 → 6, 6, 7, 8, 9, 11, 16, 16; and the cap.
- `FighterBasicStringParityTests` +1 (the sim value scales).
- `StoryBlockModelTests` +1 (a 20-damage mob hit gives 11).
- In place:
  - `StoryBlockModelTests.ABlockedHitLocksTheBlockerInShieldstunForTheSharedWindow`
  - `FighterBasicStringParityTests` (two cases)
  - `SpecialBlockChargeCostTests`, `DefenceOrderingTests`, `BlockShatterLockoutTests`, `StorySpecialBlockChargeCostTests`
  - `FighterGrabTests` (the whiff window)
  - `SiphonSnareTests`, `StoryDefenceOrderTests`, `ShakespeareKitTests`, `FighterLockoutRefusalRulesTests`, `CpuVerbPolicyTests`, `CalibrationDrillScriptTests`

**Docs and ledger.**
- `USER-SCALED-SHIELDSTUN` (PFC-06, FGS-07).
- Design master :1434, :1089 and :1432 (A01's "ordinary 8-frame").
- `COMBAT_VALIDATION.md` :5 (by hand, both copies).
- AGENTS.md: the block bullet.
- The Systems Card copy.

**Acceptance.** One function, read by both modes. The parity case gives equal frames for equal damage.

#### 2.4.2 — 4.2 Fighter respawn protection: 120 frames shrinking to 60, ended by the first attack · WS **CM-HIT** · Fighter (Q8) · hash: moves

**Decided.** 120 frames, shrinking toward 60 the longer the fighter waits on the platform, and ended early by the first attack or Special. This needs a sim record of why the fighter is invulnerable. Q8 records the reading of "Both".

**Now.**
- `FighterMatchFlowRules.RespawnInvulnerabilityFrames = 180` (:43). On the platform the fighter holds `RespawnFramesRemaining + 180`.
- At the drop, `ProcessRespawnPlatform` (`FighterSimulationSystems.cs:827–859`) computes `elapsed` **before** the decrement (:831) and writes 180 regardless of the wait.
- Invulnerability is one shared, max-assigned `InvulnerabilityFrames` counter. Its sources are the roll, the air dodge, the tech, the knockdown, the throw animation, Foresight and the test seam.
- The driver shows "Respawning" and the spawn glow for **any** invulnerability.

**Design** (*Provisional*, `PROVISIONAL-RESPAWN-PROTECTION-CURVE`).
- `RespawnInvulnerabilityMaxFrames 120`, `RespawnInvulnerabilityMinFrames 60`.
- `RespawnInvulnerabilityFor(int waited) = max(60, 120 − 60 × clamp(waited − 30, 0, 270) / 270)`, integer arithmetic.
- `waited = RespawnPlatformFrames − RespawnFramesRemaining`, read **after** the decrement on the drop tick. The first input drop after the 30-frame grace has waited 31 and gets 120. The 300-frame auto-drop has waited 300 and gets 60. The explicit floor makes 60 exact.
- On the platform the fighter holds `RespawnFramesRemaining + 120`; with no stocks left, 120.
- At the drop, set 323 `ActionFlags.RespawnProtection`. Clear it when `InvulnerabilityFrames` reaches 0.
- When `OnActionAccepted` sees an **offensive** kind (Swing, Special, Ultimate, Grab) with the flag set, it zeroes `InvulnerabilityFrames` and clears the flag. The movement ability, roll, air dodge, jumps, block, Echo Step and the ledge get-ups (`LedgeGetUp`) do not.
- **Why zeroing the shared counter is safe.** Every other source except 5.2's hang is action-locked while it runs: roll, air dodge, tech, knockdown, throw animation and Foresight. 5.2's first-grab intangibility runs while hanging, and a hang can be left by the ledge attack, which is a `Swing`. That case is safe by ordering:
  - Each get-up calls the chokepoint **before** it max-assigns its own climb intangibility. The ledge attack's 8 climb frames therefore survive the zeroing.
  - The hang's remaining first-grab frames do not, because the hang has ended.
  - A roll inside the protection max-assigns as today; a later attack still ends the respawn remainder.
- The driver's "Respawning" label and `SetSpawnInvulnerability` read the flag.

**Tests.**
- `FighterMatchFlowTests` +3: 120 at the first legal drop and exactly 60 at the auto-drop; a swing or Special ends it; a roll does not.
- `FighterPresentationSyncTests` +1: the spawn glow and label show only for the respawn grant.
- In place:
  - `AnyInputAfterTheGraceWindowDropsTheFighterAndArmsTheThreeSecondWindow`
  - `AnExpiredPlatformDropsTheFighterAndArmsTheSameThreeSecondWindow`
  - `TheFighterIsInvulnerableForTheWholeDissolve`
  - `RollInvulnerabilityStaysDistinctFromSpawnInvulnerability`
  - `FighterMatchFlowContentTests.MatchFlowTimingsMatchTheDesignDocument` (pins 3 × TickRate)
  - `GlowPresentationControllerTests`
  - the CPU target-invulnerability cases in `FighterCpuBehaviorTests`

**Docs and ledger.**
- `USER-RESPAWN-PROTECTION` (FGS-14, PFC-19), with Q8.
- Design master :2147–2150, FSM :799, the glow table :3874.
- `FIGHTER_MATCH_RULES.md`, if it restates 3 s (both copies).
- AGENTS.md: the Fighter bullet ("three-second post-drop").

**Acceptance.** The curve and the early end are pinned. The Sudden Death reset clears the flag. Rollback replays it.

#### 2.4.3 — 4.3 Defy protection ends on the first attack or Special · WS **CM-HIT** · both · hash: moves

**Now.**
- `FighterDefenseRules.Tick` counts 60 actionable ticks ("attacking neither cancels nor refreshes it").
- Story keeps `_defyProtectionFrames` (PlayerController ≈ :577). `ClearDefyProtection()` exists (≈ :602).
- D04 (`DEFENSIVE_EFFECTS.md` :238) and design :1323 forbid cancelling.

**Design.**
- **Sim.** `FighterDefenseRules.EndOnOffence` zeroes the await flag, the frames and the proc identity. `OnActionAccepted` calls it for the offensive kinds (the same set as 4.2, so the ledge attack counts).
- **Story.** `ClearDefyProtection()` is called in `ArmSwingLockout`, `ArmAbilityLockout` (Special and Ultimate) and a successful `TryStartGrab`.
- The movement ability, roll, air dodge, jump and block keep it (*Provisional*, as in 4.2).

**Tests.** `DefyProtectedRecoveryTests` +2 (a swing or Special ends it; a roll does not). `StoryDefenceOrderTests` +1.

**Docs and ledger.**
- `USER-DEFY-ENDS-ON-OFFENCE`, amending D04 explicitly.
- `DEFENSIVE_EFFECTS.md` :238 (both copies) and design master :1323.
- AGENTS.md: the D01–D04 bullet.
- The `FighterDefenseRules.Tick` doc comment.

**Acceptance.** In both modes, the protected survivor is hittable from the tick of its first offensive action.

### 2.5 Round 5 — Fighter movement

#### 2.5.1 — 5.1 Sim short hop (×2.5 while rising with Jump released) · WS **CM-MOVE** · Fighter · hash: moves

**Decided.** Add Story's rule to the sim, at about 45–55 % of full height. The prerequisite is 1.1(5). Re-run the CPU recovery matrix on the three Open stages.

**Now.**
- `FighterAirPhysics.StepMovement(vy, fastFalling, floating, jumpedThisAirtime)` has no Jump input.
- Story's `StoryAirPhysics.ShortHopGravityMultiplier = 2.5` (:95) is documented as "Story only".
- Story's `SelectBand` (:158–172) cuts **any** normal-control rise with `_jumpHeld` false. That includes a post-hitstun Airborne rise and a kit lift. Its launch branch has a short-hop clause too (:163), which the sim's `StepLaunch` lacks; that clause is part of `DEFER-H7-STORY-TERMINAL`'s launch band.
- Sim harnesses build frames as `Held = Pressed = buttons` and never set `Released`. The sim copies `Released` from the frame (`FighterSimulationSystems.cs` ≈ :223), and `PlayerInputFrame.Create` computes it (`Released = previousHeld & ~held`). Prediction zeroes `Pressed` and `Released` (`FighterSimulation.PredictPlayerTwo`).

**Design** (*Provisional*, `PROVISIONAL-SIM-SHORT-HOP-LATCH`; Q4 default; Q5 ruled own-jump-only, §0.2).
- Move the multiplier to `UniversalMovementRules.ShortHopGravityMultiplier = 2.5`. `StoryAirPhysics` forwards to it.
- Add an overload `StepMovement(vy, fastFalling, floating, jumpedThisAirtime, bool jumpHeld)`. The old signature means `jumpHeld: true`, so these keep compiling and keep planning full hops:
  - the CPU's arc predictions;
  - `CpuRecoveryProfile`;
  - every test harness;
  - the cast-step overload from A0.

  Since 1.1(5), a full hop is what the CPU does on its own jumps.
- Band order: fast-fall > float > **short hop (vy > 0 && !jumpHeld)** > hang (6.1) > fall > base. `StepLaunch` is unchanged, so launches keep their gravity; launch gravity stays in `DEFER-H7-STORY-TERMINAL`.
- **Scope (Q5 ruled 2026-10-16: own jump only, both modes; §0.2).** The band applies only to a rise that started with this airtime's own Jump press: a ground jump, an air jump or the ledge climb jump. A post-hitstun rise, the Tempest owner lift (`Velocity.y = LiftSpeed` at the zone spawn), an airborne victim of a non-launching hit and every other rise are never cut.
  - **Sim:** 323 `ActionFlags` bit 3 `RiseFromOwnJump` (CM-MOVE). Set when a Jump, AirJump or ledge climb jump is accepted; cleared on landing, on entering hitstun / a launch / a grab or throw, on a ledge grab, when a kit writes the fighter's vertical velocity (the Tempest lift, kit motions), and by the respawn-platform zeroing. The band reads `vy > 0 && RiseFromOwnJump && !jumpHeld`.
  - **Story:** `PlayerController` gains the same latch (a private bool beside `JumpedThisAirtime`, same set/clear points), and `StoryAirPhysics.SelectBand` takes it as a parameter, so Story's band stops cutting post-hitstun and kit-lift rises. Ordinary held and tapped jumps are unchanged, so no level reach guard moves.
  - A CPU that does not hold Jump after a launch is therefore not cut; its planner's full-rise assumption stays true.
- **The latch.** `jumpHeld` reads 323 `ActionFlags.JumpReleasedSincePress` (true means "released").
  - `Released & Jump` sets the bit.
  - `Pressed & Jump` clears it.
  - The respawn-platform zeroing resets it.
  - For any `PlayerInputFrame.Create` stream (the driver, the CPU, the wire), the latch equals "Jump not held", tick for tick.
  - Hand-built frames with no `Released` stay full hops. That avoids churn across about 33 Jump-pressing suites, but it also hides real-input behaviour (§1.8), so the Create-built cases below are mandatory.
  - Input-discard paths zero both edges, so the latch freezes there, which is roughly Story's frozen `_jumpHeld`.
- **The ledge climb jump** (0.9×) follows the same rule, so a tapped climb jumps short. This is recorded. 5.2's neutral climb is the tap-safe exit.
- **Height (Q4).** The decided band is 45–55 % of a held apex. The recon's arithmetic gives about 42–47 % for 1–2-tick taps.
  - CM-MOVE measures 1-, 2- and 3-tick taps for every roster hero, recorded per hero.
  - It does not retune ×2.5, which is decided.
  - Any tap below 45 % goes into `PROVISIONAL-SIM-SHORT-HOP-HEIGHT` for Q4.

**Tests.**
- `FighterShortHopTests` (5, **`[RequireGodotRuntime]`**). Hero jump speeds come from `CharacterData` `.tres` through `FighterLoadoutFactory`, which needs the engine (signature 7). The roster is enumerated through `CharacterRoster`.
  1. Taps of 1, 2 and 3 ticks are monotone and each ≤ 55 % of the held apex, with every value recorded per hero.
  2. A held jump is unchanged, tick for tick.
  3. Launch states (hitstun, daze) never take the band.
  4. A released air jump short-hops.
  5. **Create-built**: with Jump released, a post-hitstun Tumble 2 rise and the Tempest owner lift are **not** cut, while a released rise from the fighter's own jump is cut ×2.5. This pins Q5.
- `FighterMovementFeelTests` +3:
  1. The latch equals a Held reading for a Create-built stream.
  2. A tap/hold script converges under rollback.
  3. The band order.
- `FighterStageGeometryTests` (one of its +2, below): Create-built held jumps escape every pit on Paris, Vesuvius and Nassau for every hero, and a hanging fighter's neutral climb ends standing on each Open stage.
- Re-run `CpuRecoveryMatrixTests` on Paris, Vesuvius and Nassau with no count change, and record survivability.
- Run `FighterTumbleTests` and `StoryTumbleTests` (CM-HIT's) and `ShakespeareKitTests` against the merged result.

**Docs and ledger.**
- `USER-SIM-SHORT-HOP` (PFC-09).
- `PROVISIONAL-SIM-SHORT-HOP-LATCH` (the latch and the `RiseFromOwnJump` set/clear points).
- `USER-SHORT-HOP-OWN-JUMP-ONLY` (Q5, both modes; amends Story's `SelectBand`).
- `PROVISIONAL-SIM-SHORT-HOP-HEIGHT` (Q4, with the per-hero values).
- Close the short-hop half of `DEFER-H7-STORY-TERMINAL`. Story's launch-band short-hop clause stays in its launch half.
- Design master :957, :977, :987, :1464–1467.
- AGENTS.md: the universal-movement and H-7 bullets.

**Acceptance.**
- Taps are measured for every hero. The 45–55 % band is the decided target, and any hero below 45 % is ledgered, not tuned.
- Every existing harness and CPU suite is green.
- The Open-stage recovery matrix is green.

#### 2.5.2 — 5.2 Fighter ledge get-ups and first-grab intangibility · WS **CM-MOVE** · Fighter (Story's `LedgeGrabPoint` later) · hash: moves

**Now.**
- `FighterLedgeRules` (Grab / Process):
  - Jump climbs at 0.9×, on a press (`Process`, ≈ :1697).
  - Down releases on a hold (≈ :1707), with a 30-frame regrab lockout.
  - Auto-release at 300 frames.
  - No invulnerability.
- The hang branch ignores BasicAttack, Roll and the stick; `FighterLedgeTests.HangingSuppresses…` pins this.
- `LedgeStateFrames` counts the hang.
- The CPU answers a hang with `GameplayButtons.Jump` and `moveX = 0` (`FighterCpuController.cs:803`), delivered after its reaction delay. Until then, its recovery steer toward the stage is still being delivered.

**Design** (*Provisional*, `PROVISIONAL-LEDGE-GETUP-FRAMES`).

| Input while hanging | Get-up | Chokepoint kind |
|---|---|---|
| Jump press | the climb jump (unchanged) | `Jump` |
| Roll press (not with Block; never the Echo Step chord) | **Ledge roll**: an 8-frame intangible climb, then the ordinary grounded roll toward the stage (4/12/10, with its 8 invulnerable travel frames) | `LedgeGetUp` |
| BasicAttack press (not with Block; never the grab chord) | **Ledge attack**: an 8-frame intangible climb, then an 8/4/14 strike at 1.0 × `BasicAttackDamage`, with hit 1's box and knockback, 30-frame hitstun, no launch | `Swing` (offensive: it ends respawn and Defy protection) |
| Down held | release (unchanged) | — |
| A **fresh** toward-stage or Up tilt | **Neutral climb**: 20 frames, intangible on frames 1–12, ending standing 0.5 u inside the edge | `LedgeGetUp` |

- **The fresh-tilt trigger.**
  - 323 `ActionFlags.LedgeStickRearmed` is cleared at the grab. It is set on any hang tick whose stick is neither toward the stage nor up (neutral, away or down).
  - The neutral climb fires only on a hang tick where all three hold: `LedgeStateFrames ≥ LedgeGetUpMinHangFrames` (*Provisional*, 6), the bit is set, and the stick is toward the stage or up.
  - So a fighter still steering in from its recovery never neutral-climbs on the first hang tick, and never spends its first-grab intangibility that way. It has to centre the stick and tilt again.
- **Priority on one tick.** Jump press > Roll press > BasicAttack press > Down held > fresh tilt.
  - The CPU's delivered hang decision (Jump, with a neutral stick) therefore always climb-jumps, even on the tick that first centres its stick. **The CPU stays climb-jump only (9.3).**
  - `CpuRecoveryMatrixTests` gains an in-place assertion that no matrix case leaves a ledge by any get-up other than the climb jump. If it fails, CM-MOVE records the case, and the orchestrator decides on a one-line CPU change with CM-CANCEL.
- **First-grab intangibility.** 30 frames, max-assigned into `InvulnerabilityFrames`, when `LedgeGrabsThisAirtime` was 0 before the grab. This is anti-planking: the count resets only on grounding or a stock loss.
- **Ordering with 4.2.** Each get-up calls `OnActionAccepted(kind)` **first**, then max-assigns its own climb intangibility (2.4.2).
- **State.**
  - Universal-movement phases 8 / 9 / 10 in 305, with the anchor packed in `UniversalMovementDirection`; no new bytes.
  - The hang clears when a get-up starts, so trump, the regrab budget and the hit pipeline need no special case.
  - A hit during the vulnerable frames cancels the get-up (`FighterUniversalMovementRules.Cancel`).
  - `IsCombatLocked` includes phases 8–10.
- **The strike.**
  - `AttackFlags` 512 `FlagLedgeAttack` joins `VariantMask`.
  - A `FighterCombatSystem.ApplyLedgeAttackSwing` lives in a new `FighterCombatSystem.LedgeAttack.cs` and is included in the construct sweep.
  - Its constants go in `BasicComboRules`, at the ledge anchor beside `LedgeRegrabsPerAirtime`.
- **Geometry.** `FighterStagePlatform.StandPosition(side)` and `FighterStageGeometry.TryGetStandPosition(anchor, out pos)`, floor-segment anchors included.
- **Presentation.** `ledge_pull_up` (already in the presentation contract), `roll` and `basic_attack_1`. These are the ledge-phase cases in the driver's `ResolvePresentationAnimation`.
- **Move List.** A ledge-options row, under `# CM-MOVE`.

**Tests.**
- `FighterLedgeGetUpTests` (8):
  1. Each option ends on the platform at its frame count.
  2. Each option's intangible frames.
  3. The ledge attack strikes a target at the edge and passes `OnActionAccepted(Swing)`: it ends respawn and Defy protection, and its own climb frames survive.
  4. A first grab is intangible; a regrab is not.
  5. Block+Roll and Block+Attack start nothing.
  6. A hit in the vulnerable frames ends the get-up.
  7. The neutral-climb trigger, with `Create`-built input. A stick held toward the stage from before the grab never climbs; a fresh tilt after 6 frames does; a Jump press on the tick of a fresh tilt climb-jumps.
  8. Rollback through each option.
- In place:
  - `FighterLedgeTests.HangingSuppressesAttacksSpecialsAndBlockAndIgnoresRollAndMovement` (Specials and Block are still suppressed).
  - `FighterStageGeometryTests.EveryAuthoredPitIsEscapableByEveryCharacter`. It holds the stick toward the floor while hanging, but with no fresh tilt, so re-check it.
  - `MoveListScreenTests` (the new ledge row).
  - `CpuRecoveryMatrixTests` (the climb-jump-only assertion).

**Docs and ledger.**
- `USER-FIGHTER-LEDGE-GETUPS` (PFC-12).
- Design master :790 and :994–1003.
- A supersession note in `GAMEPLAY_FEEL_2026-08-10_PLAN.md` §2.11.
- AGENTS.md: the ledge sentence and the 305 notes.
- `CPU_RECOVERY.md` (both copies).

**Acceptance.** All three get-ups, the trigger and the first-grab rule are pinned. Every existing ledge case is green or deliberately rewritten. The CPU never leaves a ledge except by the climb jump.

#### 2.5.3 — 5.3 The air dodge refreshes when hit · WS **CM-HIT** · both · hash: moves

**Now.**
- Sim: `FighterAirDodgeRules.Refresh` runs only on landing, a ledge grab and a stock loss.
- Story: `_airDodgeUsed` is cleared only on landing and a ledge grab (A03, design :829).

**Design** (*Provisional* trigger).
- Any applied hitstun refreshes the air dodge, including throws and ticks that apply hitstun. Blocked, absorbed, armoured and Defy-zeroed hits do not.
- Sim: `if (fighter.HitstunFrames > 0) Refresh(ref knockdown)`, placed before `TickCounters` through a call into the new `FighterMovementSystem.HitReaction.cs`. It is stateless.
- Story: `ApplyStun` sets `_airDodgeUsed = false`.

**Tests.** `FighterAirDodgeTests` +1 and `StoryAirDodgeTests` +1: dodge, get hit, then dodge again after hitstun.

**Docs and ledger.** `USER-AIR-DODGE-REFRESH-ON-HIT`, amending A03. Design master :829 / :996 / :777. `COMBAT_VALIDATION.md` :32 (both copies). AGENTS.md: the universal-movement bullet.

**Acceptance.** Both cases are green.

### 2.6 Round 6 — movement in both modes

#### 2.6.1 — 6.1 Apex hang only while Jump is held · WS **CM-MOVE** · both · hash: moves

**Now.** Story's `StoryAirPhysics.SelectBand` (:158–172) gives the falling half of the band `ApexHang` whatever Jump is doing. The sim's `StepMovement` hangs on `jumpedThisAirtime` alone.

**Design.**
- The hang requires `jumpHeld` in both modes: Story's `_jumpHeld`, and the sim's 5.1 latch.
- A released short hop drops through its peak at ×2.2. Full held jumps keep the hang exactly.
- Every Story content reach helper integrates held arcs (`SimulateJump`, `jumpHeld: true`), so no level guard moves.
- **Bots.** `PlaytestNav.StepFlight` and the NavAgent jump hold release at `vy ≥ 0`. On the final jump of a flight they now hold through the band (`vy ≥ ApexHangSpeedBandPixels`), which keeps their reach. The release before a planned air jump stays.

**Tests.**
- `StoryAirPhysicsTests` +2: `Band(60, held:false, jumped:true) == Fall` and `Band(0, false, true) == Fall`.
- `StoryMovementFeelTests` +1: a released hop has 0 hang ticks.
- `PlaytestFlightModelTests` (new; the first of its 2 cases): a held final jump keeps the full hang, and its predicted landing matches the controller.
- In place: `FighterMovementFeelTests.AJumpHangsAtItsApexButALedgeDropDoesNot` and `ALedgeClimbJumpHangsAtItsApexAfterAWalkOffCapture`. Their frames set no `Released`, so they keep hanging under the latch. Rewrite them only if they are switched to `Create`-built frames.

**Docs and ledger.**
- `USER-APEX-HANG-HELD`, amending `USER-MOVEMENT-FEEL` and `PROVISIONAL-MOVEMENT-FEEL-RULES` (3).
- Design master :956–958 and :978.
- CLAUDE.md: the "How the bots move" text and the comparability note.

**Acceptance.** The released-hop cases are green in both modes. The held-jump numbers are unchanged.

#### 2.6.2 — 6.2 Corner forgiveness · WS **CM-MOVE** · both (Story nudge + lip pop; the sim one-way lip pop, Q6) · Fighter hash: moves

**Decided.** A head-clip nudge of up to about 12 px and a rising lip pop of 8–12 px, stateless (current frame only).

**Now.**
- Story calls a bare `MoveAndSlide()` (`PlayerController.cs:2272`) with no corner correction. One-way platforms already pop a body up by up to 12 px (`OneWayCollisionMargin = 12`, scene-authored).
- The sim has nothing for either rule to act on:
  - walls and the ceiling are full-width;
  - one-way platforms are passable from below;
  - the per-x floor snap already lifts a pit fighter onto a segment from any depth.

**Design** (*Provisional*, `PROVISIONAL-CORNER-FORGIVENESS-RULES`).
- `UniversalMovementRules.HeadClipNudgeUnits = 0.2` (12 px) and `LipPopUnits = 10/60` (10 px).
- **Story.** A new `PlayerController.CornerForgiveness.cs`, called once just before `MoveAndSlide`:
  - It runs only in normal-control airborne states: `Airborne`, an aerial `Attacking`, and the air dodge. Never under launch gravity, `UsingMovementAbility` or `UsingSpecial`.
  - **Head clip.** While rising, it runs a `TestMove` of this tick's vertical motion. If that meets a ceiling (normal.Y > 0.7), it tries horizontal shifts of 1–12 px, nearest side first, and accepts a shift when both the shift and the rise from there are clear.
  - **Lip pop.** While rising into a wall (|normal.X| > 0.7) with input or velocity toward it, it tries lifts of 1–10 px and accepts one when both the lift and the horizontal step from there are clear.
  - The scan runs only on a tick whose first test collides (at most 22 `TestMove`s). `TestMove` respects one-way shapes.
- **Sim (Q6 ruled 2026-10-16: add the Fighter lip pop; §0.2).**
  - **Head clip:** still a documented no-op, because no sim ceiling is partial. A geometry guard pins that premise (no surface a rising fighter can meet from below is anything but a full-width ceiling or a one-way platform), so a future solid underside breaks the guard instead of silently missing the rule.
  - **One-way lip pop** (*Provisional*, `PROVISIONAL-SIM-LIP-POP`): `UniversalMovementRules.LipPopUnits` is the Story pop (10 px = 1/6 u); the sim uses `UniversalMovementRules.SimOneWayLipPopUnits = 0.2` (FP64 static readonly). On a normal-control airborne tick (never launch gravity, hitstun, a cast or a kit motion), if the fighter's feet are below a one-way platform's top by at most 0.2 u, horizontally over the platform, and its vertical velocity is ≤ 0 this tick (the apex or a fall), the fighter is placed on the top and lands through the normal landing path (refreshing jumps and the air dodge). It is stateless and runs in `FighterMovementSystem` before `TryGrabLedge`, so a near-edge capture inside the band becomes a landing; record that.
  - Not applied to floor segments (their per-x snap already lifts from any depth; `VERIFY-SIM-PIT-FLOOR-SNAP` stays separate) or to drop-through (a fighter dropping through a platform this airtime is exempt until it is fully below the band).
  - `FighterCpuController` code is unchanged, but its arc predictions do not model the pop: re-run `CpuRecoveryMatrixTests` on the three Open stages and `FighterLedgeTests`, and record any changed capture or survivability.
  - The pre-existing any-depth pit-floor snap is not changed. Record `VERIFY-SIM-PIT-FLOOR-SNAP` for a separate ruling; it touches `VERIFY-PARIS-DEEP-PIT-RECOVERY`.
- **Gates.** Gate columns and lock barriers are full height with no lip, so they stay uncrossable. Add the lip-pop allowance explicitly to the "no hero can reach" guards, as in-place edits:
  - `TutorialCalibrationContentTests`' gate (240 px against the best single jump, 214 px + 10);
  - `ArenaGeometryContentTests`;
  - the Level 9 and 10 walls.
- **Bots.** `PlaytestNav.StepFlight` models the nudge and the pop from the shared constants (`PlaytestFlightModelTests`, the second case).

**Tests.**
- `StoryCornerForgivenessTests` (4, real frames):
  1. clips of ≤ 12 px slide past;
  2. a 13 px clip bonks;
  3. lips of ≤ 10 px pop, and 11 px does not;
  4. launched bodies get neither.
- `FighterStageGeometryTests` (the other of its +2): the sim head-clip geometry guard above.
- New `FighterLipPopTests` (3, pure C# with hand-built and Create-built frames): a fighter 0.19 u below a one-way top at its apex lands on it, 0.21 u does not; a falling fighter inside the band lands rather than ledge-grabs at a platform edge; a dropping-through fighter is not popped back up, and a pop/no-pop script converges under rollback.
- `PlaytestFlightModelTests` (its second case).

**Docs and ledger.**
- `USER-CORNER-FORGIVENESS` (MOV-05), both modes per Q6.
- `PROVISIONAL-SIM-LIP-POP` (the 0.2 u band, the apex/fall condition, the order before `TryGrabLedge`, the drop-through exemption).
- `VERIFY-SIM-PIT-FLOOR-SNAP`.
- Design master: a new clause in Physics Constants (:950–966) and in One-Way Platforms (:1006–1027): the Story nudge and lip pop, the sim one-way lip pop, and why the sim has no head-clip case.
- CLAUDE.md: the bot movement text.

**Acceptance.** The Story corner cases pass. The sim head-clip guard and `FighterLipPopTests` are green. The Open-stage recovery matrix and the ledge suites are green (any capture that became a landing is recorded). Every level-gate guard is green with the allowance.

#### 2.6.3 — 6.3 Both drop-through inputs in both modes · WS **CM-MOVE** · hash: moves

**Now.**
- Sim: Down+Jump only (`ApplyNormalMovement`, `DropThroughFrames = 30`, vy −2).
- Story: a double tap of Down within `StoryCombatRules.DownDoubleTapFrames = 18` (:8), with `DropThroughFrames = 15`.
- Design :1027 records the M-18 divergence.

**Design.**
- `UniversalMovementRules.DropThroughDoubleTapFrames = 18` (0.3 s, the design value). `StoryCombatRules.DownDoubleTapFrames` forwards to it.
- **Sim.** On a grounded Down press edge on a one-way platform (not the solid base floor), while `allowDropThrough`:
  - if `DownTapFrames > 0`, drop (30 frames, vy −2) and clear it;
  - otherwise set it to 18.

  It decrements after the hitstop gate and resets on the respawn platform. Prediction never fabricates a tap, because predicted frames carry no `Pressed` edges.
- **Story.** In `CheckJumpInput`, a Jump press with Down held on the floor tries `TryDropThrough()` first. On solid ground it stays a jump. A buffered jump that lands with Down held still jumps; this is recorded.
- Each mode keeps its own `DropThroughFrames`.

**Tests.**
- `FighterDropThroughTests` (4, including Create-built input):
  1. A double tap drops; taps 19 frames apart do not.
  2. The main floor and the Sealed base never drop.
  3. Shieldstun refuses it.
  4. Down+Jump still works, and a snapshot taken between taps resumes identically.
- `FeelPassStoryTests` +1: Down+Jump drops from Idle, Running, Crouching and Blocking, and jumps on solid floor.
- Audit harnesses that press Down on consecutive ticks while standing on a platform, and re-time them in place.

**Docs and ledger.**
- `USER-DROP-THROUGH-BOTH-INPUTS` (MOV-06, TWO-11, M-18).
- Design master: delete :1027, and amend :1007, :1012, :1024, :1483–1488 and :2279.
- AGENTS.md: the parity bullet (M-18).
- `tooltip_first_use_one_way`, if it names an input.

**Acceptance.** Both inputs work in both modes, and the M-18 note is gone.

### 2.7 Round 7 — Story combat feel

#### 2.7.1 — 7.1 Ability-hit shake scales with damage · WS **CM-STORY** · Story · hash: no

**Now.** `StoryEnemyCombatRules.DealtHitShakeIntensity(AttackClass, authored, damage, launches)` (:413) returns `authored × 12` for any non-Basic hit with authored > 0. 25 of the 27 Specials and movement abilities author 0.2, which gives 2.4 px. So the Emancipator shakes 2.4 px, against a launching finisher's ≈ 5.4.

**Design.**
- For a Special-class hit whose origin is not Ultimate, the shake is `max(authored × 12, curve(damage, launches))`. The curve is the existing `min(6, 1 + 0.15·d) + 2 if launching`. Duration is `max(authored, curve duration)`.
- Ultimates keep the authored value only. Basics keep the curve. `CameraShake`'s accessibility scale still applies.
- Results: the Emancipator 8 px, Smite 5.2 px, the E=mc² burst 6 px.
- The rule takes a `HitOrigin`. Three callers pass it:
  - `EnemyController.PresentDealtHit`;
  - `BossController.PresentDealtHit`;
  - player-sourced hits on the Level 13 clone: the shake block in `PlayerController.ResolveIncomingHit` (≈ :1837), only when `AttackerIndex ≥ 0`.

  That keeps the rulebook's "the same whichever side" comment true. Enemy hits on the hero are unchanged.

**Tests.**
- `StoryEnemyCombatRulesTests` +1 (pure): a Special of 40 damage that launches gives 8; one of 4 damage gives 2.4; an Ultimate gives 7.2; an Ultimate-origin Special stays authored; the duration rule.
- `StoryEnemyCombatTests` +1: an enemy hurtbox and a boss hurtbox each set the 8 px shake, scaled by the accessibility setting, restoring `CameraShake` in `finally`.
- In place: the wording of `AnAbilitysAuthoredShakeWinsAndBasicsKeepTheDamageCurve`.

**Docs and ledger.** `USER-STORY-ABILITY-SHAKE` (CMB-02). Amend `F9-DEALT-HIT-FEEDBACK`. Design master :2398–2403.

**Acceptance.** No non-Ultimate Special shakes less than a basic of the same damage.

#### 2.7.2 — 7.2 Mercy invulnerability: measure first with a re-hit counter · WS **CM-BOT** · Story diagnostics · hash: no

**Now.** `PlaytestTelemetry.OnPlayerHP` attributes damage to the nearest live enemy. `PlayerHPPayload` carries no source. Boss melee hitboxes carry no `SourceActorId`.

**Design** (*Provisional*).
- Add an observation-only `PlayerController.DamageTaken` event, in a new `PlayerController.DamageObservation.cs`. Its payload is: kind (Hit / Hazard / Status), `SourceActorId`, `AttackID`, `AttackerIndex`, delivery and damage.
- It is raised after damage > 0 in three places, and it never changes a value:
  - `ResolveIncomingHit`, right after `ApplyDamage` (≈ :1698);
  - `ApplyEnvironmentalDamage`;
  - `ApplyPersistentDamage`.
- `BossController` stamps `_attackHitbox.SourceActorIdOverride = GetInstanceId()`. This is gameplay-neutral, because `ReportEnemyAttack` requires an `EnemyController`.
- A pure `RehitTracker` in `PlaytestCombatStats.cs` counts:
  - **cross-source re-hits**: a hit from source S at frame F, with a hit from a different source S′ in [F−30, F);
  - same-source re-hits, as a contrast;
  - a 20-frame figure from the same log;
  - counts by kind (standard, elite, boss, Mirror, projectile, hazard).

  Status ticks are counted under their own kind and kept out of the headline. The log is capped at 500 entries.
- It is read through a seam in `PlaytestWorld`'s "Hero gates" region.
- Outputs:
  - a `rehits` report block;
  - `xsrc_rehits` in `PLAYTEST_RESULT` and in the CSV;
  - a `rehits` table in `compare_runs.py`;
  - a header line in `trace.py`.
- The decision itself waits for the after-batch (§4.3).

**Tests.**
- `PlaytestRehitTrackerTests` (pure, 5).
- `PlaytestCombatStatsTests` +2:
  - two mobs hitting 10 frames apart read cross 1, and the event leaves HP, meter and Rally identical;
  - a boss hit is attributed to the boss.

**Docs and ledger.**
- `USER-STORY-MERCY-MEASURE-FIRST` (CMB-14).
- Amend `USER-BOT-COMBAT-TELEMETRY`.
- CLAUDE.md: the "Since 2026-10-11 the report also carries" list, plus the CSV and compare paragraphs.
- AGENTS.md: the repository map's `scripts/Diagnostics/` line.

**Acceptance.** The counter appears in the after-batch reports. Gameplay is unchanged (the observation-neutral case).

#### 2.7.3 — 7.3 Story Echo Step costs 30 × 0.35 · WS **CM-STORY** · Story · hash: no

**Now.** `PlayerController.TryStartEchoStep` reads `BasicComboRules.EchoStepMeterCost` (30, :704) at its check (≈ :3135) and at its drain (≈ :3155).

**Design** (*Provisional*: 10.5, unrounded).
- `StoryCombatRules.StoryEchoStepMeterCost = BasicComboRules.EchoStepMeterCost × MeterGainScale` = 10.5. It is derived, so there is no second 0.35.
- Add `EchoStepMeterCost(bool storyScaled)`.
- `PlayerController.EchoStepMeterCost => StoryCombatRules.EchoStepMeterCost(AppliesStoryMeterGainScale)`. Only the campaign hero pays 10.5. Everyone else pays 30:
  - the Mirror clone (it earns ×1.0, and its CPU reads 30);
  - the drills and the Holodeck;
  - Fighter;
  - the bots' `--story-meter-scale=off` A/B.
- `InfluenceMeterSourceScanTests` forbids the new name under `scripts/FighterSim/` and in `InfluenceMeterRules`.
- Reword `systems_card_echo_body` (`en.csv` :959, "Spend 30 meter") without the number, under `# CM-STORY`.

**Tests.** `StoryEchoStepCostTests` (3):
- the campaign hero Echo Steps at 10.5, and is refused at 10.4 with nothing spent;
- a flag-false build needs 30;
- a pure equality case.

Plus the in-place scan extension.

**Docs and ledger.**
- `USER-STORY-ECHO-STEP-COST` (CMB-16, TWO-10).
- Amend `USER-STORY-METER-SCALE`, `PROVISIONAL-STORY-METER-SCALE-RULES` (5) and `PROVISIONAL-STORY-METER-GAIN-SCOPE` (2).
- Design master :809, :822, :1271, :1345 (the Story line).
- `COMFORT_SETTINGS.md` :116 (both copies).

**Acceptance.** As tested. Fighter, the CPU, the HUD and the drill stay at 30.

#### 2.7.4 — 7.4 A raised block absorbs Story hazard ticks · WS **CM-BLOCK** · Story · hash: no

**Now.**
- `PlayerController.ApplyEnvironmentalDamage(int, bool)` (:4264) has no block layer.
- `FallingDebris.ApplyTo` calls `BlockSystem.ResolveHit` directly, as Basic class, **before** D04 and Aegis. That ordering is wrong under D01/D02b.
- `BlockRules.ChargeCost(AttackClass.Hazard) = 1` already exists. The sim applies D03c.

**Design** (*Provisional*, `PROVISIONAL-STORY-HAZARD-BLOCK-SOURCES`).
- Add one entry point, `PlayerController.ApplyEnvironmentalHit(int damage, Vector2 hitOrigin, bool blockable, bool appliesHitstop = false)`, in a new `PlayerController.EnvironmentalBlock.cs`. It returns Applied, Blocked or Protected.
- Order:
  1. D04.
  2. Aegis/barrier.
  3. If the hit is blockable, the hero is in Blocking, front-facing relative to the origin's X, and has charges > 0: `BlockSystem.ResolveHit`, with:
     - Hazard class, costing 1 charge;
     - scaled shieldstun by the tick's damage;
     - the perfect block applying;
     - `ExemptFromHitstop` and `AttackerIndex −1`.

     A block deals 0 HP and gives no echo or meter.
  4. Otherwise the existing pipeline.
- **Blockable sources** pass their `GlobalPosition` and skip knockback on Blocked:
  - `StoryCyclicHazard.ApplyToPlayer`;
  - `ChronalExtractor.ApplyDischargeToPlayer` (its −20 meter drain still applies, per D03c);
  - `SearchlightZone.TriggerStrike`;
  - the spikes (`StoryLevelControllerBase.ApplySpikeDamage`, `Level01Controller.BuildHazardSpikes`);
  - `FallingDebris`, which moves onto the entry point (Hazard class, cost 1). That fixes its order.
- **Unblockable sources** are penalties, not hits:
  - `RisingWaterZone` drowning;
  - `Level07Controller.HaulOutOfTheWater`;
  - `ChronalRiftZone.TriggerTimeLoopSnap`;
  - the `EscapeSequenceController` catch;
  - Level 0's scripted kill.

  `StageHazard` is in no scene and stays unblockable; this is recorded.
- Henry's Bastion grants on a hazard block, as debris does today. Shield of Orléans does not.
- The bots already brace with Block (`brace_hazard`, `brace_discharge`), so `damage_by_source` moves. Batches are not comparable across this change.

**Tests.** `StoryEnvironmentalDamageTests` +6:
1. A front block absorbs a cyclic tick for 1 charge, with no HP, knockback, echo or meter.
2. A rear tick lands.
3. Aegis absorbs first, with no charge spent.
4. Defy protection rejects first.
5. Drowning ignores the block.
6. A blocked discharge still drains meter, and debris during Defy spends nothing.

In place: `CollapseTremorTests` (the debris class), `StoryDefenceOrderTests`, and the hazard callers in `EraMechanicToolkitTests` and `PuzzleEnvironmentToolkitTests`.

**Docs and ledger.**
- Close `VERIFY-STORY-HAZARD-BLOCK` with the ruled source list.
- Design master :3937 and :337.
- `DEFENSIVE_EFFECTS.md` D03c, the Story scope (both copies).
- AGENTS.md: the V7.3 environmental bullet, the Collapse Tremor bullet, and "Still held for a user ruling".

**Acceptance.** As tested. The source list matches the ledger row.

### 2.8 Round 8 — tuning and feedback

#### 2.8.1 — 8.1 Story per-hit floor on basics against standards · WS **CM-STORY** · Story · hash: no

**Now.**
- `EnemyController.ResolveIntakeScale` (:2914) multiplies three factors: the S2/D4 chain scale, then S4 (×1.25 for Standard tier and `IsBasicsPacingHit`), then the D8 Punish.
- `ScaleIntakeDamage` rounds once.
- Cleopatra's hits are 4 / 5 / 8, so a Normal 40-HP Chrono-Slasher takes about 8 hits.

**Design** (*Provisional*, `PROVISIONAL-STORY-BASIC-FLOOR-ORDER`).
- `StoryEnemyCombatRules.StandardBasicDamageFloor = 6`.
- `StandardBasicPacingScale(int base) = base ≤ 0 ? 1 : max(1.25, 6 / base)` replaces the bare ×1.25 at the S4 site.
- The floor folds into S4, **before** the chain scale and Punish, with one rounding, so proration still bites: hit 4+ of a chain decays below 6.
- The floor is pre-defense, so frontal shields and the bubble still reduce it.
- Standard tier only. Zero damage stays 0.
- Story difficulty scales mob HP, never damage dealt, so the floor is flat across difficulties.
- Result on a 40-HP slasher: the zoners take 6 hits, Joan and Lincoln 5.
- `ResolveIntakeScale` gains a `baseDamage` parameter, shared with 8.2, so both land in one change.

**Tests.**
- `StoryEnemyCombatRulesTests` +1 (pure): 3 → ×2.0; 8 → ×1.25; 0 → 1; a chain-4 decay of 6 gives 5; Punish gives 7.
- `StoryEnemyCombatTests` +2:
  - a 3-damage `combo_1` deals 6 to a standard, 3 to an elite, and 3 as a `turret_shot`;
  - a per-hero hits-to-kill table, built from the nine `CharacterData` and `StringProfileFor`, pins every hero at ≤ 6 hits on a Normal standard.
- In place:
  - `EnemyControllerTests.NaturallyExpiredStunArmsGetupArmor…` (3 → 6).
  - Every fixture that drives standards with small `StringHit` damage: about 27 calls across `StoryEnemyCombatTests`, `StoryFirstSpecialTaxTests`, `StoryWallBounceTests`, `StoryEnemyCombatReviewTests` and `StoryPunishWindowTests`. Audit each for kills and HP arithmetic.

**Docs and ledger.** `USER-STORY-BASIC-FLOOR` (CMB-03). Amend `PROVISIONAL-BASICS-PACING`. Design master: new Story content beside Enemy Stagger Discipline.

**Acceptance.** As tested. Elites, bosses and the clone are unchanged.

#### 2.8.2 — 8.2 Dim reduced damage numbers · WS **CM-STORY** · Story · hash: no

**Now.**
- `EnemyController.PresentDealtHit` floats white numbers at scale 1. A punished hit floats `GoldBright` at 1.25.
- The proration factor never leaves `ResolveIntakeScale`.
- `FloatingDamageNumber` tints through `font_color`, because `_Process` overwrites `Modulate`.

**Design** (*Provisional*).
- `ResolveIntakeScale` gains `out bool prorated`. It is true when the S2/D4 chain factor is below 1: the ×0.6 second-Special tax, or the ×0.85 decay. An untaxed first Special, and any S4 or floor increase, is never dimmed.
- The dimmed look uses two channels:
  - a new colour, `UIPalette.DamageNumberReduced` (0.66, 0.68, 0.74), kept distinct from `Slate` (the ward's grey "0");
  - a smaller scale, `StoryEnemyCombatRules.ProratedDamageNumberScale = 0.8`.
- A punished and prorated hit shows the Punish treatment.
- No poise-trip flash.

**Tests.** `StoryEnemyCombatTests` +2:
- Special A is white; Special B, confirmed in hitstun, is dim at 0.8; a first Special at chain index ≥ 4 is white; a decayed basic is dim;
- the precedence between the Punish and dimmed treatments.

**Docs and ledger.** `USER-DIM-REDUCED-NUMBERS` (CMB-11). Amend `F9-DEALT-HIT-FEEDBACK` and `PROVISIONAL-STORY-CONFIRM-PRORATION`. Design master :3368–3372.

**Acceptance.** As tested.

#### 2.8.3 — 8.4 One named hurtbox value per mode, no number change · WS **CM-SIM** (sim half, A0 commit 4), **CM-STORY** (Story half) · hash: no (raw-identical)

**Now.**
- Sim: **ten** copies of `new(FP64.FromDouble(0.5), FP64.One)`:
  - the public `FighterKitGeometry.FighterHalfExtents` (`FighterKitRules.cs:15`);
  - `FighterCombatSystem.VictimHalfExtents` (`FighterSimulationSystems.cs:2268`, exposed as `VictimHurtboxHalfWidth`);
  - `FighterUltimateActivationRules.VictimHalfExtents` (:131);
  - `FighterKitMotion.Reach.cs:419`;
  - six private copies in `FighterEntitySystems.cs` (:1387, :1927, :2096, :2287, :2784, :2977), covering the ability, projectile, persistent, hazard, orb and zone systems.
- Story is already named (`StoryCombatRules.HeroHurtboxWidthPixels` / `HeightPixels`). Two derived literals remain:
  - the body box `new Vector2(40, 64)` in `CharacterFactory.cs:168–169`;
  - `PlayerController.GroundStringBoxAnchorYPixels = -32f` (:1320).

**Design.**
- **Sim.**
  - A new `scripts/FighterSim/FighterHurtbox.cs` holds `FighterHurtbox.HalfExtents`, with the identical expression, so it is raw-identical.
  - `FighterKitGeometry.FighterHalfExtents` and `VictimHurtboxHalfWidth` forward to it.
  - The orb-pickup box (:2784) and the hazard overlap box (:2287) are the same fighter box today, but they are a different concept. They get a separately named alias, `FighterBodyOverlap.HalfExtents`, which forwards to `FighterHurtbox.HalfExtents`. A later `VERIFY-SIM-HURTBOX-WIDTH` resize can then split them instead of silently shrinking orb pickup and hazard reach.
  - The other copies are deleted.
- **Story.**
  - The anchor becomes `-HeroHurtboxHeightPixels / 2f`.
  - The body box gets its own name, because it is a collision box, not the hurtbox.

**Tests.**
- `FighterHurtboxSourceScanTests` (pure, 1, uses `System.IO`):
  - the expression appears exactly once under `scripts/FighterSim/`;
  - `FighterBodyOverlap` forwards to it;
  - `VictimHurtboxHalfWidth.RawValue` equals the value's x.
- The Story anchor pin is an in-place assertion in `StoryCombatantGeometryTests`, with no new case.
- The CM-SIM handoff records a seeded replay hash taken before and after commit 4.

**Docs and ledger.** `USER-HURTBOX-ONE-VALUE` (TWO-13). Amend `VERIFY-SIM-HURTBOX-WIDTH` (the copies are merged, and the orb/hazard alias is named; the target size is still open) and `VERIFY-STORY-HURTBOX-UNIVERSAL`.

**Acceptance.** One definition per mode, plus the named alias. The full suite is green with no hash-relative pin moving.

### 2.9 Round 9 — controls and CPU

#### 2.9.1 — 9.1 Last-used device as P1, press-to-join on Fighter select · WS **CM-INPUT** · both · hash: no

**Now.**
- `InputManager` has no `_Input`. It captures every slot in `_PhysicsProcess` (:163) through `CaptureFrame` (:176).
- `ApplyDeviceTopology` (:281) pins the keyboard to slot 0 and auto-fills slot 1 with the first pad, so a lone pad is P2.
- `AutoAssignDevices` (:219) is called only by tests.
- `CharacterSelectScreen` polls `GetFrame(0/1)`.
- The driver samples the CPU directly.

**Design** (*Provisional*, `PROVISIONAL-DEVICE-FOLLOW-RULES`).

*Join modes.* `InputManager` gains `LocalJoinMode { FollowLastUsed (default), Joined }`, `BeginLocalJoin()` and `EndLocalJoin()`.

*Detection by polling, not `_Input`.* InputManager is an autoload, and Godot propagates `_input` in reverse tree order, so the autoload would see an event only after the scene's own handlers. `BootSequenceOverlay` (:305), `SettingsMenu` (:597) and `PauseMenu` (:139) mark events handled. A Pausable autoload also receives no input while the tree is paused. So detection runs inside `CaptureFrame`:
- At the start of slot 0's capture in FollowLastUsed mode, and in Joined mode for unclaimed devices, it compares each device's current input with that device's snapshot from the previous poll:
  - each connected pad: its pressed-button mask through `Input.IsJoyButtonPressed`, and its left stick against that device's `StickProfile` deadzone;
  - the keyboard: `Input.IsPhysicalKeyPressed` over the keys bound in the InputMap, plus mouse buttons.
- A device that shows a **new** press, or a stick crossing out of its deadzone, is the active device. Classification is by device kind, never by `InputEvent.Device`, because Godot gives keys and pad 0 the same index. Mouse motion never counts.
- A test seam, `InputManager.DeviceActivityProbeForTesting`, replaces the poll with a fake.
- The poll costs at most a few dozen engine reads per tick.
- While the tree is paused, the poll runs only when a paused UI calls `GetFrame`/`CaptureFrame`. That is recorded.

*FollowLastUsed.*
- Slot 0 follows the active device (`AssignDevice(device, 0)` on a change), and slot 1 stays unassigned.
- The switch happens before slot 0 is sampled, so the switching press acts as a fresh edge: a Story player who picks up the pad and presses Jump jumps.
- A switch drops the slot's Toggle Block latch, as `AssignDevice` does today.

*Joined.*
- `CharacterSelectScreen` calls `BeginLocalJoin()` in Local Versus.
- The first face, confirm or Interact press on an unclaimed device, keyboard included, claims the lowest free slot (*Provisional*; this amends design :1837). The device's previous-held bits are seeded, so the joining press does not also confirm a tile.
- An override source counts as joined, which keeps the scene tests valid.
- An unjoined P2 drives no token. The P2 panel shows `select_press_to_join` (`# CM-INPUT`, an authored label in `CharacterSelect.tscn`) and refreshes on `DeviceAssigned`.

*Join lifetime.*
- `GameManager.LoadScene` calls `EndLocalJoin()` when the destination is neither the character select nor a Fighter-VS destination (`LoadingScreen.SelectVariant`). Rematch and Back-to-select keep the join.
- Versus CPU, the Holodeck and the drills never join.

*Controller lost.* `PauseMenu.ResolveControllerLost` also resumes on a keyboard press. `LocalFighterPause` keeps its per-slot modal in Joined mode.

*Follow-ons.* Haptics (`ResolveDevice(0)`) and the dialogue glyph (`IsJoypadConnected(0)`) follow the active device for free. `AutoAssignDevices` becomes "reset to FollowLastUsed with the keyboard on 0".

**Tests.**
- `InputDeviceFollowTests` (7). They use the probe seam and `SetConnectedJoypadsForTesting`, and restore everything in `finally`.
  1. A pad press moves slot 0, and a key moves it back.
  2. Mouse motion and sub-deadzone drift do not move it.
  3. Slot 1 stays unassigned with two pads.
  4. The latch drops on a switch.
  5. An override on slot 0 is unaffected.
  6. A press the scene already handled still switches (the poll does not depend on event propagation).
  7. The join lifetime: a load to a stage keeps the join, and a load to the menu or hub ends it.
- `CharacterSelectSceneTests` +3:
  - no P2 token appears until pad B presses, and that press confirms nothing;
  - P1's device cannot take slot 2;
  - the keyboard can join.
- `PauseFocusLossTests` +1: a keyboard key resumes a Story controller loss.
- In place: `HapticDeviceRoutingTests`, `LocalFighterPauseTests` (the three topology cases), `PauseFocusLossTests.ControllerLossPausesStory…`, `StoryPauseTests` (controller loss).

**Docs and ledger.**
- `USER-LAST-USED-DEVICE` (KIT-03).
- Design master :660, :715, :1834–1837, :4048–4053.
- `COMFORT_SETTINGS.md` :134 (by hand, both copies).
- AGENTS.md: the InputManager bullet.
- `MULTIPLAYER_DELIVERY.md` :35, only if its wording changes (both copies).

**Acceptance.** A pad-only player is P1 in Story, in the menus and in Versus CPU. Local Versus joins by press. The controller-lost flows pass.

#### 2.9.2 — 9.2 Pad Ultimate chord leniency and a live chord toggle · WS **CM-INPUT** · both · hash: no

**Now.**
- `InputManager.ReadUltimatePressed` (:542) reads the Ultimate action's own joypad events (`project.godot` 9 + 10) as a conjunction.
- Nothing masks or defers the two components. So LB pressed a frame early casts the movement ability, and the chord inside the lockout casts it too.
- The Ultimate shortcut flag is never read at runtime. Only Grab and Echo Step call `ShortcutEnabled`.

**Design** (*Provisional*, `PROVISIONAL-ULTIMATE-CHORD-LENIENCY`).
- A pure `UltimateChordWindow`, in a new `scripts/Core/UltimateChordWindow.cs` beside the `BlockToggleLatch` pattern. It is stepped per joypad slot in `SampleGodotDevice`, before `PlayerInputFrame.Create`.
- **While the Ultimate is affordable:**
  - A lone LB or RB pressed while its partner is up is masked for **3 frames**.
  - If the partner arrives inside the window, only the Ultimate is held, and both components stay masked until each is released.
  - If the window expires, the component is emitted held, at most 3 frames late.
  - If the component is released inside the window, it is emitted held for exactly one frame, so a tap is never lost.
- **While not affordable, or with the shortcut off:** pass-through, with today's bits.
- **Affordable** means:
  - Story, as reported by `PlayerController` at the end of `_PhysicsProcess` for `!IsStoryHostile`: the meter is full, the Ultimate slot is unlocked, the hero is not Suppressed and not time-frozen.
  - Fighter, as reported by `FighterSimulationDriver` after `Simulation.Advance` for human-sampled slots: Influence ≥ `FighterMeterRules.Cap`, and the fighter is neither caster-busy nor captured.

  The ability lockout is deliberately excluded: inside the lockout the chord presses only the (refused, flashing) Ultimate. A report older than 2 ticks reads as not affordable. The windows reset on pause, focus loss and device reassignment.
- **Chord recognition.** The chord is read from the recipe's **live component bindings** (MovementAbility + Special2), behind `ShortcutEnabled(UltimateAction, Joypad)`.
  - The gamepad direct slot is authored **empty** in `project.godot`; this is C01c's "initially Unbound". "No direct bind" is read as "no default single-button pad Ultimate".
  - C01c capture of a direct pad bind stays legal; this is recorded.
- **Repair.** A saved Ultimate pad half of exactly {9,10} or {4,5} is dropped to the empty default (`InputBindingSet` repair, beside `RepairGodot3ShoulderRows`). The multi-event conjunction stays as a legacy fallback.
- CPU, bot and Mirror sources (`SetInputSource`) bypass all of this.
- The Godot editor writes project settings back over `project.godot` (the CLAUDE.md editor note). The agent never opens the editor.

**Tests.**
- `UltimateChordWindowTests` (pure, 6):
  1. A lone LB is held back exactly 3 frames, then pressed.
  2. RB inside the window gives only the Ultimate, with both masked until released.
  3. A short tap still yields one press.
  4. Not affordable means no delay.
  5. With the toggle off, the components give no Ultimate.
  6. A re-press after the chord is a fresh edge.
- `UltimateChordStoryTests` (1): with a full meter and the lockout running, the chord casts no movement ability.
- `GamepadDefaultBindingTests` +1: an empty pad direct slot, with the components on 9 and 10.
- `GamepadOverrideRepairTests` +1: {9,10} and {4,5} rows are dropped, and the keyboard is kept.
- `InputBindingSchemaTests` +1: with the toggle off and no direct bind, the Ultimate is unreachable and named.
- In place:
  - `GamepadDefaultBindingTests.TheUltimateChordIsBothShouldersAndSpellsItsPresetRecipe`
  - `NoTwoActionsShareADefaultGamepadButtonOutsideTheUltimateChord` (its exception goes)
  - `GamepadOverrideRepairTests.AStaleUltimateChordBesideAReboundKeyMovesOntoTheShoulders`
  - `InputBindingSchemaTests.SaveManagerRepairsAStaleGodot3UltimateChordBeforeApplyingIt`

**Docs and ledger.**
- `USER-PAD-ULTIMATE-CHORD-LENIENCY` (CMB-04, KIT-10).
- Close `VERIFY-GAMEPAD-ULTIMATE-CHORD` (a) and (c).
- Amend `PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION` (1).
- `COMFORT_SETTINGS.md` C01c :106/:114 (by hand, both copies, minding the Unbound lines).
- Design master `:2286` (the Controls table's Ultimate row, "C01c exposes a direct gamepad bind"). This is a `DOC-MASTER-UNBOUND-BINDING` line: edit it by hand in each copy, keeping each copy's own Unbound/Severed wording (§1.1).
- AGENTS.md: the InputManager and C01c bullets.

**Acceptance.** As tested. Keyboard frames are untouched. The Story benefit needs 9.1; both are in CM-INPUT.

#### 2.9.3 — 9.3 Adapt the CPU and the Mirror to the new Special timing only · WS **CM-CANCEL** · Fighter and the Mirror · hash: moves (CPU runs)

**Decided.** Leave the CPU kit-blind. Adapt it only so it stays functional with 1.2, the Level 13 Mirror included.

**Now.**
- The CPU presses on its 3-tick cadence, gated by `SpecialLockoutClearAtDelivery` / `AbilityLockoutClearAtDelivery`.
- Grab and Echo Step admission is rolled once per opportunity (`CanStartGrab`, a mirror of the sim gate at `FighterCpuController.cs:1151`, and `CanStartEchoStep`).
- After 1.2 the sim discards presses made during the CPU's own cast. Two things are then wasted: an admission the CPU consumes, and a Hard Ultimate wait that expires mid-recovery.

**Design.**
- Add `SelfSpecialCastPhase` and `SelfSpecialCastPhaseFrames` to `CpuDecisionObservation` and `CpuVerbState`. They are filled through `FighterSimulationWorldObserver.TryGetVerbState`, from 323, or from 322's phase when the cast is Delegated.
- The two lockout-at-delivery predicates also require the cast to be idle at delivery: phase none, or recovery with frames ≤ the band's minimum reaction delay (Hard 4).
- The CPU's `CanStartGrab` mirror gains the sim's cast and tech terms (2.1.3, 2.1.6). It and `CanStartEchoStep` require phase none.
- This is input-side only. It adds no snapshot state and no new RNG draw. The early-outs shift where the draws fall, so CPU hashes move.
- The Mirror adapter fills the field from the clone's executing `BaseSpecial` (`CurrentPhase`, `PhaseFramesRemaining`), through a read-only accessor in a new `PlayerController.CastObservation.cs`.
- Not in scope:
  - kit awareness (CPU Lincoln's Splitting Strike whiffs and spends);
  - any deliberate use of D3, the ledge get-ups or the perfect block;
  - any change to the press-reading defence. That defence now meets 10–16 frames of startup, so a Hard CPU will block or roll some Specials it never could before. This is recorded.

**Tests.**
- `FighterCpuBehaviorTests` +2:
  - no grab or Echo Step is scheduled during its own cast, and the admission counters do not advance;
  - a full-meter Hard Ultimate arrives only after its own cast.
- `MirrorParadoxTests` +2: the adapter projects startup, active and recovery, and 0 when idle; the projection is lossless.
- `FighterCpuSpecialTimingSmokeTests` (1): a seeded Hard-vs-Hard CPU match on each of the ten stages for 1,800 ticks.
  - It asserts: each fighter casts at least one Special through Active; no cast exceeds S+A+R; there is no exception; a replay is identical.
  - It also **records, without asserting**, per stage: perfect blocks, Specials blocked or evaded during their startup, and Lincoln Splitting whiffs. The handoff copies these into §5 rows 9, 11 and 25.
  - This is the package's CPU smoke (§4). It is not a measurement batch (9.4).
- Re-run `CpuRecoveryMatrixTests` and `CpuMeleeBandTests`.

**Docs and ledger.**
- `USER-CPU-SPECIAL-TIMING-ONLY` (KIT-04/05/06).
- Amend `PROVISIONAL-HIT-CONFIRM-RULES` (7) and `PROVISIONAL-ABILITY-LOCKOUT-RULES` (6).
- `CPU_COMBAT_POLICY.md`: cast legality is self-state (both copies).

**Acceptance.** Every CPU and Mirror suite is green. The smoke passes on all ten stages, and its observations are in the handoff.

### 2.10 Round 10 — remaining rules

#### 2.10.1 — 10.1 Latch the sim box facing at active start · WS **CM-SIM** (A0) · Fighter · hash: moves

**Now.**
- `FighterCombatSystem.ResolveStringBox` (:2298) reads `attacker.FacingRight` on every active tick.
- It has three callers: the construct sweep (≈ :2792), the chain (≈ :2871) and the air strike (≈ :3011).
- Facing follows the stick mid-swing (`VERIFY-SIM-STRING-FACING-PER-TICK`).
- Story places its box once, at active start (design :1046).

**Design.**
- Add `FighterBasicAttackRules.FlagActiveFacingRight = 256`. It is set or cleared from `FacingRight` on every Startup → Active transition, chain continuations included.
- A `ResolveStringBox` overload takes the latched facing at the three callers. The old overload stays for `StringHitboxGeometryTests` and `CpuMeleeBandTests`.
- Latch the Up-Attack and Down-Air boxes too, if they offset by facing (CM-SIM checks).
- Steering and the sprite still turn. `StartSwing` resets the flags, so the bit never leaks.

**Tests.** `FighterBasicStringParityTests` +1: reversing on the first active tick does not flip the box for the chain, the air strike or the construct sweep, and a new chain hit re-latches.

**Docs and ledger.** Close `VERIFY-SIM-STRING-FACING-PER-TICK`. AGENTS.md: the 305 row and the parity bullet.

**Acceptance.** As tested. `GroundedSwingKeepsSteeringAndFacingFollowsTheHeldDirectionMidString` stays green.

### 2.11 Follow-ups

#### 2.11.1 — 11.1 D3 in Fighter: one rule in both modes · WS **CM-CANCEL** · hash: moves

**Decided.** A whiff keeps its full recovery. A Special that landed a direct hit cancels into a roll or jump from recovery frame 0, and into a basic or the movement ability from frame 6.

**Now.**
- Story: `BufferSpecialCancelPress` / `TryFireSpecialCancel` (PlayerController ≈ :3283–3395). The constants are in `StoryCombatRules.cs:72–99` (0 / 6 / 6). The landed flag is `BaseSpecial.LandedDirectHitThisExecution`.
- Sim: nothing.
  - Sim hit sites report only `ApplyFighterHit == Landed`, although `ResolveFighterHit` returns Refused / Blocked / Absorbed / Landed.
  - 322's sidestep is fully action-locked.

**Design** (*Provisional* details, `PROVISIONAL-FIGHTER-SPECIAL-RECOVERY-CANCEL`).

*The table.*
- Move the constants and predicates to `BasicComboRules`, in a new D3 section anchored after the ability-lockout region: `SpecialRecoveryEvadeCancelFrame 0`, `SpecialRecoveryActionCancelFrame 6`, `SpecialCancelBufferFrames 6`, `SpecialRecoveryAllowsEvade/Action` and `SpecialCancelBufferHolds`.
- `StoryCombatRules` forwards to them, so Story is unchanged and `StoryCombatRulesTests` stays green.
- Add the source scan: nothing under `scripts/FighterSim/` names `StoryCombatRules` or `StoryAirPhysics`.

*Arming.* A hit site sets 323 `LandedDirectHit` when all of these hold:
- the outcome is Landed or Absorbed, with damage > 0;
- the hit's slot equals the running cast's slot;
- the cast is busy;
- the slot's projected `FighterAbilityHitData.Delivery` is DirectHit.

So the zone pulses that Story treats as direct hits arm it identically: Golden Ratio, Lorentz and the Requiem burst. The Rift, the Vortex, the Tempest, constructs and Cleopatra never arm it.

These sites switch to `ResolveFighterHit` + `NoteLanded`:
- the Piercing thrust;
- the projectile and ground-wave contact (not construct bolts, and not Ultimate-origin hits);
- the zone pulse;
- the `ProjectileBurst`;
- `LorentzChain.FireArcs`;
- the Foresight answer;
- the melee `ApplyIntent`.

Slot attribution is safe because no Special projectile outlives its cooldown minus the 120-frame Momentum cap. A test pins this, instead of growing 302/309.

*Buffer* (323 `SpecialCancelBuffer`).
- One slot. It ages on unfrozen ticks and is valid for 6.
- It is fed on hitstop ticks without aging, by one feed call in the hitstop branch. That call goes **before** CM-SIM's busy-caster discard, so the press is captured before the input is zeroed.
- It is never fed by the Echo Step chord's Roll, by the grab chord, or by a Movement press on an Ultimate-pressed tick.
- Up/Down held at the press are stored in bits 7/8.

*Order in recovery.* The cast region exits that CM-SIM built: grounded held Block first, then D3.

*On fire.*
- Rewrite `PressedButtons` to the verb's bit only (strip S1/S2/Ultimate/Grab/EchoStep), and keep the stick and the held Jump/Down.
- For a basic, overwrite `MoveY` from the buffered Up/Down bits (Up → up, Down → down, neither → 0) before the swing starts. `SelectAttackVariant` then picks the Up-Attack, the Down-Air or the neutral strike from the press-time direction, so a held direction never eats the press. CM-CANCEL does not edit `FighterBasicAttackRules`.
- Clear the cast.
- The verb starts through its normal path: roll or air dodge, jump, the `SelectAttackVariant` swing, or `ApplyMovement`. Each passes `OnActionAccepted`.
- Cooldown, lockout and spawned objects stand.
- Special → Special stays lockout-gated, because neither is polled in recovery. No Echo Step comes out of a Special (A06).

*Foresight,* matching Story (`TubmanForesight`):
- its whiff recovery and its sidestep gain the grounded Block cancel;
- a landed answer arms D3, with recovery frame = `ForesightSidestepFrames − PhaseFrames`;
- a cancel ends the sidestep and cuts only the sidestep's remaining `InvulnerabilityFrames`, never a respawn or tech grant.

The freeze-only Special buffer (305 bits 17–19) and the D3 buffer must never fire the same press twice. A test pins this.

**Tests.**
- `FighterSpecialRecoveryCancelTests` (12), the sim twin of `StorySpecialRecoveryCancelTests`:
  1. Each verb fires only after a landed direct hit, and only from its frame.
  2. A whiff or a block keeps its recovery, with Block as the only exit.
  3. Ticks, constructs, zones and zero-damage hits never arm it; an Aegis-absorbed contact does.
  4. An earlier cast's projectile never arms it.
  5. No Special, Ultimate or Echo Step comes out.
  6. The Up/Down variant.
  7. The Ultimate chord never takes the movement cancel.
  8. The buffer holds for 6, and hitstop does not age it.
  9. Foresight's sidestep cancel cuts only its invulnerability.
  10. The freeze buffer and D3 never double-fire.
  11. Rollback across a cancel.
  12. The projectile-lifetime invariant.
- `SpecialRecoveryCancelTableTests` (pure, 1): the `BasicComboRules` values equal 0/6/6, `StoryCombatRules` forwards, and the source scan.
- In place: `TubmanKitTests.AWhiffedForesightLocksHerThroughTheStanceAndArmsTheTenSecondCooldown` and `ForesightNullifiesAStringHit…` (the Block cancel).

**Docs and ledger.**
- `USER-FIGHTER-SPECIAL-RECOVERY-CANCEL`, amending `USER-SPECIAL-RECOVERY-CANCEL` and `PROVISIONAL-SPECIAL-RECOVERY-CANCEL-RULES`.
- Design master :783–786 and :1500, both modes.
- AGENTS.md: the D3 bullet.

**Acceptance.** One table and one rule in both modes. All Story D3 cases are green and unchanged.

### 2.12 Not in this package (decided "keep", "judge by hand" or folded)

| # | One line | Where it goes |
|---|---|---|
| 1.4 | No combo damage scaling in Fighter; nothing under `scripts/FighterSim/` gains proration. | Ledger record `USER-FIGHTER-NO-COMBO-SCALING` |
| 2.2 | The D15 Ultimate totals are unchanged; the pad session decides (9.4 removed the CPU batch). | §5; amend `VERIFY-ULT-ACTIVATION-TUNING` |
| 3.1 | The held-Block landing tech is kept (V7 pillar). 1.1(3) locks its frames, and 3.2 extends it to post-hitstun tumble. | — |
| 3.3 | Walls pin a launched fighter. Kept; the pad session looks for wall loops (no enumerator, 9.4). | §5; the new row `VERIFY-WALL-PIN` |
| 4.4 | No counter-hit or punish reward in Fighter. | Ledger record `USER-FIGHTER-NO-COUNTER-HIT` |
| 5.4 | The −20 % speed cut stays in Fighter; judge 1v1 pace. | §5; amend `USER-MOVEMENT-SPEED-CUT` |
| 6.4 | Both builds' jump spreads stay (the 2026-10-09 ruling). | — |
| 8.3 | Hitstop 4–11 stays; judge by hand. | §5; amend `PROVISIONAL-HITSTOP-WEIGHT` |
| 9.4 | Pad session only: no frame tables, combo enumerator or CPU-vs-CPU batch. 7.2's counter and 9.3's smoke are functional, not measurement tools. | §5 |
| 10.2 | Sudden Death keeps F22. | §5 |
| 10.3 | The single air strike stays; judge the air game with the new short hop. | §5 |
| 10.4 | The Fighter comeback stack (orb heals, Rally, Defy, missing-HP knockback) is judged by hand. | §5 |
| 11.2 | Folded into 4.1 (both modes). | §2.4.1 |
| 11.3 | This plan and its phased workstreams. | §3 |

---

## 3. Phases, workstreams and file ownership

### Order of work

1. **Questions** (§0.1) — all eight ruled on 2026-10-16 (§0.2).
2. **Plan commit** on `main`.
3. **Before bot batch** in the clean bot worktree, at the plan commit (§4.3).
4. **Phase A.** Five parallel worktrees branch from the plan commit. CM-SIM's first five commits are the **Phase A0** foundation, tagged `cm-sim-a0` (§3.3).
   - Merge in this order: **CM-BOT → CM-STORY → CM-ULT → CM-INPUT → `cm-sim-a0` → CM-SIM (the rest)**.
   - CM-SIM rebases onto the merged CM-BOT..CM-INPUT result before its final full run. CM-INPUT's Jump sustain moves CPU-driven hashes, and CM-SIM's CPU re-timing must be checked against it.
   - After each merge the orchestrator resolves `en.en.translation` (§1.13), runs `--import`, and runs one full suite.
5. **Review round A1** (two refuting verifiers per finding) on CM-BOT..CM-INPUT + A0, with a fix round if needed.
6. **Phase B, early start.** CM-HIT, CM-BLOCK and CM-MOVE may branch from the reviewed A0 merge, so a CM-SIM overrun stalls only CM-CANCEL.
   - Before its own merge, each of these branches rebases onto `main` with the full CM-SIM merged.
   - It then re-times anything the phase machine moved, and re-runs its filtered and full suites.
   - If CM-SIM finishes before Phase B would start, all four Phase B branches start together from the full Phase A merge. That is the default.
7. **Review round A2** on the rest of CM-SIM (the phase machine), with a fix round if needed. **CM-CANCEL** branches from the reviewed full Phase A merge.
8. **Phase B merge order: CM-HIT → CM-BLOCK → CM-MOVE → CM-CANCEL.** Then a review round on the Phase B diff.
9. **Phase C:** a serial closeout in the main checkout (§7).

CM-SIM merges last in Phase A because its diff is the largest, and its few shared hunks (the driver, one `PlayerController` line) are easiest to resolve against the small branches. CM-CANCEL merges last in Phase B because it touches the most hit sites and the CPU.

### 3.1 Phase A

| WS | Decisions | Owns (may edit) | May touch additively (declared region only) | Must not edit | Declared delta |
|---|---|---|---|---|---|
| **CM-SIM** — sim foundation (A0) and the Special phase machine | 1.2, 1.1(2), 1.3, 1.1(3), 2.3, 10.1, 8.4 (sim) | New: `FighterSpecialCast.cs` (component **323**, `FighterSpecialCastRules`), `FighterActionRules.cs` (the chokepoint, empty body), `FighterHurtbox.cs` (+ `FighterBodyOverlap`), and the split files (`FighterLedgeRules.cs`, `FighterUniversalMovement.cs`, `FighterBasicAttackRules.cs`, `FighterAbilityLockoutRules.cs`). **All of** `FighterSimulationSystems.cs`, `FighterEntitySystems.cs`, `FighterSimulationComponents.cs`, `FighterLoadoutFactory.cs` and `FighterSimulation.cs`. Also `FighterKitMotion.Reach.cs`, `FighterKitRules.cs`, `FighterGrabRules.cs` (`CanStartGrab`), `FighterAirPhysics.cs` (the cast-step overload), `FighterUltimateActivation.cs` (the hurtbox line), `FighterCounter.cs` (the Delegated marker) and `FighterLockoutRefusalRules.cs`. Every sim test it re-times. | `FighterSimulationDriver.cs` (`ResolveAbilityPresentation` only). `scripts/Core/UniversalMovementRules.cs` (the `AerialSpecialGravityMultiplier` anchor, beside `ApexHangGravityMultiplier`). `PlayerController.ProcessUsingSpecial` (one forwarding line replacing the `0.5f` at ≈ :3225, and the Q1 grounded-brake call beside it). | Every other `PlayerController` line, `InputManager`, `FighterCpuController` (code), `BasicComboRules`, any other Story file | **+45** = A0 **+12** (`FighterActionStateTests` 4, `FighterHurtboxSourceScanTests` 1, `FighterVerbLayerTests` +3, `UltimateActivationTests` +2, `StoryAbilityLockoutTests` +1, `FighterBasicStringParityTests` +1) + phase machine **+33** (`StorySpecialTimingProbeTests` 2, `StorySpecialCastMotionTests` 2 (Q1), `FighterSpecialCastRulesTests` 6, `FighterSpecialTimingTests` 14, `ShieldBreakerTimingTests` 3, `SplittingStrikeSpikeTests` +2, `DivinePiercingLungeTests` +1, `FeelPassSimTests` +1, `FighterLockoutRefusalRulesTests` +1, `RollbackReadinessTests` +1) |
| **CM-STORY** — Story combat feel | 1.1(1), 2.4, 7.1, 7.3, 8.1, 8.2, 8.4 (Story) | `BaseSpecial.cs` (`SuspendsOwnerGravityNow`); the movement classes in `scripts/Characters/Abilities/`; `StoryCombatRules.cs` (the Echo Step section and `UltimateStartupArmorPreventsInterruption`); `StoryEnemyCombatRules.cs`; `FloatingDamageNumber.cs`; `UIPalette.cs` (one colour); `CharacterFactory.cs` (the body box name); `InfluenceMeterSourceScanTests.cs`; `en.csv` `# CM-STORY`. The nine `ultimate.tres` stay unchanged (Q2 ruled startup only). `LincolnRailCharge` `OnRecovery` (Q7). | `PlayerController.cs`: `ApplyGravity` (the optional `ignoreShortHop` parameter), `ProcessUsingMovementAbility`, `TryStartEchoStep`, `HasActiveHyperArmor`/`HasActiveHyperArmorAgainst`, and in `ResolveIncomingHit` the status application (≈ :1799) and the shake block (≈ :1837); `GroundStringBoxAnchorYPixels`; new partials. `EnemyController.cs`: `ResolveIntakeScale`, its call in `OnHurtboxHit`, `PresentDealtHit`. `BossController.cs`: `PresentDealtHit`. | sim files, `InputManager` | **+21**: `StoryMovementAbilityGravityTests` 6 (incl. Q7), `StoryUltimateArmorTests` 5, `StoryEnemyCombatRulesTests` +2, `StoryEnemyCombatTests` +5, `StoryEchoStepCostTests` 3 |
| **CM-ULT** — the Ultimate telegraph | 2.1 | `TelegraphGlyph.cs` (moved to `scripts/Combat/` with its `.uid`); `CoreMechanicCueCatalog.cs`; `core_mechanic_cues.tres`; `CoreMechanicCuePresenter.cs` (if needed); `GlowPresentationController.cs` (only a dedicated telegraph flag, if chosen) | `FighterSimulationDriver.cs`: `SyncPresentationFeedback`, `SyncCoreMechanicCues`, and a new glyph attach. `PlayerController.cs`: the Ultimate activation members (`BeginUltimateActivationStrike`, `ProcessUltimateActivationStrike`, `StartUltimateFromActivation`, `EndUltimateActivationStrike`) and one exit block in `TransitionTo`; new `PlayerController.UltimateTelegraph.cs`. `EnemyAbilityExecutor.cs`: colour reads only. | sim rule files | **+7**: `FighterPresentationSyncTests` +3, `StoryUltimateActivationTests` +3, `CoreMechanicCueCatalogTests` +1 |
| **CM-INPUT** — devices, chord, CPU jump hold | 9.1, 9.2, 1.1(5) | `InputManager.cs`; new `UltimateChordWindow.cs`; `InputBindings.cs`; `project.godot` (the `gameplay_ultimate` joypad events only); `CharacterSelectScreen.cs` and `CharacterSelect.tscn` (the join prompt); `PauseMenu.cs` and `LocalFighterPause.cs` (controller-lost); `GameManager.cs` (the join-lifetime line in `LoadScene`); `FighterCpuController.cs` (the Jump sustain only); `en.csv` `# CM-INPUT` | `PlayerController.cs`: one readiness line at the end of `_PhysicsProcess`, plus a new `PlayerController.InputReadiness.cs`. `FighterSimulationDriver.cs`: one readiness push after `Simulation.Advance`. `MirrorParadoxController.cs`: the `MirrorParadoxDecisionAdapter` constructor overload only. | sim rule files | **+24**: `UltimateChordWindowTests` 6, `UltimateChordStoryTests` 1, `InputDeviceFollowTests` 7, `CharacterSelectSceneTests` +3, `PauseFocusLossTests` +1, `GamepadDefaultBindingTests` +1, `GamepadOverrideRepairTests` +1, `InputBindingSchemaTests` +1, `MirrorParadoxTests` +1, `FighterCpuBehaviorTests` +2 |
| **CM-BOT** — the re-hit counter | 7.2 | `scripts/Diagnostics/PlaytestTelemetry.cs`, `PlaytestCombatStats.cs`, `PlaytestWorld.cs` (the "Hero gates" seam); `tools/playtest/*.py`, `run_playtests.ps1`; new `PlayerController.DamageObservation.cs` | `PlayerController.cs`: one raise line each in `ResolveIncomingHit` (after `ApplyDamage`, ≈ :1698), `ApplyEnvironmentalDamage` and `ApplyPersistentDamage`. `BossController.cs`: one `SourceActorIdOverride` stamp. | gameplay rules | **+7**: `PlaytestRehitTrackerTests` 5, `PlaytestCombatStatsTests` +2 |

**Phase A total: +104** (declared, after the §0.2 rulings), so a merged target of **3332**. Each handoff reconciles its exact figure.

#### Phase A shared-file map

Code:
- `PlayerController.cs`: five workstreams, each in the regions above, with bodies in new partial files. `ResolveIncomingHit` alone has three editors at distinct lines: CM-BOT at ≈ :1698, and CM-STORY at ≈ :1799 and ≈ :1837. `ProcessUsingSpecial`'s one line belongs to CM-SIM.
- `FighterSimulationDriver.cs`: CM-SIM, CM-ULT and CM-INPUT, in three distinct methods.
- `BossController.cs`: CM-STORY (`PresentDealtHit`) and CM-BOT (the stamp).
- `MirrorParadoxController.cs`: CM-INPUT only (the constructor overload).
- `en.csv`: CM-STORY and CM-INPUT, in separate `# CM-<id>` blocks. For `en.en.translation`, see §1.13.

Test files (union merge; the later branch resolves):
- `FighterPresentationSyncTests`: CM-ULT +3; CM-SIM rewrites `AcceptedFighterSpecialHoldsTheAuthoredAbilityPose` in place.
- `FighterCpuBehaviorTests` and `CpuRecoveryMatrixTests`: CM-INPUT +2 and a re-run; CM-SIM re-times in place, after its rebase.
- `MirrorParadoxTests`: CM-INPUT +1; CM-STORY in place if 2.4 moves a case; CM-SIM re-times Special cases.
- `FighterBasicStringParityTests`: CM-SIM +1 (and CM-BLOCK again in Phase B).
- `EnemyAbilityExecutorTests`: CM-ULT in place only.

### 3.2 Phase B

Phase A's files are **read-only** in Phase B, except where a row below names them. A Phase A defect goes in the handoff with a repro, and the orchestrator fixes it.

In `FighterMovementSystem.Update` and in `PlayerController`, Phase B edits are **call lines at the declared sites only**. Bodies go in the workstream's own partial file.

| WS | Decisions | Owns | Declared call sites / regions in shared files | Declared delta |
|---|---|---|---|---|
| **CM-HIT** — hit reaction and protection | 3.2, 5.3, 4.2, 4.3, 1.1(4) | `FighterActionRules.cs` (fills the body); `FighterVerbRules.cs` (tech, knockdown and slam keyed on Tumble); new `FighterMovementSystem.HitReaction.cs`; `FighterMatchFlowRules.cs`; `FighterSimulationRules.ApplyStockLoss` (the respawn constants); new `PlayerController.Tumble.cs`; `en.csv` `# CM-HIT` | `FighterMovementSystem.Update`: the hitstun-end block (≈ :330–337), one refresh call before `TickCounters`, and `ProcessRespawnPlatform`. `FighterEntitySystems.cs`: `ApplyUnattributedDamage`, the Defy condition lines in `ResolveFighterHit` (≈ :614–622), and `FighterDefenseRules` (`EndOnOffence`, `CanDefy`). `FighterSimulationDriver.cs`: the Respawning line in `GetStateLabel` (:380–396) and the tumble case in `ResolvePresentationAnimation`. `PlayerController.cs`: the `ProcessStunned` expiry, the `ProcessAirborne` landing check, `ApplyStun`, `PerformJump`, the `TransitionTo` latch clear, and one line each in `ArmSwingLockout`, `ArmAbilityLockout` and `TryStartGrab`. `CalibrationDrillRunner.Sample` (:240–262): the tumble line. `FighterCpuController.ApplyHitstunDefense`: one line, only if needed. | **+20**: `FighterTumbleTests` 6, `StoryTumbleTests` 4, `FighterAirDodgeTests` +1, `StoryAirDodgeTests` +1, `FighterMatchFlowTests` +3, `FighterPresentationSyncTests` +1, `DefyProtectedRecoveryTests` +2, `StoryDefenceOrderTests` +1, `FighterHitPipelineTests` +1 |
| **CM-BLOCK** — block | 3.4, 4.1/11.2, 7.4 | New `FighterMovementSystem.Block.cs`; `BlockSystem.cs`; new `PlayerController.PerfectBlock.cs` and `PlayerController.EnvironmentalBlock.cs`; `StoryCyclicHazard.cs`, `ChronalExtractor.cs`, `SearchlightZone.cs`, `FallingDebris.cs`; `CalibrationDrillDefinition.cs`; `en.csv` `# CM-BLOCK` | `BasicComboRules.cs`: the block section (beside `ShieldstunFrames`, ≈ :532–553). `FighterEntitySystems.cs`: `ResolveFighterHit`'s block branch (≈ :548–599) and `ApplyEnvironmentHit`. `FighterMovementSystem.Update`: one call after `blockStance` is computed, and one in `TickCounters`. `FighterSimulationDriver.cs`: a new `SyncPerfectBlockFlash` and one call line in `SyncPresentationFeedback`. `PlayerController.cs`: `CheckBlockInput`, `ProcessBlocking`, `ResolveIncomingHit`'s block branch (≈ :1660–1695), `ApplyEnvironmentalDamage`, `TryAbsorbNonDamagingCast`, and one tick call inside `_PhysicsProcess`'s `if (!TimeFrozen)` block (≈ :2185). `StoryLevelControllerBase.cs` and `Level01Controller.cs`: the spike methods only. `EnemyAbilityExecutor.cs` (≈ :375–379). `CalibrationDrillRunner.Sample`: the block line. | **+22**: `ShieldstunCurveTests` 2, `FighterBasicStringParityTests` +1, `StoryBlockModelTests` +1, `FighterPerfectBlockTests` 7, `StoryPerfectBlockTests` 4, `StoryEnvironmentalDamageTests` +6, `CalibrationDrillScriptTests` +1 |
| **CM-MOVE** — movement | 5.1, 6.1, 6.2, 6.3, 5.2 | `FighterAirPhysics.cs`; `StoryAirPhysics.cs` (incl. the Q5 `SelectBand` gate); `scripts/Core/UniversalMovementRules.cs`; `FighterLedgeRules.cs`; `FighterUniversalMovement.cs`; new `FighterMovementSystem.Movement.cs` and `FighterCombatSystem.LedgeAttack.cs`; `FighterStageGeometry.cs`; new `PlayerController.CornerForgiveness.cs`; `scripts/Diagnostics/PlaytestNav.cs`; `MoveListScreen.cs` (the ledge row); `en.csv` `# CM-MOVE` | `FighterMovementSystem.Update`: the gravity call (≈ :637), the drop-through in `ApplyNormalMovement`, the `ProcessUniversalMovement` cases 8–10, and the latch and Down-tap updates. `FighterBasicAttackRules.cs`: the ledge variant and `VariantMask`. `BasicComboRules.cs`: the ledge section (beside `LedgeRegrabsPerAirtime`). `StoryCombatRules.DownDoubleTapFrames` (:8, the forwarder). `PlayerController.cs`: one call immediately before `MoveAndSlide` (≈ :2272), and the Down+Jump branch in `CheckJumpInput`. `FighterSimulationDriver.cs`: the ledge-phase cases in `ResolvePresentationAnimation`. Content tests' reach guards (the allowance lines). **Not** `FighterCpuController` (the Q6 lip pop changes no CPU code; the matrix re-run measures it). The lip pop's single call sits in `FighterMovementSystem.Update` before `TryGrabLedge`. | **+36**: `FighterShortHopTests` 5, `FighterMovementFeelTests` +3, `StoryAirPhysicsTests` +3 (incl. the Q5 own-jump gate), `StoryMovementFeelTests` +1, `FighterLipPopTests` 3 (Q6), `StoryCornerForgivenessTests` 4, `FighterStageGeometryTests` +2, `FighterDropThroughTests` 4, `FeelPassStoryTests` +1, `FighterLedgeGetUpTests` 8, `PlaytestFlightModelTests` 2 |
| **CM-CANCEL** — D3 in Fighter, CPU and Mirror | 11.1, 9.3 | `FighterSpecialCast.cs` (buffer, `NoteLanded`, `TryFireCancel`); `FighterCounter.cs` (sidestep cancel); `FighterCpuController.cs` (except CM-HIT's possible line); `CpuDecisionObservation.cs`; `MirrorParadoxController.cs` (the adapter); new `PlayerController.CastObservation.cs`; `StoryCombatRules.cs` (the D3 forwarders, :72–99) | `FighterMovementSystem.Update`: the cast-region exits CM-SIM built, the Foresight discard (≈ :442), and one D3 feed call in the hitstop branch (≈ :312–325), placed before CM-SIM's busy-caster discard. `FighterEntitySystems.cs` and `FighterKitRules.cs`: the hit-site calls listed in 2.11.1 (not `ResolveFighterHit`'s body). `FighterSimulationSystems.cs`: `ApplyIntent`'s resolution. `BasicComboRules.cs`: a new D3 section anchored after the ability-lockout region. | **+18**: `FighterSpecialRecoveryCancelTests` 12, `SpecialRecoveryCancelTableTests` 1, `FighterCpuBehaviorTests` +2, `MirrorParadoxTests` +2, `FighterCpuSpecialTimingSmokeTests` 1 |

**Phase B total: +96** (declared, after the §0.2 rulings), so a merged target of **3428 = 3228 + 200**, plus any review-fix deltas.

#### Phase B shared-file map

Code:
- `FighterMovementSystem.Update`: four workstreams at distinct sites. CM-HIT has the hitstun-end, refresh and respawn sites. CM-BLOCK has the stance call and the `TickCounters` line. CM-MOVE has gravity, drop-through, the universal-movement cases and the latches. CM-CANCEL has the cast exits, the Foresight discard and the hitstop-branch feed.
- `PlayerController.cs`: four workstreams, in the functions named above. `_PhysicsProcess` alone has CM-BLOCK (the `!TimeFrozen` block) and CM-MOVE (before `MoveAndSlide`).
- `FighterEntitySystems.cs`, `ResolveFighterHit`: CM-BLOCK (the block branch, ≈ :548–599) and CM-HIT (the Defy lines, ≈ :614–622). CM-CANCEL edits hit-site callers only.
- `BasicComboRules.cs`: CM-BLOCK, CM-MOVE and CM-CANCEL, at three different anchors.
- `StoryCombatRules.cs`: CM-MOVE (:8) and CM-CANCEL (:72–99).
- `FighterSimulationDriver.cs`: CM-HIT (`GetStateLabel`, the tumble case), CM-BLOCK (the new flash method plus one call line) and CM-MOVE (the ledge cases). These are small adjacent functions; the merge order resolves them.
- `CalibrationDrillRunner.Sample` (:240–262): CM-HIT (tumble line) and CM-BLOCK (block line). CM-BLOCK, merging second, resolves.
- `FighterBasicAttackRules.cs`: CM-MOVE only. CM-CANCEL steers the variant through `MoveY` and does not edit it.
- `FighterCpuController.cs`: CM-CANCEL (owner), plus CM-HIT's one possible line in `ApplyHitstunDefense`.

Test files (union merge; the later branch resolves):
- `CalibrationDrillScriptTests`: CM-HIT in place, CM-BLOCK +1.
- `StoryDefenceOrderTests`: CM-HIT +1, CM-BLOCK in place.
- `FighterHitPipelineTests`: CM-HIT +1, CM-BLOCK's audit.
- `CpuRecoveryMatrixTests`: CM-HIT in place, CM-MOVE (the climb-jump-only assertion), CM-CANCEL (a re-run).
- `FighterCpuBehaviorTests`: CM-HIT in place, CM-BLOCK in place, CM-CANCEL +2.
- `MirrorParadoxTests`: CM-HIT in place if moved, CM-CANCEL +2.
- `FighterLedgeTests`: CM-HIT in place, CM-MOVE in place.
- `FighterBasicStringParityTests`: CM-BLOCK +1 and in place.
- `FighterPresentationSyncTests`: CM-HIT +1.
- `EnemyControllerTests` and `BossControllerTests`: CM-HIT in place.

### 3.3 Workstream briefs (beyond §2)

**CM-SIM.** The commit order is a contract:

| Commit | Content | Hashes | `Total:` |
|---|---|---|---|
| 1 | The mechanical split and the `partial` keywords | Identical to the predecessor (a seeded replay hash, recorded) | 3228 |
| 2 | The chokepoint (empty body) and every call site | Identical to commit 1 | 3228 |
| 3 | Component 323, every field declared and inert, its resets, its accessor, `FighterActionStateTests` | May move: the registered storage is hashed. Run the replay twice to show it is deterministic, and record before and after. | 3232 |
| 4 | The 8.4 sim hurtbox merge, `FighterHurtboxSourceScanTests` | Identical to commit 3 | 3233 |
| 5 | 1.1(3), 2.3, 10.1, the inert cast plumbing, the `CanStartGrab` terms, the hitstop-branch discard, the aerial-gravity anchor and forwarding line, the `FighterAirPhysics` cast-step overload. Tag **`cm-sim-a0`**. | Move | 3240 |
| 6 | `StorySpecialTimingProbeTests` (Story only). **Stop and report** if it contradicts §2.1.6's reading. | Identical to commit 5 | 3242 |
| 7+ | The Special phase machine (and the Q1 brake, both modes), in its own commits | Move | 3273 |

The `Total:` figures are in CM-SIM's own worktree (commit 7+ ends at 3273 with the two Q1 cases). After rebasing onto CM-BOT..CM-INPUT they are 3287 plus the same increments.

- After commit 3, run the rollback trio (`RollbackReadinessTests`, `FighterSimulationTests`, `RollbackProtocolTests`). The first build confirms that 323 registers.
- Document every reserved bit and value from §1.5 in the component and flag doc comments, so Phase B agents find them in code.
- Keep the CPU suites green by re-timing their assertions only.
- The handoff includes a short **Phase B guide**:
  - where each reserved field is read and written;
  - the chokepoint's signature and its call sites;
  - the cast-region exit points and the hitstop-branch discard that CM-CANCEL extends;
  - the results of the knockdown landing-tick and A02 thaw-tick checks.

**CM-STORY.**
- Q2 and Q7 are ruled (§0.2): startup-only armour; Rail Charge stops at recovery.
- Land 8.1 and 8.2 in one commit, because they share the `ResolveIntakeScale` signature.
- Before the full suite, run `StoryEnemyCombat*`, `StoryFirstSpecialTaxTests`, `StoryPunishWindowTests`, `StoryWallBounceTests`, `EnemyControllerTests` and `MirrorParadoxTests`.
- Run one headless Level 2 smoke.

**CM-ULT.** Run a headless smoke of `TestArena.tscn` and of `Level_13_*.tscn`.

**CM-INPUT.**
- Write the 1.1(5) verification case first (through the new adapter overload), record the failing apex, then fix.
- Never open the editor.
- After editing `project.godot`, run the `--quit` load check.

**CM-BOT.** Do one `play` bot run on Level 2 (Joan, Normal), holding the lock, to show the `rehits` block in a real report.

**CM-HIT.**
- Read CM-SIM's Phase B guide first.
- Before the full suite, run the knockdown, tech, drill, CPU and Mirror suites, and the Story elite/boss launch cases.

**CM-BLOCK.** Audit every block fixture for Block pressed within 5 ticks of contact, and report which ones you re-timed.

**CM-MOVE.**
- Q4, Q5 and Q6 are ruled (§0.2): keep ×2.5; own-jump-only band in both modes; add the sim one-way lip pop.
- Re-run `CpuRecoveryMatrixTests` on the three Open stages, and record the per-hero short-hop heights.
- Run `FighterTumbleTests`, `StoryTumbleTests` and `ShakespeareKitTests` against the merged CM-HIT.
- Do one `play` bot run each on Levels 2 and 7 (Joan, Normal) for the nav-model change.

**CM-CANCEL.**
- Run the smoke suite, and copy its recorded observations into the handoff.
- Run the Mirror on a headless `Level_13_*.tscn --quit-after 600`.

### 3.4 Risk register (read it before starting)

1. **The Fighter Special phase machine is the hardest piece and the critical path.**
   - It touches 18 Specials across all nine kits, three acceptance paths, the presentation, the hit-confirm and lockout rules, Foresight's separate component, the CPU (functionally adapted only in Phase B) and roughly 45 Determinism suites. It moves every Fighter hash.
   - Specific traps:
     - the Divine Piercing lunge's moving ticks against the cast's Active window;
     - the Tempest lift fired at active entry under the new gravity rule;
     - Rift and Vortex placement read at the fire tick;
     - the freeze-only Special buffer meeting the cast lock;
     - the hitstop-branch discard;
     - Root killing the lunge.
   - The zero-contract fallback keeps hand-built harnesses on press-tick timing. If it leaked into a production path, the feature would be silently off. `FighterSpecialTimingTests` uses production loadouts, so that cannot happen unseen.
   - **Mitigation:** Phase A0 lets CM-HIT, CM-BLOCK and CM-MOVE start on the foundation, so an overrun stalls only CM-CANCEL.
2. **`FighterMovementSystem.Update` and `PlayerController.cs` are hubs.** Mitigations:
   - CM-SIM's split, the `partial` classes and the chokepoint;
   - one-line call sites at declared places, with bodies in new partial files;
   - the merge order;
   - the orchestrator resolving the residue.
3. **Test-file contention and the lock.** Five agents, then four, serialize on one GdUnit lock, and several test files are edited by two agents in each phase (§3.1/§3.2 maps). Each merge needs a full-suite reconciliation, and signatures 5 and 8 can mask real failures during that churn. Run filtered first and one full run at the end. Expect long queues.
4. **CPU behaviour shifts.**
   - The CPU is not cast-aware until CM-CANCEL. In Phase A its presses during a cast are discarded, which is harmless but consumes admissions. Phase A's CPU suites must stay green by re-timing, not by adaptation.
   - A reactive Hard CPU will perfect-block often and will block or roll Specials with 10–16 frames of startup, and its Lincoln will whiff Splitting Strike.
   - All of this shifts CPU-driven hashes and the Level 13 Mirror's feel. Only the smoke's recorded counts and the Open-stage matrix measure it before the pad session.
5. **Hand-built frames hide real input.**
   - The latch-based short hop keeps hand-built test frames on full hops. The perfect-block arming and the ledge stick rearm read edges the same way. Existing suites can stay green while real tapped input behaves differently.
   - The `Create`-built cases (§1.8; 2.5.1, 2.5.2, 2.6.3, 2.3.2) are the guard. CM-HIT merges before CM-MOVE and cannot see the short hop, so CM-MOVE owns the post-hitstun interaction case.
6. **Feel risks that only a person can judge** (§5):
   - the Conductor's Call's 60-frame lock;
   - landed ranged Specials becoming safe under D3, which opens Special → basic → Special routes (no scaling, 1.4);
   - frequent knockdowns from 3.2;
   - a grounded Special now braking to a stop (Q1), which changes Story's feel too;
   - 3 frames of pad latency at full meter.
7. **Bots.** 6.1, 6.2, 7.4 and 3.2 change bot outcomes. Before and after batches are compared only as the pair defined in §4.3.
8. **No device glyphs** (STR-16; design :2331 asks for them before a controller-first playtest). They are out of scope, so the pad session uses text prompts.
9. **The docs-3 repository** has unrelated uncommitted edits (§1.1).
10. **Klotho `MAX_COMPONENT_TYPES`** is unverified. CM-SIM's first build confirms it.
11. **Device follow by polling** depends on Godot's per-device input state (`Input.IsJoyButtonPressed`, `IsPhysicalKeyPressed`) being current inside `_PhysicsProcess`. The probe seam keeps the tests independent of this, and §5 row 22 checks it by hand.

---

## 4. Validation gates

### 4.1 Every workstream (in its worktree, holding the lock for each Godot launch)

1. `dotnet build FightersThroughTime.csproj --nologo`. Only the three pre-existing warnings are allowed. No `KLOTHO_DET002`, and no `KLSG_ECS004` on 323.
2. Run the filtered suites for your area:

   ```bash
   dotnet test FightersThroughTime.csproj --settings .runsettings --filter "FullyQualifiedName~<Suite>"
   ```

   | WS | Suites (at minimum) |
   |---|---|
   | CM-SIM | `RollbackReadiness`, `FighterSimulation`, `RollbackProtocol`, `FighterSpecial`, `FighterActionState`, `ShieldBreaker`, `FighterAbilityLockout`, `FighterHitConfirm`, `FighterLockoutRefusal`, `FighterVerbLayer`, `FighterGrab`, `UltimateActivation`, `*KitTests`, `FeelPassSim`, `FighterPresentationSync`, `FighterCpu`, `CpuMeleeBand`, `CpuRecoveryMatrix`, `FighterLedge`, `FighterMatchFlow`, `StorySpecialTimingProbe`, `StoryAbilityLockout` |
   | CM-STORY | `StoryMovementAbilityGravity`, `StoryUltimateArmor`, `AbilityInterruption`, `StoryEnemyCombat`, `StoryFirstSpecialTax`, `StoryPunishWindow`, `StoryWallBounce`, `EnemyController`, `StoryEchoStepCost`, `StoryMeterGainScale`, `InfluenceMeterSourceScan`, `StoryCombatantGeometry`, `MirrorParadox`, `MozartSonataDriftCharge`, `TubmanKitStory`, `TeslaLightningBlinkStory`, `StoryReachKit`, `ResonanceTraversal`, `CleopatraSandDecoy`, `StoryProsperoGust`, `StoryRelativityWarpFold`, `LeonardoMidGlideBolt` |
   | CM-ULT | `FighterPresentationSync`, `StoryUltimateActivation`, `CoreMechanicCueCatalog`, `EnemyAbilityExecutor`, `MirrorParadox` |
   | CM-INPUT | `UltimateChord`, `InputDeviceFollow`, `CharacterSelectScene`, `PauseFocusLoss`, `LocalFighterPause`, `StoryPause`, `HapticDeviceRouting`, `Gamepad`, `InputBindingSchema`, `MirrorParadox`, `FighterCpuBehavior`, `CpuRecoveryMatrix` |
   | CM-BOT | `PlaytestRehitTracker`, `PlaytestCombatStats`, `PlaytestEncounterTelemetry` |
   | CM-HIT | `FighterTumble`, `StoryTumble`, `FighterKnockdown`, `FighterVerbLayer`, `DownAirSlam`, `FighterAirDodge`, `StoryAirDodge`, `FighterMatchFlow`, `DefyProtectedRecovery`, `StoryDefenceOrder`, `FighterHitPipeline`, `CalibrationDrillScript`, `TutorialCalibrationV76`, `CpuRecoveryMatrix`, `FighterCpuHitstunDefense`, `FighterCpuBehavior`, `FighterLedge`, `EnemyController`, `BossController`, `MirrorParadox`, `GlowPresentationController`, `FighterPresentationSync` |
   | CM-BLOCK | `ShieldstunCurve`, `FighterPerfectBlock`, `StoryPerfectBlock`, `StoryBlockModel`, `BlockShatterLockout`, `SpecialBlockChargeCost`, `DefenceOrdering`, `FighterBasicStringParity`, `FighterGrab`, `FighterHitPipeline`, `StoryDefenceOrder`, `StoryEnvironmentalDamage`, `CollapseTremor`, `StoryPunishWindow`, `CalibrationDrillScript`, `CpuVerbPolicy`, `FighterCpuBehavior` |
   | CM-MOVE | `FighterShortHop`, `FighterMovementFeel`, `StoryAirPhysics`, `StoryMovementFeel`, `StoryCornerForgiveness`, `FighterStageGeometry`, `FighterDropThrough`, `FeelPassStory`, `FighterLedge`, `CpuRecoveryMatrix`, `PlaytestFlightModel`, `ArenaGeometryContent`, `TutorialCalibrationContent`, `Level09Content`, `Level10Content`, `FighterTumble`, `StoryTumble`, `ShakespeareKit`, `MoveListScreen` |
   | CM-CANCEL | `FighterSpecialRecoveryCancel`, `SpecialRecoveryCancelTable`, `StorySpecialRecoveryCancel`, `TubmanKit`, `FighterCpu`, `CpuVerbPolicy`, `CpuRecoveryMatrix`, `CpuMeleeBand`, `MirrorParadox`, `FighterCpuSpecialTimingSmoke` |

3. Run the headless load check:

   ```bash
   "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" --headless --path "<worktree>" --quit
   ```

   Run `--import` instead after any `en.csv` edit, and commit `en.en.translation`.
4. Run your smoke runs (§3.3) with `--quit-after 300`. They must exit 0 with no script error. Compare against a control scene before blaming the "N resources still in use" or `Handle is not initialized` noise.
5. Do **one full run**, reconciled exactly against your running total (§4.2). Read the failure-signature table before trusting any partial total.
6. The handoff states the commands run, their totals, and "nobody played the build".

### 4.2 Orchestrator, at each merge point

After each merge:
1. Resolve and regenerate the translation (§1.13), then run `--import`.
2. Run the full suite.
3. Reconcile against the declared running total:
   - **Phase A** (after the §0.2 rulings): 3228 → 3235 (CM-BOT) → 3256 (CM-STORY) → 3263 (CM-ULT) → 3287 (CM-INPUT) → 3299 (`cm-sim-a0`) → 3332 (CM-SIM).
   - **Phase B:** → 3352 (CM-HIT) → 3374 (CM-BLOCK) → 3410 (CM-MOVE) → 3428 (CM-CANCEL).
   - Add any review-fix deltas.
4. Run the review round where §3 places one.

### 4.3 Closeout gates (Phase C)

1. **Three consecutive green full runs**, with orphans drained and the lock held, reconciled exactly against 3228 plus every handoff delta and every fix-round delta.
2. **Rollback re-measure:**

   ```bash
   dotnet test FightersThroughTime.csproj --settings .runsettings --filter "FullyQualifiedName~RollbackReadinessTests" --logger "console;verbosity=detailed"
   ```

   Record the worst-case depth-7 median, the p95 and the breaches, and the largest snapshot size. The snapshot should grow by about 72 B for 323. Update `docs/PERFORMANCE_BASELINE.md`.
3. **Smoke runs** (`--quit-after 300`, exit 0, no script error): `MainMenu.tscn`, `HubWorld.tscn`, `Level_00_Tutorial.tscn`, `Level_07_*.tscn`, `Level_13_*.tscn`, `TestArena.tscn` (with `--quit-after 1800`) and `FighterStage_Paris*.tscn`.
4. **CPU smoke** (there is no Fighter telemetry): `FighterCpuSpecialTimingSmokeTests` (Hard vs Hard on all ten stages, with its recorded counts), `CpuRecoveryMatrixTests` on Paris, Vesuvius and Nassau, and `FighterCpuBehaviorTests`. All must be green in the closeout runs.
5. **Bot smoke batch** (the Story side), holding the lock.
   - **Both batches run in one dedicated clean worktree** (`cm-bots`, created with `git worktree add` and seeded as in §1.10), never in the main checkout. That way the user's uncommitted edits reach neither batch, and before and after share a checkout state.
   - **Before**: the worktree at the plan commit, tags `cmbefore0` / `cmbefore1`.
   - **After**: the same worktree checked out at the closeout commit, tags `cmafter0` / `cmafter1`.
   - The batch: Levels 2, 6, 7, 12 and 13; Joan and Lincoln; `play`; Easy and Normal; seeds 0–1; `-MaxSeconds 0`. That is 40 runs per batch.

     ```powershell
     ./tools/playtest/run_playtests.ps1 -Levels 2,6,7,12,13 -Characters joan,lincoln -Bots play -Difficulty normal -Parallel 8 -MaxSeconds 0 -Tag cmafter0 -ExtraArgs '--seed=0'
     ```

   - Report:
     - completions and deaths;
     - `damage_by_source` (7.4);
     - knockdown and tech counts, if the telemetry shows them;
     - `stuck_lock` endings;
     - the **after-batch `rehits` table**. 7.2 is measured on the merged build only.
   - Compare pooled seeded batches (`compare_runs.py "cmbefore*" "cmafter*"`). Treat a change as real only when the per-batch ranges do not overlap (CLAUDE.md's comparability note).
   - Remove the `cm-bots` worktree afterwards.

---

## 5. The pad-session checklist (two players, two pads)

**Setup.** Record the build commit, the stage, the two heroes, the CPU difficulty if any, and the settings: Block mode, and Hazards On/Off. There are no glyphs (§3.4). For each line, write down what was seen and whether it calls for a ruling. Each line names the ledger row it closes or feeds.

| # | Decision | Setup | Look at | A ruling is needed if … | Row |
|---|---|---|---|---|---|
| 1 | 2.2 Ultimate damage | Any stage, 3 stocks, both players building meter | How much of a stock one landed Ultimate takes (70–80 against 80–130 HP), and how often it lands now that the wind-up is telegraphed | One Ultimate decides a stock, or Ultimates never land | `VERIFY-ULT-ACTIVATION-TUNING` |
| 2 | 3.3 Walls | Sealed stage, corner play | Whether a launched fighter can be re-hit against a wall with no answer (a loop) | A repeatable wall loop exists | `VERIFY-WALL-PIN` |
| 3 | 5.4 Pace | Two Open stages and two Sealed | Neutral pace after the −20 % cut | 1v1 feels sluggish | `USER-MOVEMENT-SPEED-CUT` |
| 4 | 8.3 Hitstop | Basic strings, finishers, Specials | The weight of hitstop 4–11 | Hits feel sticky, or weightless | `PROVISIONAL-HITSTOP-WEIGHT` |
| 5 | 10.2 Sudden Death | Force a timeout tie | Stalling in Sudden Death | Either player can stall it out | F22 |
| 6 | 10.3 Air game | Short hops plus the air strike | Whether short-hop air strikes give an air game, and whether a late air strike is plus on block under 4.1 | The air game is still thin | `USER-AERIAL-SINGLE-STRIKE` |
| 7 | 10.4 Comeback | A player down two stocks | Orb heals, Rally, Defy (now ending on attack), missing-HP knockback | A losing player wins too often, or never | — |
| 8 | 1.2 Special timing | Each of the nine kits | Whiff punishability; the Conductor's Call's 60-frame lock; Divine Piercing's 13-frame startup; a running cast **sliding at its entry speed**, matching Story (Q1) | A Special feels unusable, still safe on whiff, or the slide reads wrong | `PROVISIONAL-SIM-SPECIAL-PHASE-RULES` |
| 9 | 1.3 Shield-Breakers | Block a Shield-Breaker at full shield, and perfect-block one | Whether shatter → Ultimate is still guaranteed (the recon estimates by 0–12 frames; see CM-SIM's logged gap); whether a perfect-blocked Shield-Breaker should still shatter (Q3) | A guaranteed 70–80 from one block | `USER-SHIELD-BREAKER-STARTUP` |
| 10 | 11.1 D3 in Fighter | Landed Specials, especially the Emancipator at 5 u | Whether landed ranged Specials are now safe; Special → basic → Special routes | A route is too strong (no scaling, 1.4) | `USER-FIGHTER-SPECIAL-RECOVERY-CANCEL` |
| 11 | 3.4 Perfect block | Both players and a Hard CPU | Whether the 5-frame window is learnable; whether the anti-mash rearm holds; how often the Hard CPU perfect-blocks (compare the smoke's counts); whether the flash is readable | It is trivially easy, never possible, or the CPU walls it | `PROVISIONAL-PERFECT-BLOCK-RULES` |
| 12 | 4.1 Shieldstun | Block light hits and heavy Specials | Who acts first after a block, by damage | Light hits feel minus or plus the wrong way | `PROVISIONAL-SHIELDSTUN-CURVE` |
| 13 | 3.2 Tumble | Finishers, Up-Attacks, throws | Knockdown frequency; acting out of tumble; a post-hitstun rise is never cut by the short hop (Q5) | Too many knockdowns stall the pace, or recovery after a launch feels robbed | `PROVISIONAL-TUMBLE-EXIT-ACTIONS` |
| 14 | 5.1 / 6.1 Short hop and hang | Taps against holds, both modes | Short-hop height against the decided 45–55 % (Q4's recorded values); a released hop drops through its peak | The hop feels too low or too floaty | `USER-SIM-SHORT-HOP`, `PROVISIONAL-SIM-SHORT-HOP-HEIGHT`, `USER-APEX-HANG-HELD` |
| 15 | 6.2 Corner forgiveness | Story ledges and low ceilings; Fighter one-way platform tops (the sim lip pop, Q6) | Clips forgiven without skipping gates; the sim pop reads as a clean landing | A gate becomes skippable, the nudge reads as a teleport, or the sim pop steals a ledge grab you wanted | `PROVISIONAL-CORNER-FORGIVENESS-RULES` |
| 16 | 6.3 Drop-through | Both inputs, both modes | Accidental drops | Down+Jump drops when a jump was meant | `USER-DROP-THROUGH-BOTH-INPUTS` |
| 17 | 5.2 Ledge get-ups | Open stages, ledge pressure | Whether the first-grab intangibility or the ledge attack is too safe; whether the fresh-tilt neutral climb is easy to trigger on purpose and hard to trigger by accident | Ledge play is unpunishable, or the climb misfires | `PROVISIONAL-LEDGE-GETUP-FRAMES` |
| 18 | 4.2 Respawn | Lose a stock, then wait or drop | 120 → 60 protection; whether attacking ends it | Respawn is too safe or too punishable | `PROVISIONAL-RESPAWN-PROTECTION-CURVE` |
| 19 | 5.3 Air dodge | Get hit after dodging | The refresh on hit | Escapes become too easy | `USER-AIR-DODGE-REFRESH-ON-HIT` |
| 20 | 2.1 Telegraph | Ultimates by P1 (blue) and P2 (red edge) | Whether the red tint plus X is readable on P2 | The telegraph is missed | `PROVISIONAL-ULT-TELEGRAPH-LOOK` |
| 21 | 9.2 Chord | A full meter, pressing LB or RB alone | 3 frames of latency; no leaked movement ability | The latency is felt, or the chord still leaks | `PROVISIONAL-ULTIMATE-CHORD-LENIENCY` |
| 22 | 9.1 Devices | Story pad-only; Fighter select with keyboard and pad; a pad press inside a menu that handles input | P1 follows; press-to-join | The wrong device gets control | `PROVISIONAL-DEVICE-FOLLOW-RULES` |
| 23 | Story feel (2.4, 7.1, 7.3, 7.4, 8.1, 8.2) | A Story level on Normal; Cleopatra or Mozart, then Lincoln | Ultimates armoured through their startup only (Q2); ability shake; the 10.5 Echo Step cost; blocking hazards; zoner hits to kill; dimmed numbers legible and distinct from the grey 0 | Any reads wrong | the matching `USER-*` / `PROVISIONAL-*` |
| 24 | 1.1(1), 1.1(5) | Leonardo's glide and the other movement abilities; Lincoln's Rail Charge stopping at 300 px (Q7); the Level 13 Mirror | The glide sinks; the hovers are gone; the Mirror jumps full height; the Rail Charge stops cleanly | A movement ability now fails a route, or the Rail Charge stop reads abrupt | `PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`, `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY` |
| 25 | 9.3 CPU against the new timing | Hard CPU, several kits | How often it blocks or rolls Specials in their startup; Lincoln's Splitting whiffs; the Mirror's feel (compare the smoke's counts) | The CPU walls Specials or throws away its own | `USER-CPU-SPECIAL-TIMING-ONLY` |

---

## 6. Handoff format (`docs/handoffs/CM_<id>.md`)

1. **Shipped:** what changed, with exact API names, constants and their values.
2. **Files changed:** including any outside your ownership, each with the reason.
3. **Decisions:** every *Provisional* number or behaviour you chose, with one line of reasoning. These become ledger rows. Name the §0.1 ruling or default you built against.
4. **Deviations from this plan:** what you did differently, and why.
5. **Tests:**
   - new suites and cases (+N);
   - rewritten cases, named before → after;
   - shared test files you touched (§3 maps);
   - the suites you ran, with their totals;
   - your final full-suite `Total:`, reconciled against the declared running total.
6. **Measurements:** anything the ledger needs. For example:
   - per-hero short-hop heights;
   - the Mirror apex before and after;
   - the shatter → Ultimate frame gap;
   - replay hashes before and after each mechanical commit;
   - Story's Special effect tick and cast motion;
   - the smoke's recorded CPU counts.
7. **Not delivered / open:** the honest list.
8. **For the closeout:**
   - AGENTS.md / CLAUDE.md bullets to change;
   - design-master lines owed (`design-godot.md` line numbers at `e2c655a`), flagging any `DOC-MASTER-UNBOUND-BINDING` line;
   - contract lines, naming which copies differ;
   - ledger rows to write or amend.

## 7. Phase C closeout (orchestrator, main checkout)

1. Merge Phase B in the declared order. Regenerate `en.en.translation`. Run `--import` and the full suite.
2. Run the adversarial review of the Phase B diff (two refuting verifiers per finding), and fix the confirmed defects.
3. Run the §4.3 gates: three green full runs, the rollback re-measure, the smoke runs, the CPU smoke, the after bot batch in the clean worktree, and the comparison against `cmbefore*`.
4. **Ledger.** Add a new section, "Confirmed open deviations — combat and movement update, 2026-10-16", with:
   - every `USER-*` row, quoting the decision verbatim with its decision ID;
   - every `PROVISIONAL-*` and `FIX-*` row from the handoffs;
   - a row for each §0.1 question, stating the ruling or that the default was used;
   - the new `VERIFY-*` rows:
     - `VERIFY-WALL-PIN` (3.3, waiting on the pad session);
     - `VERIFY-SIM-PIT-FLOOR-SNAP` (6.2);
     - any 1-tick offset under `VERIFY-SAME-TICK-VERB-PARITY`;
     - the knockdown landing-tick and A02 thaw-tick findings, if CM-SIM found them leaking;
   - closures for:
     - `DEFER-SIM-SPECIAL-PHASES` and `SIM-SPECIAL-POSE-THIRDS`;
     - `DEFER-ULT-TELEGRAPH`;
     - `VERIFY-STORY-HAZARD-BLOCK`;
     - `VERIFY-SIM-STRING-FACING-PER-TICK`;
     - `VERIFY-SAME-TICK-VERB-PARITY` (1) and (5);
     - `VERIFY-GAMEPAD-ULTIMATE-CHORD` (a) and (c);
     - the short-hop half of `DEFER-H7-STORY-TERMINAL`;
   - the amendments listed in §0.

   Then add an index section, "The 2026-10-16 combat and movement update — index and design lines edited". Closed rows stay, struck through.
5. **Design and contracts.**
   - Edit the master and the mirror per §1.1, with a `diff --strip-trailing-cr` afterwards. The Unbound lines are edited by hand in each copy.
   - Edit the contracts in **both** copies (repo and docs-3), with the same hunks, diffed afterwards per §1.1:
     - `DEFENSIVE_EFFECTS.md` (D01/D02b perfect block, D03c Story hazards, D04);
     - `COMBAT_VALIDATION.md`;
     - `ABILITY_DATA.md`;
     - `COMFORT_SETTINGS.md` (C01c, minding the Unbound lines);
     - `CPU_COMBAT_POLICY.md`, `CPU_RECOVERY.md`, `MIRROR_PARADOX.md`;
     - `ROLLBACK_STATE_CONTRACT.md`;
     - `FIGHTER_MATCH_RULES.md`, if it restates 3 s;
     - `MULTIPLAYER_DELIVERY.md`, only if 9.1 changes it.
   - Report every hunk applied in docs-3. Do not commit that repository unless the user asks.
6. **AGENTS.md / CLAUDE.md.**
   - The Klotho inventory (323, 36 B; highest ID 323; the reserved bits).
   - The Combat, universal-movement, block, D01–D04, M05/M07, CPU, InputManager and C01c bullets, and the cross-mode parity bullet (M-18 closed, H-7 narrowed).
   - "Still held" and "not delivered".
   - A new known-gaps subsection for this package.
   - The baseline paragraph, with its per-suite arithmetic.
   - CLAUDE.md:
     - the "Where to start" rows: "Character ability", "Ultimate", "Fighter simulation / netcode", "Fighter CPU", "Input remapping";
     - the bot notes and the comparability note: 6.1, 6.2, 7.4 and 3.2 change bot outcomes, and 7.2 adds the `rehits` block.
   - Supersession notes in `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md` §2.11.
   - `docs/PERFORMANCE_BASELINE.md`.
7. Fill this plan's §9 and §10. Commit on `main`. Prune the merged worktrees, branches and the `cm-sim-a0` tag.

## 8. Out of scope

- Story's `LedgeGrabPoint` get-ups (5.2, "later").
- Story mob knockdown and tumble.
- Story mercy invulnerability itself (7.2 measures first).
- CPU kit awareness, and CPU use of D3, the ledge get-ups or the perfect block.
- Device glyphs.
- Frame tables, a combo enumerator and a CPU-vs-CPU measurement batch (9.4).
- Per-character or design-size hurtboxes (8.4 merges copies only).
- The sim's any-depth pit-floor snap (`VERIFY-SIM-PIT-FLOOR-SNAP`).
- Story's post-rewind protection (Q8 ruled Fighter only). (Q5, Q6 and Q7 were ruled into scope on 2026-10-16; see §0.2.)
- Story's launch-band short-hop clause (it stays in `DEFER-H7-STORY-TERMINAL`).
- The A02 Ultimate caster's thaw-tick input, and a knockdown landing-tick leak, if CM-SIM finds either (recorded, not fixed).
- The docs-3 copy of the ledger.
- A protocol bump. Any save-schema change.
- Ultimate damage, hitstop or speed retunes.
- Owed design lines from earlier packages outside the paragraphs this package rewrites.
- Production art and audio for the telegraph, the perfect-block flash and the join prompt (placeholders only).

## 9. Deviations log (append-only, filled from the handoffs)

Each workstream's handoff (`docs/handoffs/CM_<id>.md`) is the full record. The entries below list only:
- what departed from §1–§5, and why;
- each workstream's test delta;
- its commits.

Every agent choice has a ledger row in the section named in §7.4.

Filled at the closeout (2026-10-17) from the eighteen handoffs and the orchestrator's merge record. Each heading gives the branch tip (the handoff commit) and the merge commit on `main`. Every total below is the `[TestCase]` attribute count at that merge commit, which equals the full run's `Total:` there. Where a later fix round changed what a workstream shipped, its entry says so. The handoffs were not edited afterwards, so where two disagree the later handoff wins.

### Questions (§0.1)

All eight were ruled by the user on 2026-10-16, before Phase A (`D:/Projects/genre_review_2026-10-13/DECISIONS_combat_movement.md`, "Plan questions"). Q1, Q5, Q6 and Q7 went against the drafted default.

- **Q1** — "**Brake to a stop, both modes**: grounded casts brake over the 4-frame run-stop ramp, aerial casts keep their drift; Story changes too." Not the default. Built by CM-SIM in both modes (Story: `PlayerController.ApplySpecialCastBrake`). `USER-SPECIAL-CAST-BRAKE`.
- **Q2** — "**Startup only** (a Story rule; `.tres` flags stay false)." The default. Built by CM-STORY. `USER-STORY-ULTIMATE-ARMOR-STARTUP-ONLY`.
- **Q3** — "**Still shatters.**" The default. Built by CM-BLOCK. `USER-SHIELD-BREAKER-BEATS-PERFECT-BLOCK`.
- **Q4** — "**Keep ×2.5, measure every hero, ledger any below 45 %.**" The default. CM-MOVE measured every hero's 1-tick tap at 41.6–41.9 % of the held apex, all below 45 % (2- and 3-tick taps 46.2–51.7 %). Nothing was retuned. `USER-SHORT-HOP-MULTIPLIER-KEPT`, `PROVISIONAL-SIM-SHORT-HOP-HEIGHT`.
- **Q5** — "**Own jump only, both modes** (Story's band gains the same gate)." Not the default. Built by CM-MOVE (323 `ActionFlags` bit 3 `RiseFromOwnJump`; Story's `StoryAirPhysics.SelectBand`). `USER-SHORT-HOP-OWN-JUMP-ONLY`.
- **Q6** — "**Add the Fighter lip pop**: a fighter within 0.2 u below a one-way top at its apex or falling lands on it, resolving before ledge capture; the sim head-clip nudge stays a no-op." Not the default. Built by CM-MOVE. `USER-SIM-LIP-POP`, `PROVISIONAL-SIM-LIP-POP`.
- **Q7** — "**Stop it like the sim** (zero horizontal velocity at recovery start)." Not the default, so `VERIFY-RAIL-CHARGE-RECOVERY-CARRY` was never opened. Built by CM-STORY; CM-FIXA-STORY added the startup hold, so a running Lincoln travels 300 px from the press. `USER-RAIL-CHARGE-STOPS-LIKE-THE-SIM`, `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY`.
- **Q8** — "**Fighter only, both levers** (settled by the round-4 option text)." The default. Built by CM-HIT. `USER-RESPAWN-PROTECTION-FIGHTER-ONLY`.

### Phase A

*Merge order as planned: CM-BOT → CM-STORY → CM-ULT → CM-INPUT → `cm-sim-a0` → CM-SIM. All five branched from the plan commit `735296f`.*

**One departure from the order of work (§3, step 4).** CM-SIM did not rebase onto the merged CM-BOT..CM-INPUT before its final full run. Its branch base is still `735296f`, and its last run (3273) was in its own worktree. The orchestrator merged `cm-sim-a0` and then the rest onto `da08d70`, and the merged Phase A was green at 3332, the base every Phase A fix agent reports.

#### CM-BOT — the re-hit counter (`c12144c`, merged `cfba295`)

- The observation (`PlayerController.DamageTaken`, in `PlayerController.DamageObservation.cs`) also carries `HitboxID`, the only way to tell a pooled shot from a melee hit at the chokepoint. The report gains keys beyond the brief: `xsrc_damage_30`, `xsrc_pct`, `xsrc_gap_histogram`, the log's `xgap` / `xk`, the `unobserved_hits` self-check and the CSV's `hits_taken`.
- The engine cases deliver real mob and boss attack payloads through `PlayerController.TakeDamage`, instead of driving two mob AIs into swinging on cue.
- Its one full run read `Failed: 1` of 3235: a `RollbackReadinessTests` wall-clock breach on `alexandria_chambers` (max 10.942 ms against the 8.0 ms budget), on a machine shared with other worktrees. The class passed 22/22 alone, and the branch changed nothing under `scripts/FighterSim/`, `scripts/Networking/` or `tests/Determinism/`.
- Superseded by CM-FIXA-PRES: periodic ticks (status, zone pulses, drowning, stage-hazard DPS) are neither hits nor predecessors, and the combat-only figure `xsrc_30_combat` leaves hazard contacts out on both sides. CM-BOT's Decisions 2 and 6 are corrected in `PROVISIONAL-REHIT-COUNTER-RULES`.
- Test delta: **+7** (declared +7). Handoff: [CM_BOT.md](handoffs/CM_BOT.md).

#### CM-STORY — Story combat feel (`144bd15`, merged `7051f53`)

- Leonardo's glide caps its descent through a new `BaseSpecial.OwnerFallSpeedCapNow`, applied between gravity and the move. The plan said he needs no override, but kits write velocity after `MoveAndSlide`, so the old post-move clamp would have let him fall at about 87 px/s. The Daedalus dive suspends gravity (`PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`).
- Lincoln's Rail Charge wrote its whole velocity vector, zeroing Y every tick, not X only as the plan assumed. It now writes X only (`DriveCharge`), and `OnActive` drives the charge, so it travels exactly 300.0 px. With Q7 alone it would have travelled 290 px.
- Mozart settles onto his staff, whose top is flush with his feet, instead of hovering 6 px above it as the plan said.
- Leonardo's first glide frame still rises 0.07 px; the plan's "descends from his first glide frame" holds from frame 2. Nothing was forced.
- The reworded `systems_card_echo_body` row stays at `en.csv` :959 under a `# CM-STORY` comment, clear of CM-INPUT's end-of-file block.
- Its open items 1 and 2 (the other kit motions losing their first active tick; the Rail Charge's running startup slide) were fixed by CM-FIXA-STORY.
- Test delta: **+21** (declared +21; the skeleton's "+20" predated Q7). Handoff: [CM_STORY.md](handoffs/CM_STORY.md).

#### CM-ULT — the Ultimate telegraph (`5186474`, merged `d4d0043`)

- The red tint rides a dedicated glow-arbiter flag (`SetUltimateTelegraph`) instead of being re-asserted on the tint-override channel each frame. The ownership row allowed this; the dossier's primary text did not use it. A hit flash now rides on top of the red, and the red returns as the flash ends.
- The tint is steady. The ruling says "red flash"; the dossier said steady, which keeps it C01a-safe. Left for the user (`USER-ULT-TELEGRAPH`, `PROVISIONAL-ULT-TELEGRAPH-LOOK`).
- A `TransitionTo` exit block resets a stunned or killed Mirror clone's activation phase. The dossier named that defect but gave it no row (`FIX-MIRROR-ULT-PHASE-EXIT`).
- `EnemyAbilityExecutorTests` needed no in-place change. `EnemyAbilityExecutor.cs` gained one `using FTT.Combat;` line beyond "colour reads only".
- Amended by CM-FIXA-PRES: the Fighter telegraph is derived from a live, free caster, so it drops on the KO tick; and the tint is never warmed by the hero aura, the Hard Mirror clone's included.
- Test delta: **+7** (declared +7). Handoff: [CM_ULT.md](handoffs/CM_ULT.md).

#### CM-INPUT — devices, chord, CPU jump hold (`6fc1aff`, merged `da08d70`)

- `DeviceActivityProbeForTesting` also replaces the per-device sample reads, so a test can show the switching press arriving as a fresh edge.
- The Story readiness report sits at the top of `_PhysicsProcess`, not the end. Hitstop and the tech lock return early, so a report at the end would go stale mid-freeze.
- `LocalFighterPause` gained a FollowLastUsed resolution path, so a pad-only Versus CPU or Holodeck player whose pad drops can resume from the keyboard.
- `GamepadOverrideRepairTests` needed three more in-place edits than the plan listed (a stale Ultimate's pad half is now dropped, not repaired onto LB + RB), and two `LocalFighterPauseTests` topology cases changed.
- The build no longer shows `CS0649`, because the test probe now assigns `BlockModeOverrideForTesting`.
- Superseded by CM-FIXA-INPUT:
  - `BeginLocalJoin` pre-claims no slot. CM-INPUT kept player 1 on whatever device drove the menus, so after keyboard-driven menus two pads could not both join.
  - The join press is consumed, and `ui_cancel` follows the device that pressed it.
  - Bound pad axes (the triggers) count as activity.
  - A pause forgets chord waits but keeps masks.
  - A saved pad chord switched off with no pad route is restored.
  - The Mirror full-hop case, whose measured arc included a queued air jump, now measures one ground jump: 186.4 px, a held single jump's height.
- Test delta: **+24** (declared +24). Handoff: [CM_INPUT.md](handoffs/CM_INPUT.md).

#### CM-SIM A0 — the sim foundation (`cm-sim-a0` = `b62bf1a`, merged `a6d9bf2`)

- Commits 1–5, in §3.3's order: `9ca55ea` (the split), `d45a637` (the empty chokepoint), `a4545af` (component 323, inert), `f9be945` (the one named sim hurtbox), `b62bf1a` (1.1(3), 2.3, 10.1, the inert cast plumbing).
- The replay-hash contract held, on a probe of 24 seeded matches:
  - commits 1, 2 and 4 left all 24 hashes identical to their predecessor;
  - commit 3 moved all 24 (323's storage is hashed), and two runs agreed;
  - commit 5 moved 10 of 24.
- Commit 5's full run read `Failed: 2` of 3241. One was a harness bug in the new `NoNotYetRefusalIsClassifiedInsideTheTechLock`, fixed before tagging. The other was a wall-clock `RollbackReadinessTests` breach (17.4 ms on `paris_bastille`), green on an isolated rerun.
- Test delta: **+12** (declared +12).

#### CM-SIM — the Special phase machine (`4ca78e7`, merged `5434151`)

- Commits 6–10: `aaf6e6d` (`StorySpecialTimingProbeTests`), `366bfa2` (the phase machine), `c130fa6` (the Q1 brake, Story half), `36b1ee7` (the three new Fighter suites), `4ca78e7` (the handoff). The probe agreed with §2.1.6's reading (Story slid at 324 px/s and fired on T+S−1), so commit 6 did not stop the work.
- Divine Piercing's lunge starts in the movement system's `AdvanceSpecialCast` on the active-entry tick, not in the fire pass, so its 12 moving ticks are exactly its 12 active ones. `FighterKitMotion.Reach.cs` is untouched.
- `FighterCounter.cs` is untouched: the Delegated marker is written at acceptance and cleared when Foresight's stance ends.
- Two new partial files (`FighterMovementSystem.SpecialCast.cs`, `PlayerController.SpecialCastMotion.cs`) instead of growing the system file or touching more `PlayerController` lines.
- There were no cast or impact VFX to retime: the driver emits no Fighter ability VFX.
- `FighterSpecialTimingTests` case 5 pins the Q1 brake, not §2.1.6's pre-ruling case 5 ("holds its entry horizontal velocity").
- The driver's "not yet" classifier still passes no 323 state. The cast discard zeroes `PressedButtons`, so nothing flashes during a cast.
- `TeslaBlinkSimTests`' hand-built shooter gained `StartupFrames = 0`, and `RollbackReadinessTests.RunConvergence` gained a `Func<FighterSimulation>` overload.
- Measured:
  - The hand-built default replay hash did not move across commit 7, so the 0 / 1 / 0 fallback reproduces press-tick timing.
  - Story fires a Special's effect on T+S−1, one tick before the sim's T+S (`VERIFY-SAME-TICK-VERB-PARITY` (8)).
  - The sim still accepts an Ultimate on the tick Block is released (`VERIFY-SAME-TICK-VERB-PARITY` (9)).
  - A blocked Shield-Breaker still lets the attacker's first Ultimate press come 39 / 45 / 32 frames (Divine Piercing / the Emancipator / Splitting Strike) before the dazed blocker can act (`USER-SHIELD-BREAKER-STARTUP`).
- The knockdown landing-tick and A02 thaw-tick checks (§3.3) both leaked:
  - Special 1, Special 2, the movement ability, the direct grab and a paid Ultimate all started on a missed tech's landing tick;
  - a grab and the movement ability started on the hitstop thaw tick of an armoured wind-up.

  Both were recorded per §8, and CM-FIXA-SIM fixed both (`VERIFY-SIM-KNOCKDOWN-LANDING-TICK`, `VERIFY-SIM-A02-THAW-TICK`).
- Test delta: **+33**; **+45** with A0 (declared +33 / +45 after Q1; the skeleton's "+31; +43" predated Q1's two Story cases). Handoff: [CM_SIM.md](handoffs/CM_SIM.md), with its Phase B guide. Its §6 sentence "the knockdown branch's own discard covers L+1 on" and its §9 "The two checks" are superseded by CM_FIXA_SIM.md.

**Phase A merged: 3332 = 3228 + 104** (`5434151`), exactly as declared.

### Phase A review fixes (one round, not A1 and A2)

**The review.** CM-SIM finished inside Phase A, so the plan's split (A1 on CM-BOT..CM-INPUT + A0, A2 on the phase machine) was not needed. One adversarial review ran over the whole merged Phase A at `5434151`: six areas, two refuting verifiers per finding.
- **29 findings.** 15 were confirmed by both verifiers (2 high, 3 medium, 10 low). 14 were dropped: 7 refuted by both, 7 split.
- **22 went to the fix round,** under the orchestrator's rulings:
  - the 15 confirmed;
  - five split ones: a Special cast accepted on the knockdown landing tick, the A02 thaw tick, the trigger axes, the Local Versus pre-claim, and the tech test's dead BasicAttack row;
  - two refuted by both: the running Rail Charge's startup slide, and the Hard Mirror's aura-warmed tint.
- **Seven were not taken:** the 8.4 Rift owner-speed overlap reading the hurtbox; the 8.1 floor reading post-buff damage; two on the 7.1 player-side shake gate; the CPU jump sustain ending before a buffered Story jump; the steady telegraph against "red flash" (left to the user); and a punished, prorated hit showing the gold Punish number rather than a dimmed one (the built rule: Punish wins).
- Record: `D:/Projects/genre_review_2026-10-13/phaseA_review.json`; fix inputs `fixA_inputs.json`.

The four fix agents' worktrees were created at the stale `d2e52e0` (the 2026-10-07 baseline), and each agent moved its branch to `5434151` before any edit. The Phase B fix round's worktrees started the same way, and were moved to `740d712`.

#### CM-FIXA-SIM — the sim half (`6f668f0`, merged `891b954`)

- Nine findings, under the orchestrator's rulings (a)–(d):
  - **(a)** A missed-tech knockdown and its get-up lock every action from the landing tick through the get-up, hitstop thaw ticks included, and a running Special cast is interrupted by the knockdown.
  - **(b)** The tech's invulnerability lasts as long as its action lock: 13 ticks counting the landing tick. It used to last about 24.
  - **(c)** No action starts on the hitstop thaw tick of an Ultimate caster in its wind-up or whiff recovery, and the DI read is kept.
  - **(d)** The recovery Block-cancel tick keeps only the Block stance (`PressedButtons` masked to Block). This supersedes CM-SIM decision 13.
- Also fixed:
  - Divine Piercing's Block cancel opens on its recovery frame 0.
  - An aerial Foresight gets the half gravity and a planted stance.
  - A stock-loss tick takes no Special press and resolves no melee contact from the respawn platform.
  - The tech test's dead BasicAttack row is skipped.
- Recorded, not fixed:
  - the grabber's throw-animation invulnerability outliving the throw (fixed later by CM-HIT, `FIX-SIM-THROW-GRANT-OUTLIVES-THROW`);
  - the CPU grab mirror's missing knockdown and A02 terms (added later by CM-CANCEL).
- Test delta: **+10** (declared +10). Handoff: [CM_FIXA_SIM.md](handoffs/CM_FIXA_SIM.md).

#### CM-FIXA-STORY — Story kit motion and armour (`e902be9`, merged `33e1a86`)

- Four findings.
- Every kit motion drives its travel from `OnActive`, so its first active tick moves (`VERIFY-STORY-KIT-MOTION-FIRST-TICK`, closed):
  - Divine Piercing 165 → 180 px;
  - Lightning Blink 165 → 180;
  - North Star Leap 226.67 → 240;
  - Desert Mirage 224 → 240;
  - Sonata Drift 170 → 180;
  - and, for consistency, the Grand Crusade 795 → 810.
- Rail Charge's startup holds X, so a running Lincoln travels 300.0 px from the press (it was 345.6). This joins `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY`.
- Einstein's Rift Collapse launch no longer stuns a caught hero through the Ultimate's startup armour. The pull still moves an armoured body (`PROVISIONAL-STORY-ULTIMATE-ARMOR`).
- Left: a running Cleopatra's 69.6 px Desert Mirage startup slide, fixed later by CM-MOVE (`FIX-STORY-DESERT-MIRAGE-STARTUP-CARRY`).
- Test delta: **+8**. Handoff: [CM_FIXA_STORY.md](handoffs/CM_FIXA_STORY.md).

#### CM-FIXA-INPUT — the input half (`48fe346`, merged `98e5e4a`)

- Six findings, under the orchestrator's rulings (a)–(e).
- **The pad-B half of the first finding did not hold on Godot 4.7.1.** The built-in `ui_cancel` is Escape only, and no pad button reaches `ui_accept` or `ui_cancel`. So a pad's B never cancelled the select, and no pad could press any GUI button anywhere. That gap had no owner, and the orchestrator added CM-PADUI for it (`FIX-PAD-MENU-ACCEPT-CANCEL`).
- Fixed:
  - `BeginLocalJoin` pre-claims no slot: the first device to make a join press is player 1.
  - The join press is consumed before the GUI sees it, and `ui_cancel` is routed to the pressing device's slot.
  - Bound pad axes count as activity, so LT (Block) and RT (Roll) move player 1.
  - A pause or focus loss forgets chord waits but keeps masks.
  - `InputBindingSet.RestoreUnreachableUltimateChord` restores a saved pad chord switched off that left the Ultimate no pad route.
  - The Mirror case measures one ground jump (186.4 px). Under a mutation it fails where the old case passed.
- Test delta: **+12**. Handoff: [CM_FIXA_INPUT.md](handoffs/CM_FIXA_INPUT.md).

#### CM-FIXA-PRES — presentation and telemetry (`974ccd2`, merged `2d44fa9`)

- Three findings:
  - The Fighter wind-up telegraph is derived from a live, free caster, so it drops on the KO tick and stays off through the stock-loss freeze.
  - The telegraph tint is never warmed by the hero aura: the Hard Mirror clone's wind-up is the canonical Unblockable red. The smother still applies.
  - Periodic ticks are neither hits nor predecessors. That covers status ticks, zone pulses, and drowning and stage-hazard DPS, which the environmental chokepoint's observation-only `periodicTick` marks. The new `xsrc_30_combat` figure leaves hazard contacts out on both sides.
- Left:
  - the gold hyper-armour outline on a KO'd armoured caster (gated later by CM-HIT, `VERIFY-FIGHTER-KO-HYPERARMOR-GLOW-TICK`);
  - the Hard clone's gold hero aura, a presentation ruling (`VERIFY-MIRROR-CLONE-HERO-AURA`).
- A harness caveat for later driver tests: `GameManager`'s boot session has `FighterOpponentType = Cpu`, so a `FighterSimulationDriver` harness that does not set the session runs P2 as the Normal CPU.
- Test delta: **+5**. Handoff: [CM_FIXA_PRES.md](handoffs/CM_FIXA_PRES.md).

**After the Phase A fixes: 3367 = 3332 + 35** (`2d44fa9`).

### Phase B

*Merge order: CM-HIT → CM-BLOCK → CM-MOVE → CM-CANCEL → CM-PADUI (the last added by the orchestrator; below). Early start from `cm-sim-a0`: no. All five branched together from `2d44fa9` (Phase A plus its review fixes, 3367 tests), not from the plan's 3332.*

**The usage-limit interruption.** All five Phase B agents were stopped by a usage limit mid-work.
- The orchestrator committed each uncommitted tree as a WIP checkpoint on its branch: CM-HIT `1b2586f`, CM-BLOCK `0be0b69`, CM-CANCEL `955450c`, CM-PADUI `a1aa580`, and one for CM-MOVE.
- A fresh agent resumed each in place. It audited the checkpoint against the dossiers, kept what was sound, fixed what was broken, built what was missing, and recommitted in logical pieces.
- Four branches did that with `git reset --soft 2d44fa9`, so their WIP commits are gone. CM-MOVE kept the checkpoint's tree as its first commit, `4d3c69d`, with a rewritten message (`git commit-tree`, tree unchanged).

**The merges.**
- **`85ce072`, after CM-MOVE's merge.** The first full run after `23fcd22` failed one case: CM-HIT's `StoryAirDodgeTests.BeingHitRefreshesTheAirDodgeSoItCanDodgeAgainAfterHitstun` tapped Jump. Since 6.1 a released Jump short-hops with no hang, so the hero landed before the dodge ended. The orchestrator's fix-up holds Jump through the fixture's setup. The assertions are unchanged, and so is the count.
- **CM-CANCEL's merge (`8647fd4`)** resolved conflicts in `FighterEntitySystems.cs`, `FighterKitRules.cs` and `FighterSimulationSystems.cs` (10 hunks). Every hit site CM-CANCEL moved to `ResolveFighterHit` keeps CM-BLOCK's victim-323 argument, and `ApplyIntent` and `ResolveForesightAnswer` take both branches' parameters.
- **`fa18442` (+1), the orchestrator's Phase B fix-up.**
  - CM-MOVE's ledge-attack hit site, built beside CM-BLOCK, passed no victim 323, so a ledge attack could never be perfect-blocked. It now does, pinned by `FighterLedgeGetUpTests.TheLedgeAttackCanBePerfectBlocked` (red without the fix).
  - CM-MOVE's measured one-line CPU change went in (a hanging CPU sends no stick, Roll or BasicAttack), with `CpuRecoveryMatrixTests.CpuClimbJumpOnlyEnforced = true`. CPU-driven Fighter hashes moved.
  - The Phase B review then found that this override zeroed a struck hanging CPU's DI; CM-FIXB-CPU gated it (`FIX-CPU-HANG-DI-ZEROED`).

#### CM-HIT — hit reaction and protection (`41806b0`, merged `bb5ba58`)

- One `ApplyHitReaction` call at the hitstun-end block carries the tumble change, the air-dodge refresh, the respawn-record expiry and the throw-grant decrement. It runs before the platform hold and before every branch that runs `TickCounters`. The plan placed the refresh separately, before `TickCounters`; the effect is the same on every branch.
- `SyncPresentationFeedback` was touched for the spawn glow and the hyper-armour gate. `CalibrationDrillRunner.Sample` and `FighterCpuController` were not: the drill rule is pinned by an in-place extension instead.
- Three cases beyond the declared:
  - the grabber's throw grant now ends with the throw (CM-FIXA-SIM's recorded item, `FIX-SIM-THROW-GRANT-OUTLIVES-THROW`);
  - no gold hyper-armour outline on a fallen fighter (`VERIFY-FIGHTER-KO-HYPERARMOR-GLOW-TICK`);
  - a Story owed-bounce case.
- A presentation limit: a roll or air dodge started in the protection's last 8 frames keeps the respawn record, so the spawn glow and "Respawning" outlast the respawn grant (`PROVISIONAL-RESPAWN-PROTECTION-CURVE`).
- Amended by CM-FIXB-SIM: a non-launching hit also ends a sim tumble during the launch's hitstun (value 1), as Story already did; and a defied hit writes no velocity.
- Test delta: **+23** (declared +20). Handoff: [CM_HIT.md](handoffs/CM_HIT.md).

#### CM-BLOCK — block (`bf61769`, merged `629a816`)

- The window decrement is one `FighterBlockTimingRules.BeginTick` per unfrozen tick, right after the Special cast clock. It is not in `TickCounters`, which five branches run and which takes no 323. The same call finalizes ticks the normal path never observes.
- Component 323 is threaded to every blockable hit site (one argument each), beyond the declared block-branch region, because the block branch cannot reach the victim's 323 otherwise.
- `BlockTiming` uses bits 17 and 18 (bookkeeping) and 20–23 (a perfect-block count read only by the flash and the Calibration Drill), inside CM-BLOCK's own field. §1.5 reserved bits 0–16.
- `ApplyEnvironmentalHit` has an `out int damageApplied` overload. Three Guard-Crush literals outside ownership became `GuardCrushBlockChargeCost`. `StoryBlockClassificationTests` gained the one-charge assertion its name promised.
- The perfect-block audit (one instrumented full run) found four fixtures that reached a perfect block. They were re-timed to test an ordinary block. The rollback convergence cases were left alone, so they now cover the new state.
- Amended later:
  - by `fa18442` (the ledge-attack site);
  - by CM-FIXB-SIM: a tick that starts in hitstun or daze finalizes the stance as dropped, so a hit-2 escape keeps no window;
  - by CM-FIXB-STORY: Story shieldstun runs through the hero's own Time Freeze; Time Freeze no longer pauses the Story window or lowered age (reversing §2.3.2 and CM_BLOCK.md line 44); and the invulnerability refusal runs before the hazard block.
- Test delta: **+22** (declared +22). Handoff: [CM_BLOCK.md](handoffs/CM_BLOCK.md).

#### CM-MOVE — movement (`ff42c31`, merged `23fcd22`)

- **The CPU did not stay climb-jump only**, against §2.5.2's premise (line 1178).
  - Recovery steer and neutral-combat presses decided before the grab reached the hang, re-armed the climb and climbed: over the three Open stages, Easy 38 get-ups in 170 hangs, Normal 15 in 135, Hard 6 in 173.
  - CM-MOVE owns no CPU code, so it held the drill's assertion behind `CpuClimbJumpOnlyEnforced = false` and measured a one-line fix: 0 get-ups on every stage and band.
  - The orchestrator applied that fix in `fa18442` (`VERIFY-CPU-LEDGE-GETUPS`, closed).
- `StoryAirPhysicsTests` pins `Band(0, false, true)` as `Rise` at exactly vy 0 (base gravity), not `Fall` as §2.6.1 wrote.
- `FighterLedgeGetUpTests` case 3 could not see protection end, because the chokepoint body was still empty on this base (CM-HIT fills it). CM-FIXB-SIM added the merged-seam test. CM-HIT's tumble suites were not run on this branch.
- `CpuRecoveryMatrixTests`' own drills model no ledge or platform pop, so the lip pop shows only in the real-simulation CPU ledge drill: one capture became a landing (Vesuvius Hard, 19 → 18 hangs).
- The extra case: a running Cleopatra's Desert Mirage startup holds X, so she covers 240 px from the press (`FIX-STORY-DESERT-MIRAGE-STARTUP-CARRY`).
- Amended later:
  - by CM-FIXB-SIM: the first-grab intangibility ends at every hang exit, so the climb jump and every get-up keep their vulnerable frames;
  - by CM-FIXB-STORY: Story Down+Jump in a swing, and a movement ability's hang reading Jump live;
  - by CM-FIXB-PADUI: the menu consume no longer drops Jump's release edge from the sim's short-hop latch.
- Test delta: **+37** (declared +36, plus the Cleopatra case). Handoff: [CM_MOVE.md](handoffs/CM_MOVE.md).

#### CM-CANCEL — D3 in Fighter, CPU and Mirror (`de1bb93`, merged `8647fd4`)

- The rules live in new files (`FighterSpecialCancel.cs`, `FighterMovementSystem.SpecialCancel.cs`), and CM-SIM's inline cast-region block became one call line.
- The hitstop-branch D3 feed sits after `ClearIfInterrupted`; CM-SIM's guide put it before. That way an interrupted cast's buffer empties at once. Both still come before the discard, as the plan requires.
- Lincoln's Splitting Strike is not reach-checked before the CPU presses it. The brief summarised 9.3 as including that check; the plan and the ruling ("**Leave for now** — only adapt it to the new Special timing (1.2) so it stays functional") do not. It is left for the user (`VERIFY-CPU-SPLITTING-STRIKE-REACH`).
- Additions, all input-side or tests:
  - the delivery-time drop (`CastHoldsThisTick`), which cut the CPU's presses into its own cast from 39 to 1 across the ten-stage smoke;
  - the CPU grab mirror's knockdown, tech-lock and A02 terms (CM-FIXA-SIM's recorded item);
  - D3 case 13, with `PlayerInputFrame.Create`-built input;
  - `CpuSelfActionStateTests` (3).
- Its smoke's perfect-block counts read 0, because CM-BLOCK was not on its branch.
- Amended later:
  - by CM-FIXB-SIM: neither recovery cancel ends Foresight while its counter strike is owed, in both modes;
  - by CM-FIXB-CPU: the recovery planner reads its own cast, and the hitstun hold lets recovery presses through.
- Test delta: **+22** (declared +18). Handoff: [CM_CANCEL.md](handoffs/CM_CANCEL.md).

#### CM-PADUI — pad menus, orchestrator-added (`2dfe5b8`, merged `740d712`)

- **Not in the plan.** CM-FIXA-INPUT found that on Godot 4.7.1 no pad button reaches `ui_accept` or `ui_cancel`, so a pad could press no GUI button. Decision 9.1 makes pad-only play first-class, and §5's pad session depends on it, so the orchestrator added this workstream. It shipped:
  - pad A on `ui_accept` and pad B on `ui_cancel` in `project.godot`, with the keyboard defaults kept and neither action remappable;
  - `InputManager.ConsumeHeldPresses`, so one press does one thing;
  - an audit of every `scripts/UI` menu reader;
  - player 1's ownership of the Fighter select's footer, a player 1 join prompt, and the controller-lost copy.

  Rows: `USER-PAD-MENUS`, `PROVISIONAL-PAD-MENU-RULES`, `FIX-PAD-MENU-ACCEPT-CANCEL`.
- It went beyond the brief's "what the audit requires" on `InputManager`: a new `InputManager.MenuPress.cs` holds `ConsumeHeldPresses`, `ConsumesMenuPress` and an `_Input` override that spends a confirm a focused button takes. `HubWorldController` gained six `ConsumeInteractPress()` call lines, in place of the checkpoint's 116-line restructure.
- The checkpoint's polled-input tests could not fail: they pressed after `await physics_frame`, which reaches that same tick's capture. The cases now press in idle time, and each was mutation-checked.
- A keyboard player 1's Down, Left and Right arrows now step the token and no longer leave focus in the footer.
- Amended by CM-FIXB-PADUI:
  - the consume spends press edges only;
  - a held chord shoulder is settled on the consume tick;
  - a Jump-shared confirm never counts toward hold-to-skip while gameplay runs;
  - the tree pause is shared with a gameplay-pausing dialogue;
  - another player's directional focus move is swallowed;
  - a disconnect raises `DeviceAssigned`.
- Test delta: **+18** (none declared; outside the plan's Phase B arithmetic). Handoff: [CM_PADUI.md](handoffs/CM_PADUI.md).

**Phase B merged: 3490 = 3367 + 123** (`740d712`). That is the five branches' 122 (the plan's four: declared +96, delivered +104; CM-PADUI 18) plus `fa18442`'s 1. The plan's declared target was 3428 = 3228 + 200 before any review fix. The difference of 62 is the Phase A fix round's 35, Phase B's 8 extra cases, CM-PADUI's 18 and `fa18442`'s 1.

### Phase B review fixes

**The review.** One adversarial review ran over the Phase B diff at `740d712`: six area reviewers (hit reaction, block, movement, cancel and CPU, pad UI, seams and test fidelity), two refuting checkers per finding.
- **28 findings.** 24 were confirmed by both checkers (2 high, 13 medium, 9 low). They are 20 distinct defects, because B0 / B20, B7 / B21, B8 / B19 and B14 / B22 (a) were each found twice.
- **4 were refuted** (one by both checkers, three by one of the two):
  - a damaging Rail Charge under respawn and Defy protection does not end it;
  - a first-grab grant inside the respawn window is read as respawn protection;
  - the Mirror adapter's last-recovery-frame exemption;
  - player 1's first Up wrapping onto Back.
- **All 24 went to four fix agents,** based on `740d712`.
- Record: `D:/Projects/genre_review_2026-10-13/phaseB_review.json`; fix inputs `fixB_inputs.json`.

#### CM-FIXB-SIM — the sim half (`5c3ff7d`, merged `33fc9b5`)

- **B1.** A non-launching hit ends a sim tumble inside the launch's hitstun (value 1) as well as after it, as in Story, and an owed slam bounce then drops. Recorded for F09: a juggled victim hit by a non-launcher lands in that hit's ordinary hitstun, with no tech or knockdown protection.
- **B2.** A defied hit writes no velocity, in any impulse form.
- **B5.** A tick that starts in hitstun or daze finalizes the block stance as dropped, so a hit-2 escape keeps no perfect-block window.
- **B8 / B19.** `FighterLedgeRules.EndFirstGrabIntangibility` ends the first-grab intangibility at every hang exit. A respawn grant shorter than 30 frames that merged into it ends with it.
- **B12.** Neither recovery cancel ends Foresight while its counter strike is owed, in both modes. The cancel comes one tick later; in the sim, only after a projectile catch.
- **B23.** A merged-seam test that the ledge attack ends respawn and Defy protection and keeps its own 8 climb frames.
- Left:
  - a defied blow still cancels the survivor's own roll or air dodge in the sim (`VERIFY-DEFY-CANCELS-OWN-ROLL`);
  - a merged respawn grant keeps 323 bit 0 through the hang, so the spawn glow and "Respawning" show for the ledge frames (`PROVISIONAL-RESPAWN-PROTECTION-CURVE`).
- Test delta: **+8** (declared +8). Handoff: [CM_FIXB_SIM.md](handoffs/CM_FIXB_SIM.md).

#### CM-FIXB-CPU — the CPU half (`703cf32`, merged `2fbfa6b`)

- **B0 / B20.** Once hitstun is over and the CPU is airborne or hanging, the hitstun-defense hold lets the planner's Jump and movement-ability presses through. Block stays held, and BasicAttack and Roll stay masked. The movement activation is charged at emission, so a press the hold masks is not charged (`FIX-CPU-HITSTUN-HOLD-EATS-RECOVERY`, `PROVISIONAL-CPU-HITSTUN-HOLD-PASS-THROUGH`).
- **B3.** `SelfDefyAvailable` reads `FighterDefenseRules.IsDefyUnspentAndUnbarred`, so Hard no longer keeps meter for a Defy that Sudden Death bars (`FIX-CPU-DEFY-RESERVE-IN-SUDDEN-DEATH`).
- **B9.** `fa18442`'s hang override runs only out of hitstun and daze, so a struck hanging CPU keeps its DI (`FIX-CPU-HANG-DI-ZEROED`).
- **B13.** The recovery planner waits out its own cast, and a planned recovery press delivered while the cast holds is dropped uncharged (`FIX-CPU-RECOVERY-PRESS-IN-CAST`).
- Left (`VERIFY-CPU-DELIVERY-RESIDUALS`):
  - a non-defending CPU's recovery press delivered into hitstun is still charged;
  - a combat movement press can still arrive inside its own cast (uncharged);
  - a hang-tick charge goes to an episode the grab has already closed.
- Test delta: **+6**. Handoff: [CM_FIXB_CPU.md](handoffs/CM_FIXB_CPU.md).

#### CM-FIXB-STORY — the Story half (`1472217`, merged `2ede072`)

- **B4.** Story shieldstun counts down through the hero's own Time Freeze; regen and the shatter lockout still hold. A blocked Extractor discharge no longer locks the hero in the stance until the thaw.
- **B6.** `TickPerfectBlock` runs every tick after the hitstop gate, so a Time Freeze no longer pauses the Story window or the lowered age. Hitstop still pauses both. This reverses an agent choice in §2.3.2 (line 913) and CM_BLOCK.md line 44.
- **B7 / B21.** `ApplyEnvironmentalHit` refuses an invulnerable hero (post-rewind, roll, tech, knockdown) before Defy, Aegis, the barrier and the block.
- **B10.** Story Down+Jump out of a swing's recovery or chain hold cancels the swing into a free `Airborne` drop. In startup and active frames it drops and keeps the swing, as the double tap does.
- **B11.** A movement ability's gravity reads Jump live for the apex hang (it read Jump as always held), matching the sim.
- Left:
  - the rest of `VERIFY-STORY-INVULN-LAYER-ORDER`: the hurtbox path still spends a block charge on a post-rewind-protected blocking hero, and `ApplyEnvironmentalDamage`'s unblockable penalties still spend Aegis or the barrier on an invulnerable one;
  - Story's double tap of Down is not read in the chain hold (`VERIFY-STORY-DOUBLE-TAP-CHAIN-HOLD`);
  - an aerial Special's gravity still reads the stale `_jumpHeld` (`VERIFY-AERIAL-SPECIAL-HANG-PARITY`).
- Test delta: **+6**. Handoff: [CM_FIXB_STORY.md](handoffs/CM_FIXB_STORY.md).

#### CM-FIXB-PADUI — the pad-menu half (`1570bee`, merged `14374d7`)

- **B14 / B22 (a).** The menu-press consume spends press edges only, so a Jump let go of during a pause reaches the sim as released.
- **B22 (b).** On the consume tick a held chord shoulder is emitted held at once (`UltimateChordWindow.Step(settleHeld)`), never as a late fresh press.
- **B15.** While gameplay runs under a non-pausing sequence, a confirm that is also Jump never counts toward hold-to-skip. Interact (the hint's button) and Enter still do.
- **B16.** The tree pause is shared by the pause menu and a gameplay-pausing dialogue (`DialogueManager.HoldsGameplayPause`). Each hands it back only once the other no longer holds it. Restart Level and Quit to Main Menu release it through `PauseMenuBase.ClosePauseForSceneExit`, so the scene load can run.
- **B17.** Another player's directional focus moves on the Fighter select are swallowed.
- **B18.** A pad disconnect raises `DeviceAssigned(slot, UnassignedDevice)`, so the join prompt returns.
- Left:
  - a Jump pressed in a pause and held through the resume keeps the sim's ×2.5 cut for that airtime, and `AssignDevice` drops a held Jump's release edge on a mid-air device switch (`VERIFY-SIM-JUMP-LATCH-EDGE-LOSS`);
  - Level 9's `NearCaptureBeat` is a third tree-pause owner (`VERIFY-NEAR-CAPTURE-PAUSE-OWNER`);
  - an Interact hold still arms hold-to-skip in a non-pausing bark (`VERIFY-BARK-INPUT-RULES`).
- Test delta: **+8** (declared +8). Handoff: [CM_FIXB_PADUI.md](handoffs/CM_FIXB_PADUI.md).

**After the Phase B fixes: 3518 = 3490 + 28** (`14374d7`). The four branches merged with no conflicts, and one full run read `Failed: 0, Passed: 3518, Total: 3518` (1 m 38 s) before the three confirming runs.

### Plan text the build no longer matches

The dossiers above keep their planned wording. These sentences are superseded:
- **§2.1.1 line 337**, "That matches the sim, where every kit step runs as a held jump". The sim reads the real latch, and Story's movement-ability hang now reads Jump live (CM-FIXB-STORY B11).
- **§2.1.6's test list, case 5**, "The cast holds its entry horizontal velocity, grounded and airborne". This predates Q1; a grounded cast brakes.
- **§2.3.1's *CPU* paragraph**, "its hitstun-defense Block hold already lasts until grounded, so it techs". This is true but incomplete: once hitstun ends, the hold lets the planner's Jump and movement presses through (CM-FIXB-CPU B0 / B20).
- **§2.3.2 line 913**, "hitstop's early return (:2135) and Time Freeze both pause it". Only hitstop does (CM-FIXB-STORY B6). CM_BLOCK.md line 44 says the same and is superseded too.
- **§2.5.2 line 1178**, "The CPU's delivered hang decision (Jump, with a neutral stick) therefore always climb-jumps … **The CPU stays climb-jump only (9.3).**" The first sentence was false in the code CM-MOVE found; the second has been true since `fa18442`.
- **§2.7.4's "Order" list.** It now begins with the invulnerability refusal (CM-FIXB-STORY B7 / B21).
- **§5 row 8**, "a running cast **sliding at its entry speed**, matching Story (Q1)". This predates the Q1 ruling: a grounded cast brakes over 4 frames in both modes.
- **§3.2's and §4.2's Phase B running totals** start from 3332. The real Phase B base was 3367.

## 10. Closeout report

Built on 2026-10-16 on worktree branches merged into `main` from `cfba295` to `14374d7`; the closeout gates ran on 2026-10-16 and 2026-10-17. §9 is the per-workstream record, and the ledger section "Confirmed open deviations — combat and movement update, 2026-10-16" (with its index) holds every row named here. **Nobody has played or looked at any of it.**

**Delivered.** Every decision is built, or recorded as ruled.
- **State and compatibility:**
  - One Klotho component was allocated: `FighterActionStateComponent` (**323**; nine ints, 36 B per fighter, 72 B per snapshot). No other ID was allocated.
  - No save-schema change (still v8).
  - Protocol still v4, with unchanged packet sizes. LAN is de-scoped to Package 7, so builds from before and after this update would desync there.
  - Every Fighter hash moved; no suite pins one.
- **1.1(1)** Story movement abilities fall under gravity except in kit-owned phases. Leonardo's boost peaks at 60.9 px and his glide sinks at exactly 40 px/s, Joan's Wings rise is unchanged, and every kit motion travels on its first active tick. `FIX-STORY-MOVEMENT-ABILITY-GRAVITY`, `PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`, `VERIFY-STORY-KIT-MOTION-FIRST-TICK`, `FIX-STORY-DESERT-MIRAGE-STARTUP-CARRY`.
- **1.1(1), Q7** Story's Rail Charge travels exactly 300 px from the press, standing or running, and stops at its recovery. `USER-RAIL-CHARGE-STOPS-LIKE-THE-SIM`, `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY`.
- **1.1(2)** Every accepted sim Special spends its cost at the press. An out-of-reach Splitting Strike plays its whole whiff, and it takes the tick from a same-tick movement press. `FIX-SPLITTING-STRIKE-FREE-WHIFF`.
- **1.1(3)** The sim landing tech locks every action from its landing tick, and its invulnerability ends with the lock (13 ticks counting the landing tick). `FIX-SIM-TECH-LOCK-ACTIONS`.
- **1.1(4)** A lethal Venom tick in Sudden Death respects `DefyBarred`. `FIX-SIM-VENOM-DEFY-BARRED`.
- **1.1(5)** The CPU sustains Jump for full hops. The Level 13 Mirror's ground jump peaks at 186.4 px, a held single jump's height; its whole two-press approach arc used to peak at 131.4 px. `FIX-CPU-JUMP-HOLD`.
- **1.2** Fighter Specials run their authored startup, active and recovery on component 323:
  - the effect fires on T+S;
  - the cast lock covers T through T+S+A+R−1;
  - the interrupt set (a hit, a grab, a knockdown, a KO) clears a cast;
  - a hand-built contract with no active frames runs 0 / 1 / 0.

  `USER-SIM-SPECIAL-TIMING`, `PROVISIONAL-SIM-SPECIAL-PHASE-RULES`. This closes `DEFER-SIM-SPECIAL-PHASES` and `SIM-SPECIAL-POSE-THIRDS` (with a residual: the thirds path stays for the fallback and the press-tick Ultimate edge). The two leaks it found are closed: `VERIFY-SIM-KNOCKDOWN-LANDING-TICK`, `VERIFY-SIM-A02-THAW-TICK`.
- **1.2, Q1** A grounded Special cast brakes over the 4-frame run-stop ramp in both modes, and an aerial cast keeps its drift. Story Specials no longer slide (they slid at 324 px/s). `USER-SPECIAL-CAST-BRAKE`.
- **1.3** Fighter Shield-Breakers keep the full shatter and gain their startup. A blocked one still leaves the attacker's first Ultimate press 32–45 frames before the dazed blocker can act. `USER-SHIELD-BREAKER-STARTUP`.
- **1.4** No combo scaling in Fighter (a record). `USER-FIGHTER-NO-COMBO-SCALING`.
- **2.1** The A02 wind-up shows a steady Unblockable-red tint and the X glyph at (0, −120) px for exactly its length, in Fighter and on the Mirror clone. It also plays the silent Critical cue `ultimate_activation_windup`. `USER-ULT-TELEGRAPH`, `PROVISIONAL-ULT-TELEGRAPH-LOOK`, `FIX-MIRROR-ULT-PHASE-EXIT`, `VERIFY-FIGHTER-KO-HYPERARMOR-GLOW-TICK`; closes `DEFER-ULT-TELEGRAPH`.
- **2.2** The D15 Ultimate totals are unchanged; the pad session decides. `USER-COMBAT-MOVEMENT-JUDGE-BY-HAND`, `VERIFY-ULT-ACTIVATION-TUNING`.
- **2.3** The sim refuses the Ultimate in the block stance, shieldstun, the tech lock, a Special cast, a knockdown and a busy A02 caster. `USER-NO-ULT-FROM-BLOCK`; closes `VERIFY-SAME-TICK-VERB-PARITY` (1).
- **2.4, Q2** A Story Ultimate is armoured for its own Startup by a Story rule; the `.tres` flags stay false. A StaticCharge hit no longer stuns through any armour, and the Rift Collapse launch respects it. `USER-STORY-ULTIMATE-ARMOR`, `USER-STORY-ULTIMATE-ARMOR-STARTUP-ONLY`, `PROVISIONAL-STORY-ULTIMATE-ARMOR`.
- **3.1** The held-Block landing tech is kept; 1.1(3) locks its frames. `USER-COMBAT-MOVEMENT-JUDGE-BY-HAND`.
- **3.2** Tumble outlives hitstun until an action or the landing, in both modes and for the Story hero. A non-launching hit ends it. Story mobs stay out. `USER-TUMBLE-AFTER-HITSTUN`, `PROVISIONAL-TUMBLE-EXIT-ACTIONS`.
- **3.3** Walls kept; the pad session looks for loops. `VERIFY-WALL-PIN`.
- **3.4, Q3** The perfect block, in both modes:
  - a Block raised 0–4 ticks before contact (a 5-frame window, `BasicComboRules.PerfectBlockWindowFrames`) costs no charge and sets no shieldstun;
  - a re-raise needs 20 lowered frames;
  - Shield-Breakers still shatter, and a Story Guard-Crush keeps its cost.

  `USER-PERFECT-BLOCK`, `USER-SHIELD-BREAKER-BEATS-PERFECT-BLOCK`, `PROVISIONAL-PERFECT-BLOCK-RULES`.
- **4.1 / 11.2** Shieldstun is `BasicComboRules.ShieldstunFramesFor(damage)` = 6 + damage / 4 (integer), clamped 6–16, in both modes. The flat 8 is deleted. `USER-SCALED-SHIELDSTUN`, `PROVISIONAL-SHIELDSTUN-CURVE`.
- **4.2, Q8** Fighter respawn protection is 120 frames at the first legal drop, shrinking linearly to 60 at the auto-drop. The first swing, Special, Ultimate or grab ends it, and 323 `ActionFlags` bit 0 records it. Story's post-rewind protection is untouched. `USER-RESPAWN-PROTECTION`, `USER-RESPAWN-PROTECTION-FIGHTER-ONLY`, `PROVISIONAL-RESPAWN-PROTECTION-CURVE`.
- **4.3** Defy protection ends on the survivor's first attack, Special, Ultimate or grab, in both modes, and a defied hit writes no velocity. `USER-DEFY-ENDS-ON-OFFENCE`.
- **4.4** No counter-hit reward in Fighter (a record). `USER-FIGHTER-NO-COUNTER-HIT`.
- **5.1, Q4, Q5** The sim gains Story's ×2.5 short hop through one constant (`UniversalMovementRules.ShortHopGravityMultiplier`). In both modes it cuts only a rise from the fighter's own Jump press this airtime. `USER-SIM-SHORT-HOP`, `USER-SHORT-HOP-MULTIPLIER-KEPT`, `USER-SHORT-HOP-OWN-JUMP-ONLY`, `PROVISIONAL-SIM-SHORT-HOP-LATCH`, `PROVISIONAL-SIM-SHORT-HOP-HEIGHT`. This closes the short-hop half of `DEFER-H7-STORY-TERMINAL`, which stays open, narrowed.
- **5.2** Fighter ledge get-ups exist (a neutral climb on a fresh tilt, a ledge roll, a ledge attack). A first grab is intangible for 30 frames, which end at every hang exit. The CPU stays climb-jump only. `USER-FIGHTER-LEDGE-GETUPS`, `PROVISIONAL-LEDGE-GETUP-FRAMES`, `VERIFY-CPU-LEDGE-GETUPS` (closed).
- **5.3** Any applied hitstun refreshes the air dodge, in both modes. `USER-AIR-DODGE-REFRESH-ON-HIT`.
- **5.4** The −20 % speed cut is kept; the pad session judges it. `USER-COMBAT-MOVEMENT-JUDGE-BY-HAND`, `USER-MOVEMENT-SPEED-CUT`.
- **6.1** The apex hang applies only while Jump is held, in both modes, movement-ability gravity included. `USER-APEX-HANG-HELD`.
- **6.2, Q6** Corner forgiveness:
  - in Story, a head-clip nudge of up to 12 px and a rising lip pop of up to 10 px;
  - in the sim, a 0.2 u one-way lip pop that resolves before the ledge capture;
  - the sim head-clip nudge stays a no-op, pinned by a geometry guard.

  `USER-CORNER-FORGIVENESS`, `USER-SIM-LIP-POP`, `PROVISIONAL-CORNER-FORGIVENESS-RULES`, `PROVISIONAL-SIM-LIP-POP`.
- **6.3** A double tap of Down (inside 18 ticks) and Down+Jump both drop through, in both modes. This closes M-18. `USER-DROP-THROUGH-BOTH-INPUTS`.
- **6.4** Both builds' jump spreads are kept (a record). `USER-COMBAT-MOVEMENT-JUDGE-BY-HAND`.
- **7.1** A non-Ultimate Special shakes the Story camera by the larger of its authored shake and the damage curve: the Emancipator shakes 8 px at accessibility scale 1. `USER-STORY-ABILITY-SHAKE`.
- **7.2** The re-hit counter is built and measured (below); the mercy ruling itself is pending. `USER-STORY-MERCY-MEASURE-FIRST`, `PROVISIONAL-REHIT-COUNTER-RULES`.
- **7.3** The campaign hero's Echo Step costs 30 × 0.35 = 10.5 (`StoryCombatRules.StoryEchoStepMeterCost`); every other build pays 30. `USER-STORY-ECHO-STEP-COST`.
- **7.4** A raised, front-facing block absorbs a Story hazard tick for one charge, through `PlayerController.ApplyEnvironmentalHit`, after the invulnerability refusal, Defy, Aegis and the barrier. `USER-STORY-HAZARD-BLOCK`, `PROVISIONAL-STORY-HAZARD-BLOCK-SOURCES`; closes `VERIFY-STORY-HAZARD-BLOCK`.
- **8.1** Story basics deal at least 6 to a standard mob (`StoryEnemyCombatRules.StandardBasicDamageFloor`), so every hero kills a Normal Chrono-Slasher in 5 or 6 hits. `USER-STORY-BASIC-FLOOR`, `PROVISIONAL-STORY-BASIC-FLOOR-ORDER`.
- **8.2** A prorated damage number floats dimmed (`UIPalette.DamageNumberReduced`, (0.66, 0.68, 0.74)) at ×0.8; Punish wins. `USER-DIM-REDUCED-NUMBERS`.
- **8.3** Hitstop 4–11 is kept; the pad session judges it. `USER-COMBAT-MOVEMENT-JUDGE-BY-HAND`, `PROVISIONAL-HITSTOP-WEIGHT`.
- **8.4** One named hurtbox value per mode, raw-identical: `FighterHurtbox.HalfExtents` (with the alias `FighterBodyOverlap.HalfExtents`) replaces ten sim copies, and Story derives its string-box anchor from `StoryCombatRules.HeroHurtboxHeightPixels` and names its body box (`CharacterFactory.HeroBodyBoxWidthPixels` / `HeroBodyBoxHeightPixels`). The target size stays open. `USER-HURTBOX-ONE-VALUE`.
- **9.1** Player 1 follows the last-used device, and the Local Versus select is press-to-join with no pre-claimed slot. Pads confirm with A and back out with B in every menu (the orchestrator-added CM-PADUI). `USER-LAST-USED-DEVICE`, `PROVISIONAL-DEVICE-FOLLOW-RULES`, `USER-PAD-MENUS`, `PROVISIONAL-PAD-MENU-RULES`, `FIX-PAD-MENU-ACCEPT-CANCEL`.
- **9.2** While the Ultimate is affordable, a lone LB or RB waits 3 frames for its partner (`UltimateChordWindow.LeniencyFrames`). The pad Ultimate is the live recipe chord, the Controls toggle is wired, and the pad direct slot is empty by default. `USER-PAD-ULTIMATE-CHORD-LENIENCY`, `PROVISIONAL-ULTIMATE-CHORD-LENIENCY`; closes `VERIFY-GAMEPAD-ULTIMATE-CHORD`.
- **9.3** The CPU and the Mirror read their own Special cast:
  - Special and Ultimate presses wait it out, and are dropped at delivery while it holds the tick;
  - no grab or Echo Step is scheduled during it;
  - the grab mirror also refuses during the tech lock, a knockdown and the CPU's own Ultimate.

  `USER-CPU-SPECIAL-TIMING-ONLY`, `PROVISIONAL-CPU-SPECIAL-TIMING`. The Phase B review's four CPU delivery fixes: `FIX-CPU-HITSTUN-HOLD-EATS-RECOVERY` (with `PROVISIONAL-CPU-HITSTUN-HOLD-PASS-THROUGH`), `FIX-CPU-DEFY-RESERVE-IN-SUDDEN-DEATH`, `FIX-CPU-HANG-DI-ZEROED`, `FIX-CPU-RECOVERY-PRESS-IN-CAST`.
- **9.4** Pad session only: no frame tables, combo enumerator or CPU-vs-CPU batch. §5 is the checklist. `USER-COMBAT-MOVEMENT-JUDGE-BY-HAND`.
- **10.1** The sim latches a swing's box facing at active start (305 `AttackFlags` 256). `USER-SIM-BOX-FACING-LATCH`; closes `VERIFY-SIM-STRING-FACING-PER-TICK`.
- **10.2–10.4** Sudden Death, the single air strike and the comeback stack are kept for the pad session. `USER-COMBAT-MOVEMENT-JUDGE-BY-HAND`, `USER-AERIAL-SINGLE-STRIKE`.
- **11.1** D3 is one table in `BasicComboRules` for both modes. The sim arms it on a landed direct hit (323 `SpecialFlags` 8 and `SpecialCancelBuffer`), and Foresight's owed counter strike holds it a tick. `USER-FIGHTER-SPECIAL-RECOVERY-CANCEL`, `PROVISIONAL-FIGHTER-SPECIAL-RECOVERY-CANCEL`.
- **11.2** is folded into 4.1; **11.3** is this plan and its phased workstreams.
- **The bots** model the held-Jump hang and both corner nudges: `PROVISIONAL-BOT-FLIGHT-MODEL`.

**Questions.** All eight were ruled on 2026-10-16, before Phase A; §9 quotes each ruling.
- **Q1** Brake to a stop, both modes. Not the default. `USER-SPECIAL-CAST-BRAKE`.
- **Q2** Startup only. The default. `USER-STORY-ULTIMATE-ARMOR-STARTUP-ONLY`.
- **Q3** Still shatters. The default. `USER-SHIELD-BREAKER-BEATS-PERFECT-BLOCK`.
- **Q4** Keep ×2.5 and measure every hero. The default. Every 1-tick tap is below 45 % (41.6–41.9 %): `USER-SHORT-HOP-MULTIPLIER-KEPT`, `PROVISIONAL-SIM-SHORT-HOP-HEIGHT`.
- **Q5** Own jump only, both modes. Not the default. `USER-SHORT-HOP-OWN-JUMP-ONLY`.
- **Q6** Add the Fighter lip pop. Not the default. `USER-SIM-LIP-POP`.
- **Q7** Stop it like the sim. Not the default. `USER-RAIL-CHARGE-STOPS-LIKE-THE-SIM`.
- **Q8** Fighter only, both levers. The default. `USER-RESPAWN-PROTECTION-FIGHTER-ONLY`.

**Tests.** 3228 → **3518** (+290):
- Phase A 104, of which A0 12 (CM-BOT 7, CM-STORY 21, CM-ULT 7, CM-INPUT 24, CM-SIM 45);
- its review fixes 35 (CM-FIXA-SIM 10, CM-FIXA-STORY 8, CM-FIXA-INPUT 12, CM-FIXA-PRES 5);
- Phase B 123 (CM-HIT 23, CM-BLOCK 22, CM-MOVE 37, CM-CANCEL 22, the orchestrator-added CM-PADUI 18, and the merge fix-up `fa18442` 1; `85ce072` changed no count);
- its review fixes 28 (CM-FIXB-SIM 8, CM-FIXB-CPU 6, CM-FIXB-STORY 6, CM-FIXB-PADUI 8);
- closeout fixes 0.

The `[TestCase]` attribute count under `tests/` equals the full-run total at every merge commit. The per-suite deltas are in each handoff's Tests section.

The three consecutive full runs on 2026-10-17 at `14374d7` (orphans drained before each, lock held): `Failed: 0, Passed: 3518, Total: 3518`, in 1 m 40 s, 1 m 34 s and 1 m 35 s.

**Rollback re-measure** (2026-10-16, `RollbackReadinessTests`; 300 depth-7 corrections per stage; 8.0 ms budget; lock held). The per-stage table belongs in `docs/PERFORMANCE_BASELINE.md`.
- **Worst case:** median 0.084 ms (`alexandria_chambers`) and p95 0.168 ms (`chicago_exposition`). The largest single correction was 0.704 ms (`orleans_vanguard`). **0 breaches** on all ten stages.
- **Largest snapshot:** 4,473 B (`globe_theatre`). It was 2,699 B on 2026-08-08, with 0.058 ms median and 0.104 ms p95, before Packages 11–13. `docs/PERFORMANCE_BASELINE.md` records no re-measure in between, so the growth cannot be split by package; component 323's own data is 72 B of it per snapshot.
- **CM-SIM's in-worktree reading,** after the phase machine and before Phase B: median 0.115 ms, p95 0.240 ms, 0 breaches, largest snapshot 4,429 B (`paris_bastille`).
- **Raw log:** `D:/Projects/genre_review_2026-10-13/closeout/rollback_readiness.log`.

**Reviews.** Each phase had one adversarial review with two refuting verifiers per finding, and each fix agent ran its new cases red against the unfixed code first. The exceptions (guard and coverage cases) are named in each handoff.
- **Phase A:** one round over the merged Phase A at `5434151`, not the plan's A1 and A2.
  - 29 findings: 15 confirmed by both verifiers, and 14 dropped (7 refuted by both, 7 split).
  - 22 were fixed by CM-FIXA-SIM, CM-FIXA-STORY, CM-FIXA-INPUT and CM-FIXA-PRES (+35 tests): the 15 confirmed, five split ones and two the orchestrator took although both verifiers refuted them.
- **Phase B:** one round over `740d712`.
  - 28 findings: 24 confirmed by both checkers (20 distinct defects), and 4 refuted.
  - All 24 were fixed by CM-FIXB-SIM, CM-FIXB-CPU, CM-FIXB-STORY and CM-FIXB-PADUI (+28 tests).
- §9 lists every finding taken or not.

**CPU smoke.** `FighterCpuSpecialTimingSmokeTests`, `CpuRecoveryMatrixTests` (Paris, Vesuvius, Nassau) and `FighterCpuBehaviorTests` are green in every full run since the merges, the three confirming runs included.
- **The Special timing smoke:** Hard vs Hard on all ten stages, production loadouts, items and hazards on, 1,800 ticks per stage (20 fighter slots), every replay tick-identical.
- **The merged build's counts** (`14374d7`, summed from the `[CM-CANCEL smoke]` lines in the third confirming run's Godot log; the other two runs' lines are identical):
  - 115 casts, 93 of which reached Active;
  - Specials answered in their startup: 4 by a block, 0 by an evade;
  - 52 blocks, of which 2 were perfect blocks (Lincoln on Paris, Leonardo on Berlin);
  - Lincoln's Splitting Strike: 4 casts, 2 whiffs, 0 interrupted;
  - presses into the CPU's own cast: 0.
- **CM-CANCEL's counts, from its branch before CM-BLOCK merged** (CM_CANCEL.md §6), are history: 109 casts, 98 of which reached Active (the other 11 ended in startup through the interrupt set); 4 answered by a block and 1 by an evade; Splitting Strike 4 casts, 1 whiff, 1 interrupted; 1 press into its own cast, against 39 before the delivery drop. Its perfect-block counts read 0, because CM-BLOCK was not on that branch.
- **Plan §5 row 11's CPU number** is 2 perfect blocks in 20 fighter slots (against 52 blocks); row 25's are the 4 startup blocks and 0 evades, and Lincoln's 2 whiffs in 4 Splitting Strikes.
- **The Open-stage ledge drill:** `CpuClimbJumpOnlyEnforced = true` since `fa18442`.
  - With CM-MOVE's one-line CPU change, 0 get-ups on every stage and band (CM-MOVE's measurement: Easy 266 hangs / 258 climb jumps, Normal 158 / 158, Hard 190 / 187).
  - Without it: 38, 15 and 6 get-ups.
  - CPU recovery numbers from before and after CM-FIXB-SIM's ledge fix are not comparable: a first-grab recovery now leaves by an ordinary, vulnerable climb jump.

**Bots** (`cmbefore*` against `cmafter*`, both in the clean `cm-bots` worktree; Levels 2, 6, 7, 12, 13; Joan and Lincoln; `play`; Easy and Normal; seeds 0–1). Before ran at the plan commit `735296f` and after at `14374d7`, 40 runs each, with the lock held, `-MaxSeconds 0` and `-ExtraArgs "--seed=N"`.
- Script: `D:/Projects/genre_review_2026-10-13/scripts/cm_bots_after.ps1`. Comparison: `scripts/cm_bots_compare.py`, output `closeout/bots_compare.json`.
- Damage by source is the report's `player.damage_by_source`. `env` and `level_NN.*` keys count as hazard, and `extractor:*` keys as Extractor discharge.

| | Easy before | Easy after | Normal before | Normal after |
|---|---|---|---|---|
| Completed (of 20) | 17 | 19 | 9 | 12 |
| … per seed (s0, s1) | 8, 9 | 10, 9 | 4, 5 | 7, 5 |
| Boss down | 17 | 19 | 9 | 12 |
| Deaths per run (per seed) | 1.55 (1.5, 1.6) | 1.20 (1.2, 1.2) | 4.05 (4.2, 3.9) | 3.00 (2.9, 3.1) |
| Damage taken per run (per seed) | 251.8 (236.8, 266.9) | 229.5 (233.8, 225.2) | 450.4 (465.5, 435.4) | 377.6 (368.0, 387.2) |
| Hazard damage, total (per run) | 3110 (155.5) | 2716 (135.8) | 3646 (182.3) | 2499 (125.0) |
| Hazard share of damage taken | 61.7 % | 59.2 % | 40.5 % | 33.1 % |
| Extractor discharge damage, total | 407 | 276 | 473 | 91 |
| Enemy damage, total | 1520 | 1598 | 4890 | 4962 |
| `stuck_lock` endings | 2 | 0 | 1 | 2 |
| Median seconds of a completed run | 154.6 | 150.1 | 170.2 | 178.6 |
| Endings | 17 complete, 2 `stuck_lock`, 1 collapse | 19 complete, 1 collapse | 9 complete, 10 collapse, 1 `stuck_lock` | 12 complete, 6 collapse, 2 `stuck_lock` |

Completions by level moved only here:
- Easy Level 7: 1/2 (Joan) and 0/2 (Lincoln) before, 2/2 and 2/2 after.
- Easy Level 12, Joan: 2/2 before, 1/2 after (seed 1 collapsed after 6 deaths).
- Normal Level 2, Lincoln: 1/2 before, 2/2 after.
- Normal Level 13, Lincoln: 0/2 before (both seeds Smothered at 14 deaths against the Mirror), 2/2 after.

Knockdown and tech counts (§4.3 item 5, "if the telemetry shows them") were not tabulated.

*The reading,* by CLAUDE.md's comparability rule (a change is real only when the per-batch ranges do not overlap; two batches per side is thin evidence):
- **Completions:** the ranges overlap on both difficulties (Easy 8–9 → 9–10, Normal 4–5 → 5–7), so the +2 and +3 are not shown to be real.
- **Deaths and damage taken per run** fall on both difficulties with **no overlap**. This is the one clear change.
  - Easy: deaths 1.5–1.6 → 1.2; damage 236.8–266.9 → 225.2–233.8.
  - Normal: deaths 3.9–4.2 → 2.9–3.1; damage 435.4–465.5 → 368.0–387.2.
  - Most of it is hazard and Extractor damage, while enemy damage is flat. On Normal, hazard damage per run falls 182.3 → 125.0 and Extractor discharge 473 → 91 in total, against enemy damage 4890 → 4962. On Easy, hazard damage falls 155.5 → 135.8 per run.
  - That is consistent with 7.4 (Story hazards blockable), because the `play` bot already blocks telegraphed swings and braces hazards with Block. Nobody has checked it beyond these numbers.
- **Not comparable by construction:** run time, and the short-hop and hang flights (6.1, 6.2). Only the after batch carries the `rehits` block.

*The `stuck_lock` endings.*
- **Before:** all three are Level 7's boarding lock (Easy Joan s0, Easy Lincoln s0, Normal Lincoln s1), the known skiff-channel bot gap from the 2026-10-12 update.
- **After:** two, both **Level 7's quay lock, Lincoln, Normal, seeds 0 and 1, with 0 deaths**. This is a new pattern. A fine-trace probe of the same build (`--trace-every=3`, tag `cmprobe`) reproduced the loop frame for frame:
  - The last quay member on Normal is a `laser_pistol_deckhand` at the launch yard's west end (3330, 672; `Level07Controller.LaunchYardY` 672, yard 3300–3600). Easy's per-row prefix drops it.
  - The before bot reached it with one planned double jump from the 2950/820 step (812 → 672, take-off 3028). The after planner never plans that edge, on either seed.
  - The likely reason: CM-MOVE's flight model now releases the first jump at its apex before a planned air jump, and since 6.1 the hang needs Jump held, so the modelled double jump is shorter. That is inferred from the plan strings, not traced in the planner.
  - Instead the bot loops two flights that fail in execution. (a) The 812 → 752 single hop onto the 3200/760 step: it takes off about 90 px early (2935 against 3028), bonks its head under the 2900/640 ratline and falls short to the deck. (b) A deck → 752 double jump planned from under the launch yard (take-off 3360, heading west): it arrives eastbound and starts from a standstill, so it lags the plan, and its air jump at x 3320 bonks on the yard's underside.
  - **This is not a content block.** The human route (deck → 2950/820 → 3200/760 → yard) needs at most 80 px of rise per step, well inside Lincoln's single jump (about 133 px), and Joan clears the quay lock on Normal in every run.
  - It is a bot gap exposed by 6.1, owner Unassigned, recorded in `PROVISIONAL-BOT-FLIGHT-MODEL`. Level 7 is the only level where it appeared.

*Re-hits* (CM-BOT's telemetry, after batch only, 20 reports per difficulty; no before baseline exists, because the telemetry arrived after `735296f`):

| | Easy after | Normal after |
|---|---|---|
| Hits taken | 400 (4590 damage) | 542 (7552 damage) |
| Combat hits | 232 | 437 |
| Cross-source within 30 frames (`xsrc_30`) | 11 (2.8 % of hits; 160 damage) | 31 (5.7 %; 366 damage) |
| … of which combat (`xsrc_30_combat`) | 1 (4 damage) | 25 (254 damage) |
| Same-source within 30 frames (`same_30`) | 35 | 55 |
| Same-frame cross-source | 0 | 0 |
| By kind (hits / damage / `xsrc_30`) | boss 118 / 1318 / 0; hazard 168 / 2662 / 9; projectile 50 / 195 / 2; standard 5 / 25 / 0; mirror 59 / 390 / 0 | boss 136 / 2671 / 0; hazard 105 / 1625 / 5; projectile 126 / 1300 / 14; standard 38 / 454 / 4; elite 34 / 521 / 8; mirror 103 / 981 / 0 |

Most `same_30` hits are the Mirror clone's own strings (Easy 30 of 35, Normal 51 of 55). These are the numbers the 7.2 ruling waits on (`USER-STORY-MERCY-MEASURE-FIRST`).

**Smoke runs** (2026-10-16, closeout code `14374d7`, main checkout, lock held). All exit 0 with no `SCRIPT ERROR`:
- `MainMenu.tscn` (`--quit-after 300`);
- `HubWorld.tscn` (300);
- `Level_00_Tutorial.tscn` (300);
- `Level_07_Nassau.tscn` (300);
- `Level_13_ChronalVoid.tscn` (300);
- `TestArena.tscn` (1800);
- `FighterStage_Paris.tscn` (300).

Levels 0, 7 and 13 print `6 ObjectDB instances were leaked` / `3 resources still in use at exit`, CLAUDE.md's nondeterministic exit noise. **Control:** `Level_07_Nassau.tscn --quit-after 300` at the plan commit `735296f`, in the clean `cm-bots` worktree before its checkout to `14374d7`, exits 0 and prints the identical pair. The noise predates this package. The main checkout still holds the user's uncommitted edits (the plan's pre-flight list), so these smokes ran with them; the bot batches ran in the clean worktree and do not.

**Not delivered, and the honest list.**

- **Nobody has played or looked at any of it.** Every acceptance claim is a headless suite, a smoke run or a bot run. Nobody has seen any of these:
  - a Fighter Special's startup or whiff, the cast brake, the half-gravity float;
  - the telegraph, the perfect-block flash, the join prompts;
  - a tumble exit, a ledge get-up, a short hop, a released hop's peak;
  - a corner nudge or either lip pop;
  - a pad menu press;
  - a Story Echo Step at 10.5, a blocked hazard tick, a dimmed number.

  §5's 25 rows are the checklist the first pad session owes.
- **Open questions for the user** (none blocks the merge):
  - Should the CPU reach-check Lincoln's Splitting Strike, or any melee Special, before pressing it? 9.3 left kit awareness out. `VERIFY-CPU-SPLITTING-STRIKE-REACH`.
  - Pad A (Jump) now pages non-pausing dialogue lines, as keyboard Space and attacking always did. Should jumping or attacking page barks at all, and should an Interact hold arm hold-to-skip in one? `VERIFY-BARK-INPUT-RULES`.
  - A defied hit still cancels the survivor's own roll or air dodge in the sim; Story does not. `VERIFY-DEFY-CANCELS-OWN-ROLL`.
  - Every 1-tick short-hop tap measures 41.6–41.9 % of the held apex, below the decided "about 45–55 %". Q4 said record it, do not tune. `PROVISIONAL-SIM-SHORT-HOP-HEIGHT`.
  - The Hard Mirror clone wears the gold hero aura. `VERIFY-MIRROR-CLONE-HERO-AURA`.
  - The telegraph is built steady, where the ruling said "red flash". `USER-ULT-TELEGRAPH`, `PROVISIONAL-ULT-TELEGRAPH-LOOK`.
  - The 7.2 mercy window itself, on the re-hit numbers above. `USER-STORY-MERCY-MEASURE-FIRST`.
- **Still open in the build, each with its row:**
  - **Same-tick and layer order.** Story fires a Special's effect on T+S−1 and the sim on T+S, and the sim accepts an Ultimate on the tick Block is released (`VERIFY-SAME-TICK-VERB-PARITY` (8), (9)). The rest of `VERIFY-STORY-INVULN-LAYER-ORDER`: the hurtbox path spends a block charge on a post-rewind-protected blocking hero, and `ApplyEnvironmentalDamage`'s unblockable penalties spend Aegis or the barrier on an invulnerable one.
  - **Jump and hang.** The sim's aerial cast step keeps the old hang, and Story's aerial Special reads a frozen `_jumpHeld` (`VERIFY-AERIAL-SPECIAL-HANG-PARITY`). A Jump pressed in a pause and held through the resume keeps the sim's ×2.5 cut for that airtime, and `AssignDevice` drops a held Jump's release edge on a mid-air device switch (`VERIFY-SIM-JUMP-LATCH-EDGE-LOSS`). Story's double tap of Down is not read in the chain hold (`VERIFY-STORY-DOUBLE-TAP-CHAIN-HOLD`).
  - **CPU.** Four wasted or wrongly charged presses remain (`VERIFY-CPU-DELIVERY-RESIDUALS`): a recovery press into hitstun, still charged, for a non-defending CPU; a combat movement press inside its own cast; a press on a frozen last recovery frame; and a hang-tick charge to a closed episode.
  - **Pause ownership.** Level 9's `NearCaptureBeat` pauses the tree itself, so a Story pause over it still unpauses the world on Resume (`VERIFY-NEAR-CAPTURE-PAUSE-OWNER`).
  - **Respawn presentation.** A merged respawn grant shorter than 30 frames keeps 323 bit 0 through a hang, and a late roll or air dodge keeps the record, so the spawn glow and "Respawning" can outlast the grant (`PROVISIONAL-RESPAWN-PROTECTION-CURVE`).
  - **Physics and geometry.** The sim snaps a fighter onto a floor segment from any depth (`VERIFY-SIM-PIT-FLOOR-SNAP`; `VERIFY-PARIS-DEEP-PIT-RECOVERY` is still held). Walls are judged on pads (`VERIFY-WALL-PIN`). `DEFER-H7-STORY-TERMINAL` keeps the launch terminal, the launch gravity and Story's launch-band short-hop clause. The hurtbox sizes stay open (`VERIFY-SIM-HURTBOX-WIDTH`, `VERIFY-STORY-HURTBOX-UNIVERSAL`), and Story mobs still have no tumble or knockdown (`VERIFY-M05-KNOCKDOWN-SCOPE`).
  - **Story armour edges** (`PROVISIONAL-STORY-ULTIMATE-ARMOR`). A hitless StaticCharge can still stun through armour, and the Rift Collapse still pulls an armoured body.
  - **Bots.** Level 7's quay lock stalls the Lincoln `play` bot on Normal (`PROVISIONAL-BOT-FLIGHT-MODEL`). Bot batches from before and after this package are not comparable for run time or flight.
- **Presentation left as placeholder or unbuilt:**
  - The telegraph's cue is silence until Package 10, and under a LAN rollback across the wind-up's first tick it could play twice or not at all (LAN is de-scoped).
  - No off-screen indicator for a caster outside the view.
  - The perfect-block flash is a placeholder tint on the hit-flash channel.
  - The join prompts are text only, with no device glyphs.
  - D3 in Fighter has no pose, flash or HUD cue of its own.
  - The Story HUD and Move List show no Echo Step cost.
- **Pad-menu limits** (`PROVISIONAL-PAD-MENU-RULES`):
  - pad B on an open `OptionButton` popup could not be verified headless;
  - a pad-only player cannot cancel a pad-slot listen in Settings;
  - a joined keyboard player 2's Enter still presses player 1's focused footer button;
  - the consumed tick drops a press the other player makes on that exact tick.
- **Measurement gaps:**
  - The rollback gate fails on any single correction over budget (`budgetBreaches.Count == 0`), so a preemption on a shared machine can fail it. Two workstream full runs (CM-BOT; CM-SIM commit 5) saw single breaches that passed alone; the closeout run had none.
- **Out of scope, as §8 decided:** Story's `LedgeGrabPoint` get-ups; Story mob tumble and knockdown; CPU kit awareness and its use of D3, the ledge get-ups and the perfect block; device glyphs; per-character hurtboxes; a protocol or save-schema change; Ultimate retunes; the docs-3 copy of the ledger.
- **Other closeout steps.** The design master and mirror, the contract pairs, `AGENTS.md`, `CLAUDE.md` and `docs/PERFORMANCE_BASELINE.md` are §7's separate steps 5–6. The ledger index lists every design and contract line each ruling requires, including the `DOC-MASTER-UNBOUND-BINDING` line `:2286`, which is edited by hand in each copy.
