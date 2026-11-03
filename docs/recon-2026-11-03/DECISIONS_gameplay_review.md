# Gameplay review decisions (2026-11-03)

Source: the gameplay review of `main` at `43d54db6` (570 bot runs, 6,804 CPU-vs-CPU Fighter matches; data in this folder). Nothing is implemented yet; this file records the user's choices round by round.

## Round 1: crowd pressure and enemy variety

| # | Item | Decision |
|---|---|---|
| 1a | Waiters never strike first and hang back; 0.33 mean attackers within 250 px, 2+ attackers 2–6% of the time (PROVISIONAL-ENGAGEMENT-DIRECTOR-RULES) | **Raise the slot counts** (against the recommendation of waiter pokes). This amends the user's 1/2/3 numbers, USER-ENEMY-TURN-TAKING. Exact numbers: see round 2. |
| 1b | Waves are 2–5 mobs with no fodder | **Add swarm waves**: one or two waves per level use a new low-HP fodder mob in larger numbers (6–10). Per-level dust totals unchanged (F05). |
| 2a | Six authored elites never spawn (M-5) | **Leave them unplaced** for now (against the recommendation). M-5 stays open. |
| 2b | Few mobs demand a specific answer | **Soft counters**: traits tied to universal verbs make one verb much better but are never strictly required. No-required-hero rule intact. |

## Round 2: slots, rewards, early kit, bosses

| # | Item | Decision |
|---|---|---|
| 1a+ | New melee slot totals | **2 / 3 / 4** (Easy / Normal / Hard), per side 1 / 2 / 2. Amends USER-ENEMY-TURN-TAKING. Re-measure after, since slots alone may not raise pressure if waiters stay far away. |
| 3a | No reward for good play | **A level rank that pays dust** (against the recommendation of counter + rank without dust). Partly reverses the Chronal Rating retirement (V7.6 ruling 2.A) and touches F05. Funding: see round 3. |
| 3b | All heroes identical on Level 1 | **Special 1 from Level 1** (amends the Legacy Unlock Schedule, V7.5). Rest of the schedule: see round 3. |
| 4a | Bosses decide Normal (49 of 115) | **Wait for human data**: no boss changes, VERIFY-BOSS-HP stays open until a person has played them. |

## Round 3: rank funding, schedule, Hard, level curve

| # | Item | Decision |
|---|---|---|
| 3a+ | Funding rank dust | **Bonus on top** of the 720 / 1,000 level totals (against the recommendation of reallocating the optional pool). Amends F05 and `DustEconomyTests`. Consequence: a strong player can finish the 975 grid early, and grid expansion is still "not now". Size: see round 4. |
| 3b+ | Rest of the Legacy Unlock Schedule | **Shift all one level earlier**: Special 1 on Level 1, Movement on 2 (unchanged), Special 2 on 3, Ultimate on 4, full kit at Level 4. Amends `LegacyUnlockSchedule`; saved `UnlockedLegacyAbilities` will need a declared derivation, and Levels 1–4 need retuning for the larger kit. |
| 4b | Hard wall, 0 of 135 | **Wait for human data.** Watch the number once slots rise to 4. |
| 4c | Erratic level curve | **Set a target curve now**: a tracked ledger row of intended completion by level and difficulty, with no tuning yet. Each bot batch flags levels off the curve. |

## Round 4: rank size and basis, replay, co-op

| # | Item | Decision |
|---|---|---|
| 3a++ | Rank bonus size | **Large: up to 50 dust per level**, at most 750 extra (against the recommendation of 10). The grid can finish around mid-campaign for strong players. Surplus handling: see round 5. |
| 3c | What the rank measures | **Damage avoided + speed** (against the recommendation of combat style). Simple and hard to game; it does not reward expressive play. |
| 5a | Replay modes | **Nothing for now** (against the recommendation of level replay + Boss Rush). New Game+ stays deferred; level select stays debug only. |
| 5b | Story co-op | **Never.** Story is solo; stop designing around the possibility. |

## Round 5: surplus dust, the clock, lock pacing, movement

| # | Item | Decision |
|---|---|---|
| 3a+++ | Surplus dust once the 975 grid is full | **Reopen grid expansion** (reverses that entry of USER-COMBAT-FLOW-NOT-NOW). Size: see round 6. |
| 6 | Integrity clock never bites (0 of 405) | **Keep the fail state, retune later** (against the recommendation of reward-only). F01 stands; par gets retuned from human timings (VERIFY-PAR-SECONDS). The rank's speed score sits alongside it as a second speed system. |
| 7a | 56–76% of level width is locked | **Add traversal between locks.** Every lock stays (USER-ENCOUNTER-LOCKS intact); levels get platforming or hazard stretches between them, which also adds length. Moves anchors, fonts and manifests across 14 level controllers. |
| 7b | No wall jump, dash or pogo | **No new verb, better levels.** Use the existing kit: double jump, ropes, lifts, trapdoors, short hop, fast fall, ledge grab. |

## Round 6: grid size, hero routes, hazards, Story balance

| # | Item | Decision |
|---|---|---|
| 3a++++ | Grid expansion size | **One capstone Major per hero at about 375**, total about 1,350. Nine new nodes. Amends the "9 nodes, 975" statement and the content tests that pin it. |
| 7c | Movement abilities have no level payoff | **One optional route per level, reachable by ability type** (several abilities can clear it), paying dust or a secret. Folds into the traversal work from 7a. No-required-hero rule intact. |
| 7d | Hazards only hurt the hero | **Hazards hit mobs when launched or thrown**: big damage, or a kill for fodder. Mobs walking around stay safe. Pairs with swarm waves and gives grabs a clear use. |
| 8a | Story hero balance (Lincoln 19 of 30, Mozart 2 of 30) | **Wait for human data** (against the recommendation of extending the floor). |

## Round 7: Fighter balance, stages, moveset, tools

| # | Item | Decision |
|---|---|---|
| 8b | Tesla about 85% and Tubman 21–23% in CPU matches | **Keep the matrix as a tool, wait for humans.** No balance change and no pre-kit-pass comparison for now. |
| 9a | Seven of ten stages are Sealed | **Open two more, five of ten.** Real pits through the floor-segment system. Each needs conformance, hazard and CPU recovery checks. |
| 9b | About 10 attacks per hero, no running attack | **Add a running attack in both modes.** No new input bit; needs frame data for all nine heroes and moves every Fighter hash. |
| 9c | No Training Mode | **Basic training room in the Holodeck**: a dummy with block, jump and hitstun settings, plus frame and damage readouts. Reprioritises one out-of-scope item from AGENTS.md. |

## Round 8: CPU meter, slot edge, Special gates, onboarding

| # | Item | Decision |
|---|---|---|
| 9d | Hard CPU spends its meter on Defy (VERIFY-CPU-DEFY-ULTIMATE-STARVE) | **Save for the Ultimate when close**: above about 70 meter, Hard only Defies against an Ultimate wind-up or when near death. Input-side, code-owned in `CpuBandTuning`. |
| 9e | First-slot win rate 54.3% Florence, 48.4% Paris | **Log it and move on.** A ledger row, revisited with Package 7. |
| 10a | Three gates on Specials; 42% of Normal time under one pip | **Faster pip regen** (against the recommendation to judge by hand). The lockout and the reuse delay stay. Value: see round 9. |
| 10b | Nine Level 0 lessons and six recovery systems | **Cut a recovery system** (against the recommendation to trim the lessons). Which one: see round 9. |

## Round 9: regen, the cut system, the play session, Level 6

| # | Item | Decision |
|---|---|---|
| 10a+ | New Momentum regen | **9 s per pip, both modes** (`MomentumRegenFramesPerPip` 720 to 540). Amends USER-MOMENTUM-GAUGE. Fighter speeds up too. |
| 10b+ | Which recovery system to cut | **Time Freeze** (against the recommendation of Fonts). Reverses F03. Removal scope: see round 10. |
| 11 | When the human session happens | **After everything on this list** (against the recommendation of before). Every "wait for human data" item therefore waits for the whole list. |
| 12 | Level 6 vault: five heroes always stall (12 runs) | **Log it as a bot gap** (against the recommendation to investigate). A ledger row records the open soft-lock risk until the session. |

## Round 10: Time Freeze removal, audio, boss arenas, lead mode

| # | Item | Decision |
|---|---|---|
| 10b++ | How Time Freeze goes | **Switch off, keep the code** (against the recommendation of a full deletion). Input, HUD and lesson go; the systems stay dormant until the session confirms it. Tests must cover the off state. |
| 13 | Silent audio | **A small hit-sound set before the session**: hit, block, perfect block, grab, whiff, KO, Defy, wind-up; volume-capped and separately mutable, placeholder until Package 10. Amends the 2026-08-10 silence directive for these cues only. |
| 14 | Six bosses have stand-ins and no arena change (VERIFY-BOSS-PHASE-ARENAS) | **Build all six arena changes**: Duke, Vulcan, Admiral, Chancellor, Siege Cannon, Overseer. |
| 15 | Which mode leads | **Story leads until online exists.** Shared rules favour Story feel; Fighter adapts or takes a recorded exception. |

## Round 11: curve numbers, new pits, trait set, next step

| # | Item | Decision |
|---|---|---|
| 4c+ | Target curve numbers (a bot target; flags only, no tuning) | **Easy about 90% on every level; Normal falling smoothly from 75% at Level 1 to 50% at Level 15; Hard unset** until a stronger bot or human data exists. Levels 6, 7 and 10 carry bot-gap notes. |
| 9a+ | Which two Sealed stages get pits | **Globe and Hakata Seawall (`FighterStage_Berlin`).** Florence stays a Sealed baseline. Open stages become Paris, Vesuvius, Nassau, Globe, Hakata. |
| 2b+ | Starter soft-counter traits | **Armoured** (reduced damage until thrown), **Catcher** (counters a basic string, loses to a throw or Special), **Anti-air** (punishes jump-ins, weak to grounded pressure). |
| Next | How to proceed | **Recon and plan, then the user approves** before anything is built. |

## Open consequences to carry into the plan

- Slots rise while bosses, Hard and hero balance wait for human data, and the session comes after everything, so all of it stacks unjudged. Normal is already 36% for the bot and may fall further.
- The rank pays up to 50 dust a level on damage avoided + speed, so it rewards players already doing well, and it sits beside the Integrity clock as a second speed system.
- Removing Time Freeze removes an escape tool at the same time as crowd pressure rises.
- Waiter behaviour is unchanged, so more slots may not raise pressure if waiters stay far away; re-measure `mob_engagement` after.
- Level 6's vault remains a logged, unchecked soft-lock risk for five heroes.

## Round 12: corrections from recon

| # | Item | Decision |
|---|---|---|
| 7c+ | Route rewards vs F05's reach rule | **Shortcut only**: every hero can reach the reward by a harder base-kit path, and matching abilities make it quick and easy. F05 and the secret reach test hold. Corrects 7c. |
| 3c+ | Rank speed source | **Read Integrity at the PreBoss gate**: one clock, no new save field. Corrects the "second speed system" note; the boss fight does not count toward speed, and Level 1 (no clock) ranks on damage only. |
| 1b+ | Fodder dust | **No dust from fodder** (weight 0). Per-level totals exact. |
| 9a++ | Pit width | **3.0 on both**: Globe centred at [-1.5, 1.5], Hakata a one-sided gap. The matrix tool measures the KO share afterwards. |

## Round 13: rank by difficulty, the Overseer, swarm placement, the Mirror

| # | Item | Decision |
|---|---|---|
| 3a+5 | Rank dust by difficulty | **Easy pays half**: S 25 / A 18 / B 10 / C 4 / D 0 on Easy (exact rounding for the plan), S 50 / A 35 / B 20 / C 8 / D 0 on Normal and Hard. Letters are normalised for difficulty. |
| 14+ | Overseer Phase 2 (Level 12) | **Cycling light and heavy gravity fields**: telegraphed zones at 0.15 and 2.5, no literal inversion. |
| 1b++ | Swarm placement | **One wave per level, two only on the largest locks, none on Levels 0, 12, 13 and 14**, inside the 30-mob warm-pool limit. |
| 9d+ | Mirror and the Hard CPU meter rule | **Fighter CPU only**: the Level 13 clone is exempt, in line with waiting for human data on bosses. |

## Round 14: plan questions and approval

| # | Item | Decision |
|---|---|---|
| Q1 | The nine capstone effects | **Accept as listed, provisional** (`PROVISIONAL-CAPSTONE-EFFECTS`). |
| Q2 | Gate thresholds (Normal under 20 %, Easy under 75 %, new stall clusters) | **Report and keep building** (against the recommendation to stop). No automatic tuning either way. |
| Q3 | Levels 1–4 after the schedule shift | **Flags only, retune after the session**, unless a level is plainly broken. |
| Q4 | Fodder display name | **Chrono Swarmling** (default accepted). |
| Q5 | Training room location | **Hub console only** (default accepted). |
| Build | Approval | **Build the whole plan.** Nothing is pushed or merged to `main` without the user. |
