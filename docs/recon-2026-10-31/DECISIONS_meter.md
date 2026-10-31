# Meter and Defy History rework — decisions (started 2026-10-30)

The user's request (2026-10-30): "Defy history seems worthless and unremarkable, you dont really notice it happen and you usually just immediately die right after. I think we should probably remove it OR do a rework of how it works and what it does. List out for me all of the special functions that affect ultimate meter that is currently in the design and in the implementation and then lets take a pass over all of these for a rework."

## Current state (inventory, 2026-10-30, build at main 56031750)

The design and code inventories were taken by two read-only agents on 2026-10-30; the summary is below.

Gains:
- Damage dealt: +1/HP. The Story hero gets ×0.35 (D5); the design mirror still says 1.0.
- Damage taken: +0.25/HP on the permanent share only.
- Rally echo: paid out as it drains.
- Shield of Orléans: +5 flat per blocked attack.
- Resonance Surge orb: +15 in Fighter only, behind the Meter pickups toggle, which is off by default.
- Story MeterBoost orb: nothing spawns it.
- Level 0 lesson: direct set to 100.
- Mozart's Minor Resonance: ×1.1.
- Ultimate-origin damage earns its caster 0.

Sinks:
- Ultimate: −100 at acceptance.
- Defy History: meter to 0.
- Fighter stock loss: keeps 75%.
- Sudden Death: meter to 0.
- Extractor discharge: −20, even when blocked.
- Level 4 searchlight: −5/s.
- Eraser's Siphon Snare: −10/s, capped at 30 per cast.
- Paris Dampening Beam: −2.5 per 30 frames.
- Fresh level entry or Restart: meter to 0.
- Level 0 hold: drained to its baseline.

Story's flat effects are not scaled by the ×0.35, so they weigh about 3× more there.

Defy History today (design D04 + decision 4.3):
- Trigger: a lethal hit at exactly 100 meter, once per Story level attempt or once per Fighter match.
- Effect: the meter goes to 0, the hero survives at 1 HP and the hit's effects are released.
- Protection: 60 frames from the first actionable tick, ended early by the first attack, Special, Ultimate or grab.
- Why nobody notices it:
  - Both cues are silent placeholders.
  - There is no aura.
  - The damage number reads previousHP − 1, like an ordinary big hit.
  - Story gets a 12-frame hero hitstop plus a shake; Fighter gets no shake and no audio.
  - Mobs keep attacking.
- Bots: about 0.46 procs per Normal `play` run.

## Round 1 — Defy History's shape (2026-10-30)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| 1.1 | What happens to Defy History? | **Active burst**: a button the player presses, usable while being hit. It breaks out of hitstun, blasts nearby enemies away and grants brief invulnerability. It replaces the passive lethal-hit save. | yes |
| 1.2 | Cost | **Half a bar (50 meter)** | yes |
| 1.3 | Modes | **Both modes, same rule** | yes |
| 1.4 | Payoff beyond surviving | **Shockwave + some HP back** (a radial knockback and stagger blast plus a heal) | no (recommended: shockwave + protection, no heal) |

Follow-ups opened by round 1:
- input
- when it is legal
- limits
- whether any passive save survives
- heal size
- shockwave numbers
- Ultimate interplay
- Sudden Death
- HUD (the four-state seal no longer fits)
- the Level 0 lesson
- the CPU's use
- Story's ×0.35 making 50 meter about 143 damage dealt

## Round 2 — Defy input and limits (2026-10-30)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| 2.1 | How is it triggered? | **Its own remappable button**: a new action. | no (recommended: the grab chord while being hit) |
| 2.2 | When is it legal? | **Any time**: from neutral too, so in Fighter it is an invulnerable reversal. | no (recommended: while hit, grabbed or blocking) |
| 2.3 | Limits besides meter | **Meter only**: usable whenever the meter is at 50 or more; no per-level or per-stock cap and no cooldown. | yes |
| 2.4 | Heal | **Cash in the Rally echo pool**: heal the HP still in the echo pool, with a floor of 10 % of max HP. | yes |

Free default bindings, checked in project.godot on 2026-10-30:
- Pad: only Back (button 4) is unused. Guide (5) is the system button. The triggers are Block and Roll, and the d-pad is movement.
- Keyboard: F is free.

## Round 3 — Defy frames, blast, Ultimate interplay, default button (2026-10-30)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| 3.1 | Invulnerability / punishability | **Invulnerable startup, punishable recovery.** Provisionally about 6 fully invulnerable startup frames, the blast on frame 6, then about 24 vulnerable recovery frames. | yes |
| 3.2 | What the blast does | **Push + stagger, no damage, unblockable.** Provisionally a radius of about 2.5 u and a stun of about 30 frames. It earns no meter, gives no Rally echo and cannot kill. | yes |
| 3.3 | Versus an Ultimate | **Only before the hold**: legal during an Ultimate's 20-frame wind-up, where its invulnerability beats the strike, but not once the activation hold has connected. | no (recommended: it breaks the hold) |
| 3.4 | Default pad button | **Defy on R3**, with the Dialogue Log moved to Back/View; keyboard F. | yes |

## Round 4 — Defy presentation, HUD, bosses, CPU (2026-10-30)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| 4.1 | Presentation | **Full time-break**: see the list below. | yes |
| 4.2 | HUD | **A notch at 50 on the meter**: it lights gold once the meter is at 50 or more and the action is bound. The four-state Defy seal is retired. | yes |
| 4.3 | Story bosses | **Interrupt and push, no stun**: the blast cancels the boss's current telegraph or attack and pushes it back (except knockback-immune bosses). | yes |
| 4.4 | CPU / Mirror clone | **By band**: Easy never; Medium sometimes, to break long strings; Hard breaks strings, escapes corners and beats an Ultimate wind-up, otherwise keeping its meter for the Ultimate. The Mirror clone follows its band. | yes |

What the full time-break (4.1) includes:
- an ~8-frame world freeze on everyone in range
- a gold expanding shockwave ring
- a comfort-gated desaturate-and-snap screen pulse
- a "DEFIED" world label in place of the damage number
- the meter visibly losing half
- a named Critical cue slot, silent until Package 10

## Round 5 — Defy loose ends (2026-10-30)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| 5.1 | Level 0 lesson | **Teach the button.** See below. | yes |
| 5.2 | Sudden Death | **Allowed if you earn 50.** There is no special rule; meter starts at 0 in Sudden Death, and `DefyBarred` goes away. | yes |
| 5.3 | Story unlock | **From Level 0.** Defy is a universal verb like Block and Rally, never gated by the Legacy Unlock Schedule. | yes |
| 5.4 | Gates | **Ignores the ability lockout and Suppression, and does not arm the lockout.** It is refused during Time Freeze, like the Ultimate. | yes |

The Level 0 lesson (5.1):
- The meter is set to 50.
- A drone swarm traps the hero in a string.
- The step completes only once the player presses Defy, and then shows the blast and the heal.
- The first-use tooltip names the button.

The name stays "Defy History" (assumed; the user was told and can override it).

## Round 6 — meter rates (2026-10-30)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| 6.1 | Story gain scale | **Raise ×0.35 → ×0.5.** It amends D5 (`USER-STORY-METER-SCALE`). At ×0.5 a Story Defy takes about 100 damage dealt and an Ultimate about 200. | yes |
| 6.2 | Story flat effects | **Scale them by the Story gain factor.** See below. | yes |
| 6.3 | Damage-taken rate | **Keep 0.25 per HP.** | yes |
| 6.4 | Fighter stock-loss retention | **Keep 50 %.** It replaces 75 %, so a full bar becomes exactly one Defy. `InfluenceMeterRules` stock-loss retention goes 3/4 → 1/2; design M09 says 25 % penalty. | no (recommended: keep 75 %) |

Scaled Story flat effects (6.2), at ×0.5:

| Effect | Before | After |
|---|---|---|
| Extractor discharge | −20 | −10 |
| Level 4 searchlight | −5/s | −2.5/s |
| Siphon Snare | −10/s, cap 30 | −5/s, cap 15 |
| Shield of Orléans | +5 | +2.5 |

The same factor applies to any other Story flat grant or drain on the campaign hero. Direct sets (the Level 0 lesson) and spends (the Ultimate's 100, Defy's 50) are not scaled.

## Round 7 — pickups, Joan's perk, Ultimate meter (2026-10-30)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| 7.1 | Resonance Surge orb (Fighter, +15) | **Keep it, off by default.** No change. | yes |
| 7.2 | Story `MeterBoost` orb effect (never spawned) | **Keep it for later.** No change. | no (recommended: delete the dead path) |
| 7.3 | Shield of Orléans | **Defer to Joan's kit pass.** It stays scaled per 6.2 until then. | yes |
| 7.4 | Ultimate-origin meter | **Keep at zero** (D03h unchanged). | yes |

## Not asked, carried by default

These items are unchanged unless the user says otherwise:
- **Paris Dampening Beam:** −2.5 per 30 frames, unchanged. The Fighter is unscaled.
- **Mozart's Minor Resonance (×1.1):** deferred to Mozart's kit pass.
- **Level 0 hold drain:** unchanged.
- **Siphon Snare's design clause "can disarm an unused full-meter Defy":** moot under the active Defy. It is deleted from the design, and the drain itself follows 6.2.

Clean-ups that come with the rework:
- Delete the dead `PlayerController.ApplyStockLossMeterRetention` / `UltimateMeter.ApplyStockLossRetention` Story path (it has no caller), unless a test pin needs it rewritten.
- Remove the retired Echo Step steps that `TEMPORAL_STATE_CONTRACT.md` still describes.
- Amend the design master and mirror for D5: they still say 1.0 per HP for Story.

## Provisional numbers (agent-chosen, to be judged by hand)

- Defy's frames:
  - about 6 invulnerable startup frames
  - the blast on frame 6
  - about 24 vulnerable recovery frames
- The blast: about 2.5 u radius and a 30-frame stagger.
- The world freeze: about 8 frames.
- The heal floor: 10 % of max HP.

## Direction change — the Special resource (2026-10-31)

The user proposed replacing Special cooldowns with a meter, and asked whether one bar for everything would make Ultimates too hard and whether two bars confuse players. The trade-offs discussed:
- One shared bar starves the Ultimate.
- Fighting games that run two gauges (SF6 Drive + Super, Guilty Gear Burst + Tension, SF4 Super + Revenge) read clearly when each gauge has one job.
- One-bar fighters keep base Specials free.

**Ruling: two gauges.** The Influence meter stays Ultimate-focused, and a new Special gauge replaces Special cooldowns. The details are settled in the rounds below. Defy's cost (round 1.2, 50 Influence) reopens with them.

### Today's numbers (Explore survey, 2026-10-31)
- **Special cooldowns** range from 7 to 13 s:
  - Einstein 11 / 8, Joan 10 / 11, Leonardo 8 / 10
  - Lincoln 13 / 12, Cleopatra 10 / 9, Tesla 8 / 11
  - Shakespeare 7 / 7, Mozart 7 / 12, Tubman 9 / 10
- **Movement abilities** are all 5 s.
- **Momentum** refunds 20 frames for string hits 1–2 and the directional strikes, 60 for the finisher, capped at 120 per slot per cycle.
- **Kit cooldown rules:**
  - Requiem Chord shaves 2 s off Fortissimo.
  - Sonata Drift's staff refunds 50 % of the movement cooldown.
  - Extra Note adds a second movement charge.
  - Cleopatra's Vortex Step halves Sandstorm Vortex's cooldown.
- **Grid cooldown minors:** `joan_piercing_cooldown`, `mozart_wave_cooldown` and `lincoln_splitting_cooldown`, each −10 %.
- **Sim storage:** component 305 holds the three cooldown frame fields.

## Gauge round 1 — shape, cost, refill, movement (2026-10-31)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| G1.1 | Shape | **4 pips**, shared by Special 1 and Special 2, filling continuously within each pip. | yes |
| G1.2 | Cost | **1 pip for every Special.** | yes |
| G1.3 | Refill | **Slow: passive regen of 1 pip per 12 s, plus landed hits.** String hit 1 +0.05, hit 2 +0.05, finisher +0.2, so a full landed string is 0.3. | yes (second ask) |
| G1.4 | Regen pause after a spend | **No pause.** | no (recommended: 3 s) |
| G1.5 | Movement ability | **Keeps its own 5 s cooldown**, outside the gauge. | yes |

How G1.3 was settled:
- First ask: the user rejected the recommended rate (1 pip / 5 s, string 0.7) because it "would seem to guarantee always having at least 1 pip all the time allowing for special spamming".
- Second ask: the slow rate above, which works out to about one Special per 12 s idle and one per 5 s while hitting constantly (today: about one per 5 s idle, one per 4 s at best).
- Still open: the gain for the directional strikes (Up-Attack, Down-Air, air strike). The default is to treat them as string hits, +0.05 each.

## Gauge round 2 — Defy cost, lockouts, reuse delay, Special refill (2026-10-31)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| G2.1 | Defy cost | **50 Influence, as ruled in 1.2.** Defy stays on the Influence meter and competes with the Ultimate; the Special gauge pays for Specials only. | no (recommended: 2 Special pips) |
| G2.2 | Lockouts | **Keep both**: the 30-frame ability lockout with the D1 hit-confirm release, and the 30-frame Special → Special lockout (S). | yes |
| G2.3 | Same-move reuse | **A 2 s per-slot reuse delay.** Using Special 1 locks Special 1 alone for 2 s; Special 2 stays free. | yes |
| G2.4 | Do landed Specials refill the Special gauge? | **No, only basics refill it.** Specials still earn Influence as today. | yes |

Consequences of G2.1:
- The four-pip gauge is Specials only.
- The HUD notch at 50 Influence (4.2) stands.
- Round 6's Story ×0.5 and 50 % stock-loss retention keep their Defy reasoning.

## Gauge round 3 — lifecycle (2026-10-31)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| G3.1 | Start fill (Story level attempt / Fighter match) | **Half: 2 pips.** | no (recommended: full) |
| G3.2 | Fighter stock loss | **Keep it as it was.** | yes |
| G3.3 | Story death rewind / reload / Collapse / Snap | **Keeps its value, persisted like the meter.** It replaces the persisted Special cooldown timers in the save. A fresh entry or a full Restart sets the start fill (2 pips). | yes |
| G3.4 | Mode rates | **Same in both modes.** | yes |

Default carried, not asked: **Sudden Death sets the gauge to the match-start value (2 pips)**. Today Sudden Death readies every cooldown; this follows G3.1 rather than refilling the gauge to full.

## Gauge round 4 — Story unlock, kit rules, grid nodes, CPU (2026-10-31)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| G4.1 | When the gauge appears in Story | **Visible from Level 0, dimmed**, with a Dormant overlay like today's locked slots until Special 1 is restored after Level 2. | no (recommended: hidden until Special 1) |
| G4.2 | Kit cooldown rules (Requiem → Fortissimo −2 s; Cleopatra's Vortex Step halving the Vortex) | **Disabled until the kit pass.** Both rules do nothing for now; Mozart's and Cleopatra's kit passes redesign them. | no (recommended: map them onto the reuse delay) |
| G4.3 | Grid cooldown minors (Joan Piercing, Mozart Wave, Lincoln Splitting) | **+10 % Special-gauge regen, character-wide**, as a placeholder until the kit pass. | yes |
| G4.4 | CPU / Mirror clone | **Like ready cooldowns**: the existing ladder fires a Special whenever it has a pip and the ladder picks one; Easy still uses none. It stays kit-blind (decision 9.3). | yes |

Defaults carried, not asked:
- The dimmed gauge fills normally in Levels 0–2 (inert), from the 2-pip start.
- The Systems Card section `systems_card_momentum_*` is rewritten for the gauge.
- G4.2 leaves `KitReachRules`' Requiem shave and Cleopatra's Vortex Step scale unread. The kit pass owes them a design, and `ResonanceReaderCoverageTests`-style coverage must allow them until then.

## Gauge round 5 — HUD, name, packaging, play test (2026-10-31)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| G5.1 | HUD | **Along the Specials.** See below. | yes |
| G5.2 | Name | **Momentum.** "Land hits to build Momentum, spend it on Specials." It takes over the name of the per-hit cooldown refund it replaces. | yes |
| G5.3 | Packaging | **One package with Defy**: the gauge and the active Defy are planned and built together. | yes |
| G5.4 | Hands-on test | **After this package, before the kit pass.** The session folds in the owed 2026-10-16 pad checklist. | yes |

The HUD (G5.1):
- Four pips sit under the Specials' capsule in Story, and under each player's Special lenses in Fighter.
- The Special lenses lose their cooldown sweep. They show only the 2 s reuse delay and the lockout, and dim when no pip is left.
- The movement lens keeps its cooldown sweep.

Every question for the gauge is now answered.

## Recon follow-up round (2026-10-31, after the four recon dossiers in docs/recon-2026-10-31/)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| R1 | Vortex Step: it halves Desert Mirage, Cleopatra's MOVEMENT cooldown, not a Special | **Keep it working.** G4.2's premise was wrong for Cleopatra. Only Mozart's Requiem → Fortissimo shave is disabled until the kit pass. | yes |
| R2 | A Special pressed with 0 pips | **The "not yet" flash** on the lens, the same flash as a press refused by the lockout, in both modes. | yes |
| R3 | Defy against an Ultimate wind-up (6 invulnerable frames vs a 20-frame wind-up) | **The blast cancels the wind-up.** The blast pushes and staggers the A02 caster through its hyper-armour and cancels the activation; the caster's meter stays spent. This amends 3.3: Defy is still refused once the activation hold has connected. | no (recommended: a timing read) |
| R4 | Projectiles and constructs | **Characters only.** The blast affects fighters, mobs, bosses and the Mirror clone; projectiles, constructs and zones are untouched. | no (recommended: clear enemy projectiles) |

## Orchestrator rulings (2026-10-31; provisional, reversible, each to get a ledger row)

### Fighter state and the wire

**O1. State layout, Option A: no new Klotho component ID.**
- 310's `MomentumRefundedFramesSlotOne` becomes `MomentumUnits`: the gauge, in integer units of 7200 per pip (max 28 800, start 14 400, cost 7200).
- The `...SlotTwo` field is freed.
- 305's `SpecialOne/TwoCooldownFrames` become the per-slot reuse-delay timers (120 frames).
- 323 gains:
  - one appended int, `DefyFrames` (36 → 40 B)
  - `ActionFlags` bit 5: blast fired
  - `ActionFlags` bits 12–15: a wrapping Defy presentation counter
- The passive-Defy ints in 312 are removed; Aegis and the barrier stay.

**O2. Protocol v4 → v5.** `GameplayButtons` bit 15 = Defy. A new bit meaning makes old peers desync, so the version must refuse them. The serialized sizes stay 12 B / 47 B, and after this the wire has no free bits.

### The Momentum gauge

**O3. Constants live once in a new "Momentum gauge" region of `BasicComboRules`.**
- The D2 per-hit refund is deleted (`MomentumGrant`, `RefundMomentum` / `ApplyMomentumRefund`, the 120-frame cap).
- The sim reads the constants through `FighterMomentumRules` (integer units).
- Story keeps float pips derived from the same constants: 1 pip / 12 s means 10 units a frame.

**O4. The gauge ticks exactly where Special cooldowns ticked before**, in both modes. It pauses in the fighter's own hitstop and under Time Freeze; whatever the old cooldown tick skipped, the regen skips too.

**O5. Gains, once per landed swing (the D1a landed predicate), bosses included:**

| Swing | Gain |
|---|---|
| String hits 1–2 | +0.05 pip |
| Up-Attack, Down-Air, air strike, ledge attack | +0.05 pip |
| Finisher | +0.2 pip |

Throws, Specials, Ultimates, constructs and zones pay nothing.

**O6. Grid cooldown minors become a new stat key, `MomentumRegen`.**
- Character-wide, ×1.1 regen.
- The three nodes' data changes and their descriptions are rewritten.
- The scoped `CooldownReduction` key is removed if nothing else reads it.
- `ResonanceReaderCoverageTests` is updated.

**O7. Requiem → Fortissimo shave: disabled until Mozart's kit pass.** It gets a DEFER ledger row. Its `KitReachRules` constant is removed or marked unread.

**O8. Special cooldown data goes away.**
- `CooldownDuration` is removed from the 18 Special .tres. The field stays on `AbilityData` for movement abilities and Ultimates.
- A content test pins "no Special authors a cooldown".
- The Move List shows a Momentum cost line for Specials.

### Defy: when it is legal

**O9. Legal from any state**:
- idle, run or air
- mid-swing
- mid-Special cast (the cast is cancelled)
- hitstun, tumble, knockdown, tech lock
- blocking or shieldstun
- held by a grab (the grabber is released)
- ledge hang

Refused:
- dead or on the respawn platform
- captured by an A02 activation hold
- during your own Ultimate, in any activation phase or the Story Ultimate cast
- during Time Freeze or the Post-Landing Hold
- during the pre-match countdown, scripted gameplay pauses and Level 0's unlosable hold
- while already Defying
- below 50 Influence

**O10. A press during the fighter's own hitstop freeze** is held one deep and starts on the thaw tick. That start overrides the hit's hitstun, launch or knockdown.

### Defy: the action

**O11. Frames.**
- Frames 1–6 invulnerable (`InvulnerabilityFrames` in the sim, `IsDefyProtected`-style in Story).
- The blast on frame 6.
- 24 vulnerable recovery frames, then the fighter is free.
- Velocity is zeroed at the start, with no gravity through frame 6 and normal gravity in recovery.
- Defy claims its tick over every other action.
- It ignores the ability lockout and Suppression, and arms neither.

**O12. The blast.**
- Radius 2.5 u (150 px) from the body centre.
- Unblockable, no damage.
- An assigned push away from the Defier (a provisional value the planner picks).
- A 30-frame non-launching stagger.
- An 8-frame victim hitstop on everyone it hits, plus the Defier: the "world freeze", which is sim state through `HitstopFrames`.
- It earns no meter, no Rally echo, no D1a landed and no Momentum.
- It ignores hyper-armour, the V7.4 getup and armored-recovery windows, and the A02 wind-up armour. Against a caster in wind-up it cancels the activation (R3).
- It charges no poise or elite stagger budget.

**O13. What the blast hits.**
- Dormant lock members: skipped.
- Bosses:
  - The current telegraph or active window cancels into recovery (`CancelTelegraphIntoRecovery`).
  - They are pushed unless `IsKnockbackImmune`.
  - They are not stunned.
- The Mirror clone is treated as a fighter.
- Characters only (R4).

**O14. The heal.** `min(missingHP, max(echoPool, ceil(10 % MaxHP)))` on the blast frame. The echo pool is zeroed and its uncredited damage-taken meter forfeited. Story goes through the normal heal path.

**O15. Cost.** 50 Influence, spent at acceptance and never scaled. The Story ×0.5 scale affects gains only.

### Defy: what is removed

**O16. The passive Defy is deleted entirely**:
- the lethal branches in both modes
- the 60-frame protection window and `EndOnOffence`
- `DefyBarred`
- the seal model, the HUD seal and the EventBus seal payload
- the `defy_*` kit icons, behind a legacy-reference scan
- the `defy_seal_shatter` cue
- the CharacterFactory spent-Defy seeding

`StoryDefyHistoryUsed` / `DefyHistoryUsed` stay as documented dead save data.

### Presentation, input and HUD

**O17. Presentation.**
- A pooled, code-drawn `DefyBurstEffect` (gold ring), on the `BlockSparkEffect` pattern.
- A "DEFIED" world label via a new `FloatingDamageNumber` text overload, shown even with damage numbers off.
- A desaturate pulse gated by `ComfortSettings`.
- `AudioSnapshot.DefyOrScripted` raised.
- A new `defy_burst` Critical cue (silent placeholder).
- The post-Defy glow during the invulnerable frames.
- Fighter edges come from the 323 counter's short forward step (rollback-safe).

**O18. Input.**
- `gameplay_defy`, remappable, keyboard F and pad button 8 (R3), key `controls_action_defy`.
- `ui_dialogue_log` moves to pad button 4 (Back).
- A load repair `RepairDefyPadDefaultCollision`: a saved row already on R3 keeps its button, and Defy's pad slot goes Unbound with a notice.
- A saved Time Freeze row on Back: no repair, only a ledger note.

**O19. HUD (poll model).**
- Story:
  - four pips under the Specials capsule; the slab grows only as much as the pip row needs, and its pins are updated
  - Special lenses show the 2 s reuse delay and the lockout, with a Starved look at 0 pips
  - a gold radial notch at 50 on the Ultimate lens ring
  - the seal badge is removed
- Fighter:
  - pips under each player's Special lenses
  - a gold notch on the meter tube, distinct from the boss bar's phase notches
  - the seal is removed
- A 0-pip press plays `NotYetFlash` (R2); the Fighter side extends `FighterLockoutRefusalRules`.

### Story, Fighter match rules, CPU and bots

**O20. Story unlock.** The gauge is visible from Level 0 with a Dormant overlay until Special 1 is restored, and fills normally (G4.1).

**O21. Story persistence.** The gauge is saved as a key in `StoryPlayerResourceTimers`; an absent key means 2 pips. There is no schema bump, and old Special cooldown keys are ignored on load.

**O22. Fighter match rules.**

| Event | Gauge | Influence | Defy |
|---|---|---|---|
| Stock loss | kept | × 1/2 | — |
| Sudden Death | set to 2 pips | 0 | allowed (no bar) |

**O23. CPU.**
- A pip-and-reuse-delay test replaces the four cooldown tests. The planner settles how a hand-built observation's default reads.
- Defy is a reflex branch rolled once per hitstun instance:
  - Easy never.
  - Medium is a % roll on strings of 3+ hits.
  - Hard Defies when its meter is at 50+ and it is in a string of 2+ hits, cornered, or the opponent is in an A02 wind-up within the blast radius. That last case needs the target's A02 phase added to `CpuDecisionObservation`.
  - Hard otherwise banks its meter for the Ultimate.
- The Mirror clone follows its band.

**O24. Bots.**
- `play` presses Defy conservatively: meter ≥ 50, in a string, ≥ 2 enemies within 250 px, at most once per 5 s. `spam` never does.
- New telemetry: Defy executed, hits taken in the 3 s before and after, death within 3 s, and the refusal labels `no_momentum` / `reuse_delay`.
- Batches from before and after are not comparable.

**O25. Text.**
- The Level 0 lesson follows 5.1.
- A new first-use tooltip ID replaces `defy`.
- Sarah's hub line and the Systems Card's Defy and Momentum bodies are rewritten.
- New user-visible text uses `en.csv` keys; any value with a comma is quoted.

### Docs

**O26. The design master and mirror get the full change.** That includes the catch-up of their stale lines, since these spans are rewritten anyway: per-hit Momentum history, the Story meter scale now ×0.5, and Time Freeze on L3. The three DOC-MASTER-UNBOUND-BINDING lines keep their own wording. The contracts and the ledger get a new section, and AGENTS.md / CLAUDE.md are updated at closeout.

**O27. Story flat effects** scale by `StoryCombatRules.FlatEffectScale` = the D5 scale alone (0.5, not Mozart's lane). It applies at the call sites, never inside `DrainUltimateMeter`:
- the Extractor discharge (and the Resonance Hold stand-ins that inherit it)
- the searchlight
- the Siphon Snare
- Shield of Orléans
- the `MeterBoost` orb

## Plan questions (2026-10-31, after docs/METER_DEFY_UPDATE_2026-10-31_PLAN.md §0.7)

| # | Question | Answer | Recommended? |
|---|---|---|---|
| Q1 | Name | **Keep "Defy History".** | yes |
| Q2 | Blast ring-out over an Open-stage pit | **Allowed** (P8; `VERIFY-DEFY-BLAST-RINGOUT`). | yes |
| Q3 | No Momentum regen before GO (P2) | Default kept; the user was told. | — |
| Q4 | Hard CPU Defies on every qualifying trigger | **As ruled; judge it in play.** | yes |
| Build | Start building | **Yes, run the whole package (phases A–D).** | yes |
