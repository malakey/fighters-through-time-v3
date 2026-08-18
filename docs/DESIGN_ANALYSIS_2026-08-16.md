# Design analysis — 2026-08-16

A deep review of the *design* of Fighters Through Time — as specified in `design-godot.md`, as amended by the locked decisions in the package plans and `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md`, and as it actually plays in the current build — measured against the two genres it straddles: the platform fighter (Super Smash Bros., Rivals of Aether, Brawlhalla, Nickelodeon All-Star Brawl, MultiVersus) and the 2D action-platformer / Metroidvania (Shovel Knight, Hollow Knight, Ori, Guacamelee, Dead Cells, Mega Man, Celeste).

This document is about **what the game should be** — design gaps, internal contradictions, and things the genre has taught players to expect. Its sibling, `docs/IMPLEMENTATION_ANALYSIS_2026-08-16.md`, is about **whether the code does what the design says** (bugs, missing features, divergences, test gaps). Where an item is both, it is summarised here and detailed there.

Numbers quoted below come from `resources/**/*.tres` and the shared rulebooks (`BasicComboRules`, `UniversalMovementRules`), never from prose — see the authority order in `CLAUDE.md`.

---

## 0. Executive summary

The design has an unusually clear identity — HP-based platform-fighter combat shared with a linear historical side-scroller, powered by a data-driven kit system that must read the same in both modes — and the codebase honours that identity structurally. But the *moment-to-moment feel* the design specifies is closer to a 2D brawler with a combo string than to a platform fighter, and the *campaign* it specifies is closer to a Mega Man stage sequence than to any of the "Metroid/Castlevania-style" exploration it name-checks. Neither is a wrong choice; both are under-decided, and the under-decision shows up as a long list of open items in the deviation logs and as a build whose Fighter mode has no off-stage game and whose campaign has eight distinct mechanics across sixteen levels.

The ten design decisions that would most change the product, in priority order:

| # | Decision | Why it matters |
|---|---|---|
| 1 | **Resolve the stage-boundary model** (H-11): open blast-zone floors vs. sealed arenas | Determines whether Fighter mode is a platform fighter or an arena brawler. Every ledge/recovery/edge-guard system hangs on it. Today the pillar text says one thing and every production stage does the other. |
| 2 | **Add hitstop and a hitstun-input model** (DI or a lighter "tech" verb) | Without hitlag, no hit has weight; without any victim agency in hitstun the finisher/launcher retune reads as a cutscene. These are the two most-felt absences vs. every genre peer. |
| 3 | **Re-centre the kit around normals, not cooldown specials** | A 28–40 damage special on a flat 10 s timer outclasses an entire 13–25 damage basic string. Neutral becomes "wait for cooldown", the opposite of what the design's rushdown/zoner archetypes want. Differentiate startup/cooldown/hitstun per ability. |
| 4 | **Decide what the block is**: charges+shatter+lockout, or a shield-stun model | The current 3-charge shield with no shatter lockout and instant recovery on release rewards holding block; the design's own 5 s lockout and 2 s regen are unimplemented "open questions" (H-5). |
| 5 | **Give the campaign a mechanical spine** — one new verb or system per act, remixed | Sixteen levels share one skeleton (3 checkpoints, 4–7 rooms, wave gates, boss); puzzles — a stated pillar — exist in one level. |
| 6 | **Make the Resonance Grid change *how you play*, not just numbers** | 54 minors are stat percentages; nothing alters traversal, and the campaign has no gated-progression payoff. |
| 7 | **Author the campaign as *this character's* story** | The player is "Traveler" in every line; the hub NPC repeats two lines for the whole game; Level 0 does not start at the hero's nexus point as specified. |
| 8 | **Design bosses as encounters, not stat rows** | Nine of fourteen bosses' phase 2 is a walk-speed multiplier; four bosses are the same three-ability shell. The design's own per-boss authoring checklist is unstarted. |
| 9 | **Rationalise the dust / death / quit economy** | Three different loss rules (collapse 20 %, quit 50 %, crash 0 %) for functionally the same event; extractors are neither hidden nor a fair trade at 15 dust for ~8 self-inflicted discharges. |
| 10 | **Retire or fund the online pillar** | "Delay-based is unacceptable / GGPO rollback" is a pillar with no handshake, no input redundancy, no resync, no transport. Either commit Package 7 or reframe launch as local + LAN. |

---

## 1. Fighter mode vs. the platform-fighter genre

### 1.1 What the design commits to, and what that implies

The design's Fighter pillar (`design-godot.md:20-24`, `AGENTS.md`) is: HP not percent; 0 HP = KO; falling through the **bottom** blast zone costs a stock; **sides and top are solid**; 3 stocks / 8:00; 1v1 only; 2–3 pass-through platforms over a **solid ground floor** (`:3091-3094`).

Those choices are internally in tension:

- A *solid* base floor spanning wall to wall means the bottom blast zone is reachable **only from a pit in the floor**. The stage table (`:3108-3119`) authors exactly one pit (Paris's "central lower pit"). Every production stage seals its floor (`FighterStageGeometry.cs`), Paris included. **The bottom blast zone — a stated pillar and one of five end conditions — cannot occur in a real match** (audit H-11, still open).
- With no side/top blast zones and no reachable bottom, knockback is purely a spacing tool. That is a defensible *arena brawler* design (Brawlhalla-with-walls, or a Power Stone / TMNT-style HP fighter) — but then a stack of platform-fighter machinery (ledge grab with 19 tests, respawn platform, off-stage CPU recovery, "Special 2 is often a recovery move") is dead weight, and the design's language ("carries opponents toward the blast zone", "edge-guarding", "recovery") is fiction.
- Conversely, if the intent *is* a platform fighter, HP-with-no-percent means knockback distance never scales with damage — the design's own answer was the 2026-08-10 low-HP knockback scale (`×(1 + missingHP)`), which is a good instinct, but it only matters if there is somewhere to be knocked *to*.

**Recommendation.** Make this decision explicitly and write it into the pillar list, then align stages and systems:

- **Option A — platform fighter (recommended).** Give `FighterStageGeometry` floor *segments* so stages can have open pits and true ledges at the main-floor edges; open at least Paris (per its own dossier), Vesuvius (slopes + narrow ledges) and Nassau (listing deck). Keep walls on the *far* sides so the top/side-solid pillar survives, but let the main floor end before the wall on the "open" stages. Then the ledge system, recovery specials, off-stage CPU logic and the respawn platform all earn their keep, and edge-guarding exists.
- **Option B — sealed arena.** Delete the bottom-blast end condition, retire the ledge system to platform edges only, drop "recovery" from the Special 2 brief, and rewrite `AGENTS.md`/`design-godot.md:22`. Then invest the freed effort in what an HP arena needs: hitstop, wall-splat/wall-bounce (walls are solid — use them), and corner pressure rules.

Either is coherent. The current state — the pillar of A with the geometry of B — is not.

### 1.2 Feel: what a platform-fighter player will notice is missing

Verified by reading `FighterSimulationSystems.cs`/`FighterEntitySystems.cs` (see the implementation report for line references):

| Genre expectation | Status | Design recommendation |
|---|---|---|
| **Hitstop / hitlag** on every connect | Absent (only the KO freeze exists) | Add 3–8 frames of shared freeze scaled by damage, applied to both parties in the sim (deterministic, snapshot a counter). This is the single cheapest feel win in the project. |
| **Victim agency in hitstun** (DI, SDI, tech/ukemi, air dodge) | Absent — hitstun is a countdown with input suppressed | Pick *one* verb: either directional influence on launch angle (small, ±15°), or a landing tech (press Block on ground contact during tumble → 12 f invuln, no bounce). Do not add all of Melee; add enough that being hit is a decision. |
| **Landing lag / jump squat / dash-dance** | No landing lag (design forbids it), no jump squat, dash removed 2026-08-09 | Fine as a deliberate "arcade" choice — but say so in the design and add *some* commitment cost to aerials or the aerial string will dominate. |
| **Shield stun / shield-drop lag / grab** | Absent — block absorb returns instantly, no throw exists | The block system needs a punish window *and* an answer to turtling. Either add shieldstun (attacker advantage on block) or a universal grab/guard-crush. Today, holding block vs. basics is nearly free (see §1.4). |
| **Ledge invincibility / trump / regrab limit** | No invuln (by design), no trump, no limit (sim can climb-regrab forever) | If Option A: add a regrab cap (2–3) and a short trump. If Option B: irrelevant. |
| **Distinct hitboxes per normal** | Sim uses one 2.0×1.6 box for all three basics and hits *behind* the attacker | Author the three escalating boxes the design already specifies (`:951-955`) and enforce facing. This is a bug as much as a design gap. |
| **Knockback angles** | Sim launches every hit at a fixed 45° | Author per-hit angles (the design already gives Story per-hit vertical components). |
| **Trades / clank** | Both swings resolve independently in the same tick | Acceptable for now; document it. |
| **Sudden death / infinite-match guard** | Stock mode has no timer; a true tie is a draw | Add an optional stock-mode timer default (e.g. 8:00 in stock too) and a sudden-death (1 HP each, or hazards on) rather than a draw. |
| **Match settings persistence, handicap, stage striking** | None persist; no handicap; rematch is same-everything or full restart | Persist `MatchSettings` in the global save; add "Rematch / New stage / Change fighters"; consider a damage-ratio handicap for local play. |
| **CPU realism** | Bands are well-authored; but block reacts to a 1-frame edge sampled every 3 frames, so Hard blocks ~27 % not 80 % | Design is fine; implementation gap. |

### 1.3 Kits: specials dominate normals

From the ability `.tres` (post the 2026-08-10 "basics ×0.5, specials ×2" pass):

| Character | HP | Full basic string (0.8+1+1.5 ×) | Special 1 | Special 2 | Ultimate total |
|---|---|---|---|---|---|
| Lincoln | 130 | 24.75 | **40** | **36** | **40** |
| Joan | 110 | 19.8 | 28 | 24 (6×4) | 72 |
| Einstein | 100 | 16.5 | 30 | 18 (3×6) | 75 |
| Leonardo | 95 | 14.85 | **60** (20×3) | 15 (5×3, turret) | 80 |
| Shakespeare | 95 | 16.5 | 28 | 0 (utility) | 84 |
| Tesla | 90 | 14.85 | 5 (coil) | 24 | 72 |
| Pocahontas | 90 | 14.85 | 28 | 8 (snare) | 80 |
| Mozart | 85 | 13.2 | 24 (8×3) | 24 | 80 |
| Cleopatra | 80 | 13.2 | 10 (nest) | 20 (4×5) | 80 |

Observations:

- **A single special out-damages a whole three-hit string for every character**, and every special is on the same flat 10 s cooldown with (for 15 of 18) identical 12/6/18 frame data. Neutral therefore reduces to: land whatever, wait 10 s, land the special. The design's own archetypes (rushdown, zoner, trapper) need normals to *be* the character. Recommendation: pull specials back toward ~1.5× a string, then differentiate them by **startup, cooldown (6–14 s), and authored hitstun** rather than by raw damage — cheap zoning tools should be fast and short-cooldown, Lincoln's Emancipator should be slow and long-cooldown.
- **Lincoln's ultimate is 40 damage; everyone else's is 72–84.** Same 100 meter. Either intended as a Root/launch utility ult (then say so and buff its knockback/pen) or a data error. Also Lincoln's two specials (76 in one cycle) nearly delete Cleopatra (80 HP) — the heavy has the highest burst *and* the highest HP.
- **Leonardo's Golden Ratio at 60 is the largest single special in the roster**, on a hybrid/gadget character.
- **Mozart is thin**: two near-identical projectiles in the sim, a movement ability that (in the sim) is a random upward pop. **Shakespeare's Tempest does 0 damage** — a pure utility Special 2 is fine, but the design brief for it should say so.
- **Movement abilities collapse to four sim behaviours** (glide ×4, teleport ×3, dash ×1, float ×1). The design's `MovementType` enum has six; the roster uses the same three repeatedly. Recommendation: pick a distinguishing rule per character (Joan's wings refill on hit, Pocahontas glide can attack, Tesla's blink passes through projectiles, Cleopatra's mirage leaves a decoy) — small rules that make the shared code paths *feel* different.
- **Roster jump spread was compressed to 11.5–13.0** by user direction. Combined with universal double-jump and no fast-fall differentiation, aerial mobility is now nearly homogeneous; weight (0.7–1.6) is the only strong axis of physical identity left. Consider re-widening *air control* (0.4–0.75 today) or fall speed as the identity axis instead of jump height.

### 1.4 The block

The design (`:1001-1013`): front-facing, 3 charges, specials shatter, ultimates bypass, 1 s daze on shatter, **5 s lockout after shatter, 1 charge per 2 s regen while not blocking**. The build: 3 charges, 3 s regen (`BlockChargeRegenFrames = 180`), **no lockout**, block-cancels-hitstun (2026-08-10), no shieldstun, no cost to holding block, and — because nothing checks `charges > 0` — holding Block at zero charges is a self-inflicted stun (implementation report). Story's shield is also not gated to grounded (a bug); the sim's is.

Design questions to answer, in order: (1) does shattering cost more than a 1 s daze? (the 5 s lockout is the design's own answer — decide, don't leave it "open"); (2) what beats a turtle? (shieldstun, a guard-crush on the finisher, a grab, or charge drain on hold); (3) is block-cancels-hitstun compatible with any combo game at all? — it currently means hit 1 into hit 2 is escapable by holding a button, which makes the three-hit string a two-hit string vs. any competent opponent. A middle ground: block cancels hitstun only *after* hit 2's hitstun (or only if the victim was not in a chain).

### 1.5 Stages, hazards, orbs

- **Layouts** are conservative and good (Battlefield-triangles, side platforms) and were re-verified against the double-jump envelope. Fine.
- **Hazards** are the strongest stage design in the game — ten distinct identities with warning/active/recovery. Two design notes: activation is a fixed cadence (design says random 30–60 s), which makes hazards *learnable* to the frame — decide whether that is a feature (competitive) or a bug (spectacle) and record it; and there is no per-stage "hazards off but platforms move" middle setting.
- **Chronal Orbs**: the four designed types exist and match the numbers. Design note: **two orb taxonomies exist** (Fighter: Heal/Haste/Uplift/Aegis; Story `ChronalOrbItem`: HP/Meter/Speed/Damage/Shield). Pick one vocabulary — the Story one is richer and would let the hub Holodeck and campaign share pickups.

### 1.6 Online

The design's netcode section is a pillar ("delay-based unacceptable") plus a matchmaking spec (regions, public queue, private 6-digit rooms, jitter HUD, forfeit-by-hold, reconnection window). Package 7 has been deferred by user decision. As a *design* matter: the current foundation has no input delay setting, no frame-advantage management, no input redundancy, no handshake, no resync — i.e. it is not yet a rollback *design*, only a rollback *simulation*. Recommendation: either fund Package 7 as a real milestone (with a decision on Steam Sockets vs. custom relay, which the design leaves open) or reframe the initial release as local + LAN and move online to a post-launch pillar. Do not ship the pillar text as-is.

---

## 2. Story mode vs. the 2D action-platformer genre

### 2.1 What the design promises vs. what it specifies

The design says "Metroid/Castlevania style" scrolling, "exploration-heavy environments" (`:2404`), "Read, Plan, Execute" puzzles (`:2441`), and a 6–8 hour campaign in the Shovel Knight / Celeste / Mega Man band (`:131`). It then specifies: strictly linear sequential levels, no map, no fast travel, no replay, no level select, no backtracking, one active portal, exactly two-to-three checkpoints per level, wave-triggered rooms, and a Resonance grid of nine stat nodes per character. **That is a Mega Man / Shovel Knight structure, not a Metroidvania**, and the "exploration-heavy" phrase should be struck — the design contradicts itself and the build has followed the second half.

That is a fine structure. What Shovel Knight, Mega Man and Celeste do with it, and what the design (and build) currently lack:

| Genre practice | Design | Build |
|---|---|---|
| **One new mechanic per stage, taught then remixed** | Level 1 layout only; levels 2–15 "deferred to level design phase" (`:2519-2531`, checklist unstarted) | 8 distinct interactive systems across 16 levels; `CyclicHazard` in 12; the same shield-tower gate in L2 and L11, the same searchlight in L4 and L9, the same moving platform in L7/L12/L13/L14. Puzzles: `PuzzleManager` used in **one** level. |
| **Secrets / collectibles / optional rooms** | Only "hidden" extractors | Extractors are on the critical path; `TreasureChest` exists and is placed nowhere; no secret counter, no completion %. |
| **Bosses as set-pieces** (intro card, arena ritual, unique mechanic, phase spectacle) | Per-boss checklist unstarted; bosses are "placeholder designs" | 14 scripted bosses; nine have a phase 2 that is a speed multiplier; four are the same 3-ability shell; Borgia has 2 abilities. |
| **Character-specific narrative** | Level 0 opens at the hero's nexus point; character-specific dialogue "left open" | Player is "Traveler" in all 175 lines; L0 is generic; Sarah has 2 lines for the whole game. |
| **Progression that changes traversal** | Grid = stats + 3 perks; "additional nodes deferred" | Nothing grants a new movement verb; no room becomes reachable later. |
| **Level results / mastery loop** | Results overlay: dust, time, rewinds | No rank, no kill count, no time-attack, no replay, no post-completion mode. |
| **Difficulty as texture** | HP/damage/drops/rewinds table (good) | Implemented faithfully. |

### 2.2 The Chronal Rewind — the campaign's best original idea, under-used

The rewind is genuinely distinctive: death is a scrub back through your own history, not a reload. The 2026-08-15 rework (8 s buffer, half-duration playback, 0.75 s hold) is the right pacing instinct. Design gaps:

- **It is only a death mechanic.** The tutorial had to script a demonstration because there is no rewind input. Consider a *manual* rewind (very limited charges, e.g. one per checkpoint on Hard) — it would turn the mechanic into a verb, tie into the "temporal" fantasy, and give the puzzle toolkit a unique tool (rewind a collapsed platform, replay a timed sequence). It also fixes the tutorial's awkward "watch this happen" step.
- **Rewind never interacts with the level.** Enemies freeze, projectiles clear, constructs reset. Nothing in the world *rewinds*. Even one exemplar (a `PathMovingPlatform` scrubbing back with the player) would sell it.
- **Timeline Collapse has no presentation** beyond a scene change to the hub. It is the campaign's only failure state; it deserves a beat.
- The rewind fallback can land at world origin before the first checkpoint (implementation report) — design should state that the level entrance is always an implicit anchor.

### 2.3 Hub

The design's hub checklist (`:198-211`) is unstarted by its own admission. The build is a single flat corridor with four hotspots and one NPC. Design decisions needed: room count and layout, at least three NPCs with per-act dialogue, the portal-charge conversation gate (currently permanently on), and *what the Repository does when deposit is automatic* (today the terminal deposits zero). Also: the Holodeck is the interim training mode; give it CPU character/difficulty selection UI in-hub as the design says (`:3194-3197`) rather than reusing the fighter select verbatim.

### 2.4 Economy

`docs/DUST_ECONOMY.md` locks a model. Design-level issues:

- **Three loss rules for the same event** — collapse 20 %, quit 50 %, crash 0 % (last checkpoint's wallet). Unify to "undeposited dust is at risk; deposited is safe", with *one* penalty on any exit from a level.
- **Extractors are the only optional content and are not a fair trade**: 100 HP object, 15 dust, discharges on every hit (design says an idle cycle) — a melee character eats ~160 damage to earn 15 dust. Redesign as the *design already says*: idle cycle with a safe window, and a reward that scales with risk (25 as originally specified, or an item).
- **Boss and extractor dust spawn no pickup**, so the design's Large sprite tier never appears; the "single icon rule" pickup ritual is only for mobs.
- The Grid's 975-dust full-clear vs. 1,200–1,800 income means every campaign fully clears the grid with slack; there is no choice pressure. Either add a fourth branch / deeper tier (the design defers this) or reduce income so a playthrough clears ~two branches.

### 2.5 Checkpoints and death

Design: strike the fracture to activate; two per level. Build: walk-through, three per level. The strike-to-activate is a nice ritual and ties to the theme — decide (it is on the "still open" list). Three checkpoints per level is generous; with rewinds refilling at each on Easy/Normal, death rarely costs more than a room. Consider Hard leaving checkpoint 1 out.

---

## 3. Shared kit design (both modes)

- **All 27 major perks are wired** (the docs contradict each other on this — see the implementation report), but **5 minors are dead** (`BlockDurability` ×4, `Armor` ×1 have no resolver) and 8 minors were widened to roster-wide stats. Design should re-author those 13 nodes with keys the resolver has, or add the two stat types.
- **Status effects**: the design's "Root then Venom" (Cleopatra) is impossible under newest-replaces-oldest; the design text should be amended (currently delivered as hitstun). `TimeDilation` slows movement by intensity but animation by a flat 50 % — decide whether intensity is one number.
- **The Warp/float 3 s cap** in Einstein's brief is unenforced anywhere; either drop it from the design or implement.
- **Persistent-object spec** (`:1041-1046`) says constructs "carry physical collision shapes that block horizontal movement and AI pathing"; the 2026-08-11 rebalance made them impulse-free and pushbox-less. Update the design.
- **Design-vs-locked-decision drift.** The design still says: no fast-fall (`:1019`), a 12-frame dash (`:689`), buffer window 0.4 s after active frames (build: after recovery), 300-frame rewind buffer (build: 480), block regen 2 s (build: 3 s), one jump for five characters (build: two for all), pre-retune damage numbers, per-boss checklists unstarted, `MaxMoveSpeed`/`MaxJumpForce` at pre-retune values, .NET 8, `IAerialMobility`, a `PuzzleManager` signal architecture (exists), Story-side "aerial combo sheet" (build reuses one). **Recommendation: a single "V7" pass on `design-godot.md` that folds in every §2 decision from `GAMEPLAY_FEEL_2026-08-10_PLAN.md` and every deviation-log entry, so the design document stops being a source of wrong numbers** — the same reason the Unity archive was deleted.

---

## 4. Enemies and bosses

- **27 enemies ≈ 8–10 behaviour combos.** Ground ×21 / Flying ×3 / StandGuard ×3; abilities skew Projectile ×14, no Summon or Teleport enemies; every elite has exactly one elite ability so the designed cycle never cycles. Recommendation: author *behaviour* variety, not name variety — give each era's elite a second ability, put a Teleport and a Summon on at least one mob, add a shield-bearer that must be hit from behind (the frontal-reduction flag exists on two enemies), and a mob that jumps (design says they don't; consider one exception per act).
- **The design's own AI spec has unimplemented items** — mob hazard immunity, "mobs cannot recover from pits", "elites/bosses bounded to platforms" (`:1147`).
- **Bosses**: authored per the design's phase counts, but the per-boss checklist (`:2370-2375`) is empty. Nine of fourteen phase transitions change only walk speed. Recommendation: for each boss, one authored *phase mechanic* (arena change, new ability unlocked, hazard interaction) — Level 14's Archive Prime already does this and is the template. Also add per-ability cooldowns to the selection algorithm (the data exists, nothing reads it) so summons cannot chain.
- **Mirror Paradox** (L13) is the campaign's best boss idea and it is the one that reuses the real CPU. Lean into it: it should also mirror the player's *Resonance perks* on Hard (currently normalized), or at least mirror the equipped ability VFX.

---

## 5. Presentation design gaps that are design decisions (not just missing assets)

- **Animation frame budget.** Design targets idle 6–8, run 8–10, basic 11–18/hit, specials 23–41, ultimate ~165 frames. The retro atlases ship **3 frames per animation** for all 26 animations. That is a *style* decision (retro-pulp), not a shortfall — but it needs to be recorded as one, and the frame-callback hitbox model (`:742`, `:903`) needs to be reconciled with 3-frame attacks: active frames are numeric today, which is fine, but the design's "exact impact frame" language no longer describes the pipeline.
- **No cutscene surface exists** (intro/ending are dialogue). Decide whether the "character-specific intro cinematic" (`:2578`) is a dialogue sequence with art or a real animated scene; either way it is currently zero.
- **HUD micro-behaviours** (rewind pulse at 1, dust fade-in top-right, 00:10 timer pulse + chime, ultimate-full flash, radial cooldowns) are all "still open". They are cheap and they are what makes a HUD *read*; schedule them.
- **Input glyphs.** The design specifies device-appropriate `[E]`/`[B]` prompts; the build renders binding names. Needed before any controller-first playtest.
- **Accessibility**: colorblind, text size, hold/toggle block, deadzones are all explicitly deferred by the design. Text-size / UI scale is the one that should not be — the theme hardcodes 18 px and there is no scaling path.

---

## 6. Prioritised design recommendations

**Decide now (they gate other work):**
1. Stage-boundary model (§1.1). Write it into the pillar list.
2. Block model (§1.4): lockout, shieldstun-or-grab, and whether block-cancels-hitstun stays.
3. Online scope for initial release (§1.6).
4. "V7" design-doc reconciliation pass (§3) so implementers stop reading stale numbers.

**Design next (feel):**
5. Hitstop + one hitstun-agency verb (§1.2).
6. Special/normal rebalance and per-ability differentiation (startup / cooldown / hitstun / angle) (§1.3). Fix Lincoln's ultimate and Leonardo's Golden Ratio outliers; give Mozart a real movement ability.
7. Author the three escalating basic hitboxes and per-hit knockback angles in the sim (§1.2) — the design already has them.
8. Movement-ability distinguishing rules (§1.3).

**Design next (campaign):**
9. Per-level mechanic spine — one new system per act, remixed (§2.1); place the unused templates (`TreasureChest`, `SequenceLock`, weights) as secrets and puzzles.
10. Per-boss phase mechanic and cooldowns (§4); one boss intro/arena ritual.
11. Character-specific narrative layer: L0 nexus openings, one hero line per level, per-act Sarah dialogue, portal-charge gate (§2.1, §2.3).
12. Rewind as a verb: manual rewind charge, one world-rewind exemplar, Timeline Collapse presentation (§2.2).
13. Economy unification (§2.4): one exit penalty, extractor idle cycle and 25-dust reward, boss/extractor pickups, and grid choice pressure.

**Design later:**
14. Fighter QoL: settings persistence, rematch/new-stage, handicap, sudden death, stock-mode timer default (§1.2).
15. Enemy behaviour variety (§4).
16. HUD micro-behaviours, glyphs, UI scale (§5).
