# Recon D - design documents to amend (Momentum gauge + active Defy + meter rates), 2026-10-31

Source of rulings: DECISIONS_meter.md. Line numbers 1-based. Master = D:/Projects/fighters-through-time-docs-3/design-godot-v7.md (DG-M); mirror = design-godot.md (DG-R). Line numbers are identical in both.

## 0. Master/mirror sync state
- Both 4973 lines. Ignoring CR line endings, the ONLY content differences are the three DOC-MASTER-UNBOUND-BINDING lines: 2312 (Ultimate row), 2314 (Grab row), 4173 (Input Remapping bullet). Master says "Severed", mirror says "Unbound". Apply every edit to both, identical except those three lines.
- Both are STALE vs the build where this package touches: Momentum is still finisher-only (DG 1125, 1374; build is per-hit since 2026-10-11, D2); no Story meter scale at all (D5 never reached the master); Controls table 2315 shows Time Freeze pad as Back/Select (build: L3).
- Section 5 kit entries carry almost no numeric Special cooldowns (only 2460 Golden Ratio 8 s; band text 1124, 1161). Per-ability numbers live in ABILITY_DATA.md.

## 1. design-godot (apply to master AND mirror)
| Lines | Section | Says now | Must change |
|---|---|---|---|
| 1-45 head (V7.1 addendum ~25; status row "Combat, shields, Rally and Defy" ~14) | Preamble | Defy passive full-meter save; Momentum finisher refund | Add dated revision block 2026-10-31; superseded note on V7.1 addendum summary |
| 102 | Health bar is temporal (Rally, Desperation, Defy) | Defy passive | Defy active, cashes in Rally echo |
| 196 | Level 4 dampening beams drain meter | drain | Story drain x0.5 |
| 299, 305, 309 | Legacy Unlock text, Level 0 row, Level 4 row | "Ultimate Meter + Defy History" Level 0 kit | Defy universal verb with own button from Level 0, never gated; Momentum gauge visible but dimmed until Special 1 (G4.1) |
| 341 | Hazard dmg "so Defy History and Rally..." | passive Defy | reword |
| 389, 392 | First-use tooltips; Level 0 refs "Defy seal" | no Defy tooltip | add Defy tooltip naming button; drop "seal" |
| 400-401 | Level 0 exemptions; Meter & Defy History Calibration | meter set 100, F13 seal, scripted lethal hit | meter set 50, drone swarm traps hero in a string, completes only on a Defy press, show blast + heal, tooltip names button |
| 475-476, 501-510, 537 | Grid minors / StatType CooldownReduction | cooldown stat | three Special cooldown minors become +10% Momentum regen (G4.3) |
| 740-828 (758, 761, 774, 778, 780, 786, 807) | FSM tables | "Special input (if off cooldown)"; 807 "after any eligible Defy" | "(if a pip and slot reuse delay allow)"; add Defy action row; delete passive-Defy clause |
| 913 area, 1635-1637, 1767-1769, 1789, 1791 | Data tables: Special cooldown timers; DefyHistoryUsed/DefyProtection*; MomentumRefundsS1 | cooldown/flag/counter | MomentumPips + per-slot reuse delay; drop DefyHistoryUsed and refund counter; add Defy phase frames |
| 1123-1132 | Special Attacks - Cooldown System (6-14 s band; 1125 Momentum finisher-only; start timing; no pause/reset; CDR; radial UI; input blocking) | per-ability cooldown | Rewrite as Momentum gauge: 4 pips, 1 pip/Special, regen 1 pip/12 s, +0.05/+0.05/+0.2 per landed string hit, no regen pause, 2 s per-slot reuse delay, Specials do not refill, start 2 pips, persists through rewind/Collapse/Snap, Sudden Death resets to 2; HUD pips; movement keeps 5 s; delete band and CDR text |
| 1150, 1161, 1191, 1199 | AbilityData: AbilityIcon "HUD cooldown slot icon"; CooldownDuration 6-14 s; movement 5.0 | authored cooldown | Special: MomentumCost 1 + ReuseDelay 2.0; movement 5.0 stays |
| 1275-1285 | Casting steps ("checks cooldown timer"; "spend on acceptance spends its cooldown") | cooldown | spends 1 pip, arms reuse delay + lockout |
| 1294 | Meter: Damage Dealt 1.0/HP | no Story scale | Story x0.5 (absent from master); flat grants/drains scaled; direct sets and spends not |
| 1295 | Damage Taken 0.25 | - | unchanged |
| 1296 | Defy History (V7.1) lifeline | lethal hit at 100 | active burst, 50 Influence |
| 1297 | Echo Step removed note ("Defy insurance") | wording | reword |
| 1298 | Death Carryover M09: 25% penalty (x0.75) | 75% kept | 50% kept (80 -> 40) |
| 1299 | F14 Siphon Snare 10/s, max 3 s | -10/s, cap 30 | Story -5/s cap 15 |
| 1301 | Activation strike A02 | wind-up armor | Defy legal in the 20-frame wind-up (invulnerable, beats strike), not after hold connects |
| 1334-1367 | Recoverable Health: Rally 1339-1343; item 3 Defy 1350-1361 (1353 no Sudden Death, 1354 no echo, 1355 dual lethal, 1356 presentation, 1361 D03f clause); 1364 snapshot state; 1366 HUD; 1367 tutorial | passive at 100, once per match/level | Replace item 3 wholesale (input, legal anytime except Time Freeze/hold, cost 50, 6 inv. startup frames, blast on 6, ~24 vuln. recovery, blast 2.5 u / 30-frame stagger / no damage / unblockable / no meter or echo / cannot kill, bosses interrupted+pushed no stun, heals Rally echo pool floor 10% max HP, ignores lockout and Suppression, no lockout armed, time-break presentation: 8-frame freeze, gold ring, comfort-gated pulse, "DEFIED" label, meter loses half); delete 1353, 1355; Rally add cash-in; update 1364, 1366, 1367 |
| 1369-1374 | Time Systems header + item 2 Momentum | finisher refund | rewrite as gauge or delete; rename header |
| 1454, 1529 | Specials vs block A01; cancel into Special "if off cooldown" | cooldown | "if a pip is available and gate allows" |
| 1981-1984 | EventBus: OnCooldownStarted/Complete; OnUltimateMeterChanged consumers "Defy Seal" | cooldown events | add OnMomentumChanged; seal consumer -> notch |
| 2096, 2140, 2143 (2138) | Death rewind; Death Rewind "not prevented by Defy"; Collapse "only when Defy does not prevent death" | passive Defy in lethal chain | remove Defy from chain; near 2138 add Defy refused during Time Freeze |
| 2128, 2148 | R03 hold clocks "ability cooldowns"; Collapse/Snap retains meter | - | Momentum regen runs; Momentum retained |
| 2171 | Respawn State M09 | no meter rule | meter 50% |
| 4146-4147 | Overtime; Sudden Death F22 | cooldowns ready, Defy disabled | meter 0, Momentum 2 pips, Defy allowed if earned |
| 2214, 2220 | Save table (attemptState "Defy-used") | defyUsed | drop; add Momentum; playerResourceTimers swap (migration derivation) |
| 2296-2336 | Controls: 2307 Special1, 2308 Special2 ("per-ability cooldown, 6-14 s"), 2309 Movement, 2312*/2314* Ultimate/Grab, 2315 Time Freeze, 2336 Time Freeze cooldown, Dialogue Log row | no Defy row | add Defy row (kbd F, pad R3, direct bind, no chord); Dialogue Log pad -> Back/View; Time Freeze pad -> L3; Special rows "a Momentum pip". Keep Severed/Unbound difference at 2312, 2314 |
| 3040 | Siphon Snare incl. "can disarm an unused full-meter Defy" | clause + -10/s | delete clause; -5/s cap 15 |
| 3189-3190 | Extractor -20 meter; hazard blocking | -20 | Story -10 |
| 3357-3365 | Onboarding: Systems Card 3364, drills 3365 | Momentum = finisher refund | rewrite Momentum section (systems_card_momentum_*); add Defy as universal verb |
| 3376, 3384, 3391-3392, 3437-3453, 3465, 3488-3489, 3506-3507, 3521-3523, 3529, 3538 | HUD: Ultimate bar, M25 lenses, trees DefySeal 20x20, Cooldown1/2/3_Radial, lock states, Story lens sizes, Fighter 54 px lenses | four-state seal; Special sweeps | seal -> gold notch at 50, lit at >=50 and bound; 4 pips under Specials capsule (Story) / each player's Special lenses (Fighter); S1/S2 lenses lose sweep (reuse delay + lockout, dim with no pip); movement lens keeps sweep; Dormant overlay on gauge until Special 1 |
| 3757, 3763, 4971 | Audio: Defy cue; H04 cue list | passive | Defy activation cue (Critical slot, silent until Package 10) |
| 4009-4040 (4024, 4037, 4039) | CPU Fighter AI | Specials "cooldown permitting"; no Defy | Specials need a pip; Defy by band (Easy never; Medium sometimes to break long strings; Hard breaks strings, escapes corners, beats Ultimate wind-up, else keeps meter for Ultimate); Mirror follows band |
| 4324 | Sarah Meter & Defy station line | passive fiction | copy edit (open) |
| 4920 | Eraser entry Siphon Snare | -10/s | -5/s Story |
| 4938, 4949-4951 | Sprites: Defy seal set; lens rows | seal | notch asset; pip row; S1/S2 lens rows no sweep |
| 2570-2581, 2809-2822, 2930-2941 | Grid: Joan Minor Piercing Cooldown, Mozart Minor Wave Cooldown, Lincoln Minor Splitting Cooldown (-10%) | cooldown | +10% Momentum regen placeholder |
| 2789-2790, 2884 | Mozart Requiem shaves Fortissimo 2 s; Cleopatra Vortex Step halves Vortex cooldown | rules | "disabled until the kit pass" |
| 2587 | Joan Shield of Orleans | no meter number found by grep | Story +2.5 if stated |
About 58 places per copy; about 25 substantive rewrites.

## 2. Contracts (docs/design-contracts)
- DEFENSIVE_EFFECTS.md (278): D03f 182-205 incl. 197-205 "Defy, healing and repeated events"; D03h 221-235 (233 Defy clause); D04 237-257 rewrite entirely (60-tick protected recovery, DefyProtection fields 255, dual lethal 247) as active burst; 259-278 eligibility/remaining/validation; passive mentions at 3, 11, 47, 117, 123, 164. ~10 places.
- ABILITY_DATA.md (70): line 7 ("6-14 s band ... Resonance Momentum refunds"); Cooldown column rows 13-34 (Einstein 11/8, Joan 10/11, Leonardo 8/10, Lincoln 13/12, Cleopatra 10/9, Tesla 8/11, Shakespeare 7/7, Mozart 7/12, Tubman 9/10) -> 1 pip + 2 s reuse delay; line 25 Requiem shave and Vortex Step: disabled; movement 5 s stays (36); add Defy universal-verb row. ~6.
- HUD_CONTRACT.md (92): 10, 12; 18-22 and 26 (M25 lenses, sweeps "including Resonance Momentum refunds"); 68; 74 compact Story HUD (Defy badge, capsule, sizes); 82-86 Fighter HUD, Ultimate bar 84, gauges 86; add dated amendment (model: line 74). ~8.
- COMFORT_SETTINGS.md (154): C01c 91-119 (action list 95, chord 109) add Defy direct action; 23, 41, 60, 87 (Defy presentation under Reduced Temporal Effects). ~6.
- FIGHTER_MATCH_RULES.md (91): 21, 33, 63 (Momentum 2 pips), 68 (Defy allowed if earned), 91; stock-loss retention row 25% -> 50%. ~6.
- STORY_PERSISTENCE.md (115): 13, 16-18, 25 (delete death-prevention before rewind), 52, 59 (defyHistoryUsed -> dead data), 68 (Momentum replaces Special cooldown timers), 83, 105; declare derivation. ~8.
- TEMPORAL_STATE_CONTRACT.md (108): Echo Step leftovers: banner 3, row 13, section 22-47, snapshot text 33-35, table headers; add Defy/Momentum under Time Freeze/rewind. ~6.
- CPU_COMBAT_POLICY.md (40): 1-3, dead Echo Step section 27-31, 34; add Defy by band and pip-gated Specials. ~4.
- MIRROR_PARADOX.md (47): table 11-19 add Defy row; 37, 47. ~3.
- ROLLBACK_STATE_CONTRACT.md (107): 40, 41, 43 (Momentum refund counters -> gauge + reuse delays + Defy state), 49, 71, 97, 107. ~6.
- CAMPAIGN_VALIDATION.md 49, 72 (~2); COMBAT_VALIDATION.md 95, 123, 127 (~3); CPU_RECOVERY.md 13 (~1).
- No change: NARRATIVE_RESOLUTION (its seal is the N01 anchor), MULTIPLAYER_DELIVERY, PRODUCTION_SCOPE, DUST_ECONOMY.
Total about 65 places in 12 files.

## 3. Ledger (DESIGN_BUILD_DEVIATIONS.md, 766 lines)
Convention (model: section "Confirmed open deviations - combat and movement update, 2026-10-16", header at 401, intro paragraph, rows `| **ID** - title | quote/ruling | ... |`, closing "index and design lines edited" section at 501 with table `Decision | User row | New rows | Existing rows amended or closed | Design-master, mirror and contract lines the ruling requires`). Prefixes USER-/PROVISIONAL-/VERIFY-/DEFER-/FIX-; superseded rows struck through (e.g. 470). New section and index go after 723-727, before "Historical leads - dispositioned" (729).
Existing rows (ledger line): 311 USER-STORY-METER-SCALE (x0.35 -> x0.5); 312 PROVISIONAL-STORY-METER-SCALE-RULES (flat effects scaled; "Defy comes around less often"); 214 PROVISIONAL-STORY-METER-GAIN-SCOPE; 213 FIX-MOZART-BUILD-RATE-UNREAD (note, deferred to Mozart pass); 281 USER-PER-HIT-MOMENTUM (supersede); 282 PROVISIONAL-HIT-CONFIRM-RULES (refund cap superseded); 280 USER-HIT-CONFIRM-CANCEL, 283-284 freeze-only buffer (keep, cross-ref); 285 USER-NOT-YET-FLASH (no-pip press?); 175/176 USER-ABILITY-LOCKOUT and PROVISIONAL-ABILITY-LOCKOUT-RULES (kept, G2.2); 177; 222 FIX-GRID-SCOPED-COOLDOWN-INVERTED (minors become regen); 444 USER-DEFY-ENDS-ON-OFFENCE (supersede); 445 VERIFY-DEFY-CANCELS-OWN-ROLL (moot); 415 FIX-SIM-VENOM-DEFY-BARRED, 490 FIX-CPU-DEFY-RESERVE-IN-SUDDEN-DEATH (moot); other Defy mentions to inspect: 35, 103, 236, 301, 434, 454, 457, 471, 498, 507, 521 (index row 4.3), 541, 720-721; 417/419 (wording "spends its cooldown"); 714-727 compact HUD rows (Defy badge retired).
New section "Confirmed open deviations - Momentum gauge, active Defy and meter rates, 2026-10-31": USER-MOMENTUM-GAUGE, USER-MOMENTUM-LOCKOUTS-KEPT, USER-MOMENTUM-HUD, USER-DEFY-ACTIVE, USER-DEFY-INPUT, USER-DEFY-LEVEL0-LESSON, USER-STORY-METER-SCALE-0.5, USER-STORY-FLAT-EFFECTS-SCALED, USER-STOCK-LOSS-RETENTION-50, USER-KIT-COOLDOWN-RULES-DISABLED, USER-GRID-COOLDOWN-MINORS-REGEN; PROVISIONAL rows (6/24 frames, 2.5 u, 30-frame stagger, 8-frame freeze, heal floor 10%, directional-strike gain); VERIFY/DEFER-KIT-PASS rows; index table. About 16 existing rows touched, about 25 new rows.

## 4. Size
~58 places x 2 design copies, ~65 contract places in 12 files, ~16 ledger amendments + a new section. AGENTS.md and CLAUDE.md carry much more meter/Defy/Momentum text (out of scope here).

## 5. Open questions
1. Any protection after the Defy burst (old 60-frame D04 window)? Assumed none; USER-DEFY-ENDS-ON-OFFENCE superseded.
2. Pad R3 = RightStick (8), currently Dialogue Log; Time Freeze is L3 (7). Dialogue Log to Back (4): confirm free in project.godot.
3. One Influence meter funds Defy (50) and Ultimate (100): notch semantics, bot/CPU reservation rule.
4. Directional strikes' Momentum gain defaulted +0.05.
5. Does a no-pip Special press fire the D10 not-yet flash?
6. G3.2 "stock loss keep as it was" for Momentum: value kept (assumed).
7. Master never caught up after 2026-10-11: full catch-up edit or delta only?
8. Grid minors: reuse StatType CooldownReduction or new key (ResonanceReaderCoverageTests).
9. Save: Momentum replaces persisted Special cooldown timers; declare derivation for next single bump, no own bump?
10. Rollback: gauge pips + two reuse delays + Defy phase need sim state; 305/310 full, 323 has free bits or new component.
11. Name stays "Defy History" (assumed); Sarah line (DG 4324) and seal art need decisions.
