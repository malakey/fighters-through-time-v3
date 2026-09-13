# FTT Design Doc Deep Analysis — V7.5 Baseline (2026-09-08)

A full audit of `design-godot-v7.md` (all 17 sections) and the HTML mirrors, performed immediately after the V7.5 narrative rework pass. Four parts: hard inconsistencies, challenged design concepts with recommendations, genre benchmarks, and gaps blocking full implementation. A suggested priority order closes the report.

---

## Part 1 — Hard inconsistencies (spec contradicts spec)

### 1.1 The Fractured tier is mathematically unreachable *(worst finding in the doc)*
> **Status: RESOLVED in V7.6 (2026-09-11)** — by a different fix than recommended below. Re-checking the math showed a deeper flaw: Extractors drained only once their off-path room was entered, so skipping every Extractor scored 100% (the 15% share fix would have punished only players who engaged). Timeline Integrity was redesigned as the **level timer** — drains in real time, faster per living Extractor, slowed (never refilled) by destroying Extractors and finding the secret, frozen at the pre-boss checkpoint, Timeline Collapse at zero on every difficulty. See the V7.6 block and Section 3 of `design-godot-v7.md`.

Timeline Integrity: each Extractor holds a hard-capped **10% siphon share**, levels contain **2–3 Extractors**, and the share cap is explicit ("no single Extractor can ever cost more than its 10%"). Maximum possible loss is therefore 30%, and the worst possible level-end value is exactly **70%** — which is the *Stabilized* threshold (≥70). **Fractured (<70%) cannot occur on any difficulty**: Hard doubles the drain *rate*, not the share. The three-tier rating system's bottom tier is dead code, along with its exit beat, its 0% dust bonus, and its Sarah/hero lines.

**Fix options:** raise shares to 15% (worst case 55%), allow 4-Extractor levels in Act II, or add a non-Extractor drain source (e.g., boss-arena drain while the boss lives). **Recommendation:** 15% shares with restoration retuned to +4% per Extractor kill — preserves the local-pressure fiction and makes Fractured a real punishment for ignoring the system.

### 1.2 The nine Resonance Grids contradict the V7 grid rules that govern them
> **Status: RESOLVED in V7.6 (2026-09-11)** — all nine grids re-authored with unique per-character topologies, dead stats removed, the nine traversal nodes placed as Tier 2 nodes, and the node schema extended (`prerequisiteMode`, ability-scoped stat keys). Node count and the 975-dust economy unchanged. See the V7.6 grid block and Section 5.

Section 3 says every dead V6 stat was re-authored (`BlockDurability` → `BlockCharges`/CDR; `Armor` → `MaxHP`, "a damage-reducing armor stat is explicitly against the combat pillar") and that **every grid contains at least one traversal node**. The Section 5 mermaid grids were never updated:

- Joan still has **"Minor Armor +5%"** (forbidden stat) and **"Minor Dash Speed +5%"** (the universal dash was removed 2026-08-09).
- Einstein, Shakespeare, Cleopatra, and Lincoln still carry **"Block Health"** nodes (no such stat key).
- Joan's "Minor Combo Speed +5%" maps to no `StatType`.
- **None of the nine authored traversal nodes** listed in Section 3 (Warp +20 frames, wings-refresh, glide re-entry…) appear in any grid diagram.

One of the two sections is fiction. The grids need a re-author pass against the Section 3 rules.

### 1.3 Lincoln's "Kinetic Splitting" major node grants something he already has twice
> **Status: RESOLVED in V7.6 (2026-09-11)** — Kinetic Splitting now ground-bounces spiked targets into a 20-frame follow-up window (PvE, stagger-discipline-bound) and deals +50% to Extractors and constructs.

The node: "Lincoln's downward crush shatters shields instantly." Splitting Strike's baseline already "shatters active blocking shields instantly" — and beyond that, *every* special consumes all 3 charges under the universal block model, so both the baseline text and the node restate a roster-wide rule. The major perk is empty and needs a real effect.

### 1.4 The two-slot status rule isn't in the data schemas
> **Status: RESOLVED in V7.6 (2026-09-11)** — `StatusSlots` (Damage / Control) on `PlayerController`, the enemy runtime table, `IStatusEffectTarget`, and `PlayerSnapshot` (six fields incl. Intensity, on the ID 310+ component); slot mapping is a routing table; same-slot overwrite is stronger-wins with weaker applications ignored (no refresh farming); `Suppression` reserved for 1.6.

V7's two-slot status model (one damage + one control status concurrently) is design law, but `PlayerController` runtime data still declares a single `ActiveStatus`, and `PlayerSnapshot` serializes a single `ActiveStatusType`/`ActiveStatusDuration` pair. The rollback snapshot as specified **cannot represent the status model as specified**. Both schemas need a second slot (on the same new Klotho component the V7.1 state already mandates, ID 310+).

### 1.5 The promised Level 0 tech prompt was dropped in the V7.5 rebuild
> **Status: RESOLVED in V7.6 (2026-09-11)** — Level 0 Part 2 gains a Grab Calibration (blocking dummy, mandatory grab-and-throw, triangle stated both ways) and a Hitstun Agency Calibration (one scripted launch: DI prompt in hitstop, mandatory landing tech that repeats until it lands, cost-free). The Section 4 landing-tech promise now points at the beat.

The landing-tech spec (Section 4) says Story Mode teaches teching — "Sarah's calibration adds one scripted tech prompt when the design's tutorial pass next revisits Level 0." V7.5 *was* that revisit, and the rebuilt calibration teaches basics, block, Rally, meter/Defy, ledges, drop-through, and rewind — but no tech, no DI, and no grab. The debt should be paid: one scripted launch the player techs, one grab prompt on the dummy. (Fighter Mode gets these via the Systems Card; Story players currently get nothing.)

### 1.6 The Erasers exist in fiction only
> **Status: RESOLVED in V7.6 (2026-09-11)** — Eraser elite line (Section 6): Null Lance → `Suppression` control status (2 s legacy-kit lock, aura smothered; Basic-class vs block), Siphon Snare meter drain; scripted ambushes at Levels 5 and 13, salted 7–15; Story-only.

V7.5 made Erasers load-bearing (they hunt the escaped legend; their weapons "nullify historical powers"; their fixation is a Mystery Thread clue) — but the enemy roster contains **no Eraser unit**, and no mechanic anywhere nullifies player powers: `StatusType` has no suppression effect, nothing locks specials, and the only meter drains are environmental.

**Recommendation:** author an **Eraser elite line** (Act II+, salted like the Chrono-Warden) with a power-null kit — a new `Suppression` status (control slot: specials locked ~2 s) and a meter-drain projectile — plus one scripted Eraser ambush per act boundary to dramatize the hunt.

### 1.7 HUD spec vs. systems that render on it
> **Status: RESOLVED in V7.6 (2026-09-11)** — hierarchy and sizing gain the Integrity clock, rewind cooldown pip, Rally echo band (both modes), two status-slot indicators (both modes), and ability slot lock states (Dormant / Suppressed / Clear); the dust counter is persistent with a `+N` pickup float.

The Story HUD hierarchy (Section 7) has no node for:

- the **Timeline Integrity percentage** (Section 3 says it "sits top-right beside the dust counter"),
- the **manual-rewind cooldown pip** (V7.3),
- the **Rally echo band** on the HP bar (V7.1: renders as an inner band),
- **locked ability slots** under the Legacy Unlock Schedule (are empty cooldown slots shown at Level 1?).

Worse, the HUD spec says the currency counter "appears briefly in top-right on pickup, then fades" while the Integrity spec assumes a **persistent** top-right dust counter. Pick one (persistent, given Integrity sits beside it) and add the four missing nodes.

### 1.8 Section 1's stage-design examples cite stages that don't exist
> **Status: RESOLVED in V7.6 (2026-09-11)** — examples now Nassau Flagship, Vesuvius Caldera, Globe Theatre (master + index.html).

"Dynamic, multi-tiered platforms based on historical events (e.g., **the deck of the Titanic, the Apollo 11 moon landing**…)" — neither is among the 10 authored Fighter stages. Stale V6 text; the examples should be real stages (Nassau Flagship, Vesuvius Caldera).

### 1.9 The Unified Difficulty Scaling Table isn't unified
> **Status: RESOLVED in V7.6 (2026-09-11)** — Checkpoint Rewind Refresh and Middle Checkpoint rows added to the master and architecture.html tables.

The master's table lacks the **Checkpoint Rewind Refresh** and **Middle Checkpoint (inert on Hard)** rows — both appear in campaign.html's version and in Lives-section prose. Mirror and master disagree on the table's row set.

### 1.10 V7.5 absence beats never reached the six level-brief cards
> **Status: RESOLVED in V7.6 (2026-09-11)** — one authored Absence line on each of the six cards (master + narrative.html), with the "Historical Event describes what history *should* be doing" note.

The Mystery Thread spec and the campaign-flow text carry the absence beats, but the per-level brief cards (Florence, Orléans, Chicago, Egypt, Globe, Gettysburg) still read as if the figures are present ("Leonardo da Vinci designing his flying machines"). Each of the six needs one absence line, with a note that the "Historical Event" row describes what history *should* be doing.

### 1.11 How does the player reach the Meridian Founding?
> **Status: RESOLVED in V7.6 (2026-09-11)** — the player rides the Forge's own firing channel; the channel exists only while the Forge fires, which is why the fight happens during the firing (Sections 2 and 3).

The Wardens are canonically cut off from the future; Level 15 is *in* the future. The transition mechanism is unstated. One sentence fixes it: the player rides the Forge's own firing channel to the seat point — which also explains why the fight happens *during* the firing.

### 1.12 Timeline Integrity scope in Act III is undefined
> **Status: RESOLVED in V7.6 (2026-09-11)** — Act III keeps the clock as the **Resonance Hold**: the hero's own charge against the Unbound's field, binary by the candle rule (full kit until zero — no mid-campaign power loss), Collapse at zero; conduits / cradle valves / anchor pylons stand in for Extractors; L14's reveal recontextualizes it as the Forge's intake. Forge-charge and Eraser-countdown clocks rejected. **Also added (not an audit finding):** the Act III Gauntlet — Levels 13–15 back to back with no hub return, Warden Beacon for Repository access, Anchor Snap respawn spending Easy 3 / Normal 2 / Hard 1 charges per level, and the Smothered Game Over when none remain.

"Every campaign level from 2 onward" includes 13–15 — but there is no *era* to drain in the Void, the Bastion, or the Founding, and Extractor placement there is unspecified (Act III rosters are deferred). Either exclude 13–15 explicitly (recommended — the eras are what the Forge drains) or define what the percentage means there.

### 1.13 Minor sweep items
> **Status: RESOLVED in V7.6 (2026-09-11)** — Move List Story lock badges driven by `unlockedLegacyAbilities`; "≈450 total" replaced with roster-scaling wording; same-frame chord priority ruled for Echo Step and grab together ("the chord beats the single input, and the priced verb beats the free one").

- The Move List screen will display specials/ultimate in Story before they're unlocked — needs a locked badge tied to `unlockedLegacyAbilities`.
- The narrative layer still says "≈450 total" and "nine per-character replays" — roster-hardcoded arithmetic the V7.5 mandate says to phrase against "the current roster."
- Echo Step (Block+Roll) and block-cancel both fire in attack recovery; same-frame chord priority is defined for grab but not for Echo Step.

---

## Part 2 — Design concepts to challenge, with recommendations
> **Status: ALL DECIDED in V7.6 (2026-09-11).** 2.A — not a level-replay game: Chronal Rating retired, no Level Select/records; replay answer is the per-character **Level 4A Legacy Level**. 2.B — abnormal-exit fee **dropped**. 2.C — hold-to-skip everywhere, first viewings confirm. 2.D — Open stages before any Fighter tuning. 2.E — L1–L4 boss HP 350/520/700/850, dust rebalance gains kit-scaling and the Legacy Level. 2.F — drills at launch, Remote Play Together message + Package 7 first post-launch, stall-validation gate. 2.G — AI assets are placeholders replaced by artist content before release (disclosure moot); flash-reduction setting remains a Part 4 item. Eraser debut moved from Level 5 to Level 4A.

### 2.A The replay pitch is thinner than the doc believes
The stated replay hook is nine per-character campaigns — but levels, routes, puzzles, secrets, and bosses are identical every run; the per-character delta is a kit and ~50 lines. Genre comparison: **Shovel Knight's** replay campaigns (Plague, Specter, King) succeed because a new moveset *re-solves the same spaces differently* — and they still re-author level chunks. **Mega Man** doesn't ask for nine consecutive 7-hour runs. FTT's kits genuinely do re-solve spaces (Einstein warps vs. Lincoln's charge), so the bones are there. Three cheap multipliers would carry most of the weight:

1. Promote **post-completion level select** from "deferred" to launch-window — the Chronal Rating S-rank chase is the real replay engine and currently has no re-entry door.
2. Add a **records surface** (per-level rating/time/Integrity board per character) — ratings are saved but displayed nowhere after the results screen.
3. Make **hold-to-skip eligibility global-seen rather than completed-slot** — as written, a player who abandons run 1 at Level 8 and rerolls must re-read every scene unskippably, which is exactly the player being retained.

### 2.B The abnormal-exit fee fines crash victims
The session-marker rule charges 20% dust after *any* abnormal exit — including the game's own crashes. Closing the Alt-F4 loophole is worth less than punishing a player for a bug: this is a single-player economy, and the exploit's ceiling is 20% of one level's undeposited dust. Genre norm is to eat the loophole. At minimum: forgive the first abnormal exit per save, or waive the fee when the marker is younger than N minutes.

### 2.C First-viewing unskippable dialogue is a friction choice worth softening
Even story-heavy action games (Hades, modern Mega Man collections) let players skip anything with a confirm. Keep the intent (don't accidentally skip canon) via hold-to-skip-with-confirmation on first viewings instead of a hard lock.

### 2.D The platform-fighter identity is load-bearing but currently unreachable
DI, landing tech, ledge trump, edge-guarding, recovery specials, and the low-HP knockback bridge all exist to serve pit/blast-zone play — and the V7.3 deferral flag admits **every shipped stage is effectively sealed** until the floor-segment build-out lands. The game's most distinctive system cluster (HP-bar platform fighter — genuinely novel) is untested at its own core loop. **Recommendation:** sequence the three Open stages before any further Fighter tuning passes; every balance number touched before pits exist may need retouching after.

### 2.E Boss/economy tuning was not revisited for the Legacy Unlock Schedule
Levels 1–4 are now fought with a progressively smaller kit (L1: basics only), but the L1–L4 boss HP pools (500–900), the encounter economy, and the dust-income ledger were authored against the full kit. A basics-only Borgia Inquisitor fight is roughly 2× longer than authored. The deferred dust-economy rebalance now has a second mandatory input: retune L1–L4 boss HP (start around −30% on L1, tapering to 0% by L5) and re-verify the ≈700–800 critical-path dust estimate under slower early clears. Resonance Momentum is inert until Special 1 unlocks (L3+) — the Level 2 Wren tip should not mention it.

### 2.F Watch-items (keep, but monitor in playtests)
- **6–14 s cooldown band in PvP** — MOBA-scale timers in a platform fighter invite stall; Overtime and shieldstun are the mitigations — validate them.
- **Total system count vs. the "low-execution" identity** — Rally + Desperation + Defy + Echo Step + Momentum + Overtime + grabs + DI + tech is a lot of card; the Systems Card and drills are the right answer, so ship the drills rather than deferring them past launch.
- **Launching a fighter with no online** — the deterministic core banked is smart, but the platform-fighter audience treats netplay as table stakes (MultiVersus and NASB both paid for weak netplay stories). Consider Steam Remote Play Together messaging at launch and Package 7 as the first post-launch beat, not a distant one.

### 2.G Production risks outside the design itself
- The GPT-Image asset pipeline needs a **Steam AI-content disclosure** plan (Valve requires it on the store page) and a consistency QA gate per atlas batch.
- The rewind/Overtime presentation stack (max chromatic aberration, scanlines, palette fracture) needs a **photosensitivity pass** — there is no flash-reduction setting anywhere in Settings.

---

## Part 3 — Genre benchmark summary

| Axis | FTT | Genre reference | Verdict |
|---|---|---|---|
| Campaign structure | Strictly linear 16 levels, no map | Mega Man / Shovel Knight | Matches; act-gated portal talk is a good ritual |
| Ability gating | Unlocks across Act I | Mega Man X intro, Shovel Knight relics | Matches norm; needs the tuning pass (2.E) |
| Death economy | Rewind pools + priced recovery ladder | Celeste (infinite retry), classic lives | Harsher than modern norm but coherent as identity; the V7.2 ladder is well-reasoned |
| PvE healing | Feast / Font / Mending | SoR4 / TMNT food | Direct genre match, well executed |
| Fighter core | HP bars + stocks + blast zones + low-HP launch curve | No direct precedent (Brawlhalla/Smash use percent-style scaling) | The most original system — protect it by shipping the Open stages |
| Execution ceiling | No dash/wavedash/landing lag | Brawlhalla accessibility ethos + trad-fighter block/grab triangle | Coherent; the V7.2 triangle write-up is stronger than most shipped docs |
| Netcode | Deterministic rollback core at launch, online later | Rivals 2 / modern standard = rollback at launch | Architecture right, sequencing risky (2.F) |
| Replayability | 9 identical campaigns + ratings | Shovel Knight campaigns, Hades heat | Weakest axis — see 2.A |
| Training | Deferred; Holodeck interim | Rivals 2 shipped full training day one | Under norm for the fighter audience; drills should not slip |

---

## Part 4 — Missing for full implementation

### Narrative / content
- **The First Unbound has no character sheet** — portrait, name-card title, era subtitle, moveset beyond the phase row. A dialogue-bearing final boss needs a portrait set.
- **Borrowed Legacies data spec** — per-character borrowed-move table; `BossData` consuming character `AbilityData` is new plumbing.
- **Act III dossiers (13/14/15) need rework for V7.5 content** — Extraction Hall set-piece, Founding arena; PACKAGE5's versions predate all of it.
- **Crew portraits** (Sarah/Wren/Okafor) with emotional variants are absent from the Section 17 asset list.
- **Missing from Section 17:** Bastion/Founding environment sets, cradle props, conduit VFX, Level 0 beam/desaturation shaders, per-character ignition stills (budgeted in the narrative layer but absent from the asset list).
- The **persistent gold resonance aura** needs a row in the glow-shader priority table (base state, below slot indicator).

### Systems
- Eraser elite line + `Suppression` status (1.6).
- Second status slot in both schemas (1.4).
- Global seen-dialogue tracking (2.A).
- Level select + records screen.
- **Achievements** — unspecced entirely; a Steam release without an achievement list is a store-page gap.
- Flash-reduction / photosensitivity setting.
- Holodeck guided drills (spec'd, deferred — recommend pulling in).

### Tuning debts (acknowledged or newly created)
- Dust-economy rebalance (now including L1–L4 kit-scaling).
- Integrity share fix (1.1).
- Grid re-author pass (1.2, 1.3).
- Floor-segment build-out (2.D).
- The H-7 gravity unification; the sim's drop-through input divergence.
- Per-level V7 delta checklist (spine assignments, puzzles-per-level, secrets).

### Open decisions
- **Pocahontas swap** — kit, atlases, localization, grid, and the Mystery Thread slot all ripple.
- Meridian Founding transition line (1.11).
- Act III Integrity scope (1.12).

---

## Suggested priority order

1. **Integrity share fix** (1.1) — one number, dead tier revived.
2. **Grid re-author pass** (1.2, 1.3) — pure doc work, blocks the grid UI.
3. **Level 0 additions** (tech/grab prompts) + HUD spec deltas (1.5, 1.7) — small, unblock implementation.
4. **L1–L4 boss/economy retune** for the unlock schedule (2.E).
5. **Eraser elite + Suppression** (1.6) — the one new system this pass genuinely owes the fiction.
6. **Floor-segment build-out** before further Fighter tuning (2.D).
7. **Replay surface** (level select + records + global-seen skip) as the launch-window package (2.A).
