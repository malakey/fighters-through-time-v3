# Recon D: tests, docs, bots, CPU (2026-10-31, HEAD ce813449, baseline 3879)

Recorded by the orchestrator from the Explore agent's inline report (the agent was read-only). Counts are grep estimates: `[TestCase]` count / lines naming the abilities.

## 1. Tests
- Determinism: TeslaLorentzChainTests 5/29, TeslaUltimateTests 7/26, TeslaKitTests 5/21, TeslaBlinkSimTests 3/11, LeonardoTurretSimTests 2/5, LeonardoKitTests 4/10, LeonardoUltimateTests 5/8, CleopatraKitTests 5/22, CleopatraUltimateTests 5/18, ShakespeareKitTests 5/23, ShakespeareUltimateTests 6/3, EinsteinUltimateTests 6/4, RiftCollapseSimTests 3/17, FighterConstructAttackTests 3/7, RelativityWarpFoldSimTests 3/2, FighterSpecialRecoveryCancelTests 16/29, FighterStatusSlotTests 4/6, FighterSpecialTimingTests 18/7, FighterHitConfirmTests 18/5, FighterMomentumGaugeTests 11/7.
- Unit: StatusControllerTests 7/23, StoryRiftCollapseTests 4/49, StoryLorentzChainTests 3/14, StoryTempestWindboxTests 2/11, PlaceholderZoneTests 3/13, NonSolidConstructTests 4/8, ConductiveMarkTests 7/15, StoryKitMotionTravelTests 8/9, StoryReachKitTests 4/4, StoryProsperoGustTests 2/4, LeonardoMidGlideBoltTests 2/8, CleopatraSandDecoyTests 2/9, CleopatraVortexRallyTests 1/8, AbilityZoneTalentTests 4/14, ResonanceTraversalTests 10/31, ResonanceMajorReworkTests 7/13, StoryAbilityDamageRoutingTests 5/10, StoryMovementAbilityGravityTests 7/11, HudAbilityIndicatorModelTests 13/13.
- ContentValidation: TeslaContentTests 5/10, CleopatraContentTests 4/16, EinsteinContentTests 3/9, LeonardoContentTests 4/8, ShakespeareContentTests 3/13, KitAlignmentContractTests 4/5, AbilityHitContractTests 6/6, UltimateActivationContentTests 3/3, ResonanceReaderCoverageTests 5/2.

## 2. .tres (resources/Abilities/<id>/)
- tesla/special_1 coil: BaseDamage 5, HitCount 1, 12/6/18, Lifetime 30, MaxActiveObjects 2, Delivery 2 (Construct), TeslaCoil.tscn.
- tesla/special_2 Lorentz Pulse: BaseDamage 20, HitCount 1, 14/6/20, Lifetime 0.25, StatusDuration 1.0, Delivery 0.
- leonardo/special_1 Golden Ratio: BaseDamage 8, HitCount 3, 12/9/18, Launches true, Lifetime 0.7.
- leonardo/special_2 turret: BaseDamage 6, HitCount 4, ProjectileSpeed 720, 12/9/18, Lifetime 15, MaxActiveObjects 1, Delivery 2, ClockworkTurret.tscn.
- cleopatra/special_1 serpent nest: BaseDamage 6, HitCount 1, 12/6/18, Lifetime 12, MaxActiveObjects 1, StatusDuration 4.0, Delivery 2, SerpentNest.tscn.
- cleopatra/special_2 Vortex: BaseDamage 4, HitCount 5, Launches true, 12/15/16, Lifetime 2.0, StatusDuration 2.0, StatusIntensity 0.8, Delivery 1 (Tick).
- einstein/special_1 E=mc2: BaseDamage 20, HitCount 1, Launches true, ProjectileSpeed 720, 14/6/20 (the 7 contact stage is separate, per the burst primitive).
- einstein/special_2 Rift: BaseDamage 3, HitCount 6, Launches true, 12/18/18, Lifetime 3.0, StatusDuration 0.5, Delivery 1.
- shakespeare/special_1 Yorick: BaseDamage 16, HitCount 1, Launches true, ProjectileSpeed 600, 10/6/14, StatusDuration 2.5, StatusIntensity 0.6.

## 3. Localization (localization/en.csv)
ability_einstein_relativity_rift_* (L51-52), ability_leonardo_golden_ratio_* (L65-66), ability_leonardo_clockwork_turret_* (L67-68), ability_cleopatra_serpent_nest_* (L81-82), ability_cleopatra_sandstorm_vortex_* (L83-84), ability_tesla_tesla_coil_* (L89-90), ability_tesla_lorentz_pulse_* (L91-92), ability_shakespeare_yoricks_lament_* (L97-98); status_timedilation (L250), status_venom (L251); Resonance minors L1015-1130, L1150; construct strings L314-315, L371. The Move List (scripts/UI/MoveListScreen.cs) loads `{id}_data.tres` abilities through AuthoredResources.Load.

## 4. Design docs
design-godot.md Section 5: Einstein 2498, Leonardo 2606, Tesla 2665, Shakespeare 2724, Cleopatra 2849 (grids to ~2900); coil/turret specs at 938 and 947. ABILITY_DATA.md: Specials 11-33 (Einstein 15-16, Leonardo 19-20, Tesla 21-22, Shakespeare 23-24, Cleopatra 27-28), Movement 34-49, Ultimates 50-65, Open values 72-74. The master `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md` has the same heading lines.

## 5. Ledger rows
VERIFY-SERPENT-NEST (closed), VERIFY-VORTEX-RECLAIM (closed), DEFER-FIGHTER-SAND-DECOY, VERIFY-ULT-FINALE-ORDER, ZONE-RESOLVED-HIT-FREEZE, F5-CASTER-FREEZE-ATTRIBUTION, FIX-TESLA-CONDUCTIVE-HOLD-UNREAD (closed), FIX-TESLA-PULSE-RADIUS-UNREAD (closed), PROVISIONAL-STORY-ABILITY-DAMAGE-ROUTING, USER-SPECIAL-RECOVERY-CANCEL, USER-FIGHTER-SPECIAL-RECOVERY-CANCEL, FIX-STORY-CONSTRUCT-GRAYBOX, FIX-SIM-DIRECT-HIT-CAST-ATTRIBUTION (closed), **DEFER-KIT-PASS-MOMENTUM** (open), PROVISIONAL-GRID-MOMENTUM-REGEN, USER-REQUIEM-SHAVE-DISABLED, USER-VORTEX-STEP-KEPT.

## 6. CPU and bots
FighterCpuController reads no constructs or zones. CpuRecoveryProfile.cs:53-54, :297 lists Rift, Lorentz, Vortex and the turret as non-recovery. Bots read the puzzle ConductiveCoil, not the Tesla construct; PlaytestCombatStats counts Venom ticks as periodic; no bot reads Venom/TimeDilation directly (bot nav reads the status multipliers, per recon B).

## 7. Presentation
FighterSimulationDriver.cs:46-56 proxy dictionaries; persistent proxies ~458-469 (ConfigureProxyHPBar), zone proxies ~504-517. Story construct HPBar at line 34 of TeslaCoil.tscn, ClockworkTurret.tscn, SerpentNest.tscn.

## Venom / TimeDilation code sites
StatusSlots.cs:72,74; StatusController.cs:150-151; GlowStateModel.cs:226,229; EventBus.cs:411-412; sources CleopatraAbilities.cs:281,634, ShakespeareAbilities.cs:119,150, EinsteinRelativityRift.cs:155,200, EinsteinEmc2Blast.cs:194-197, SerpentNestNode.cs:163, DilationFieldZone.cs:38,55, ChronalRiftZone.cs:46; intake EnemyController.cs:2337-2411, BossController.cs:1863-1933; sim FighterSimulationSystems.cs:645,1529, FighterEntitySystems.cs:1095,2705,2718; HUD HudAbilityIndicatorModel.cs:264,388-389,418-419, FighterHudModel.cs:297-298.
