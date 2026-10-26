using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// Mode-neutral snapshot the Hard/Normal/Easy CPU utility decision table reads.
    /// The deterministic Fighter path fills it straight from
    /// <see cref="FighterStateComponent"/>/<see cref="FighterRuntimeComponent"/> plus
    /// the stage geometry and an <see cref="ICpuWorldObserver"/>, so its fixed-point
    /// values stay bit-exact. A Story-side adapter (the Level 13 Mirror Paradox) fills
    /// the same fields from Godot state, which lets both modes share one decision
    /// engine without <c>scripts/FighterSim/</c> gaining any Godot or Story dependency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Positions are raw <c>FP64</c> values in world units — never pixels — with
    /// <b>+Y up</b>. Story callers must divide by their pixels-per-unit factor and
    /// negate Y (Godot screen space is +Y down) before converting.
    /// </para>
    /// <para>
    /// Every optional block carries an explicit presence flag whose <c>0</c> default
    /// means "this concept does not exist here": <see cref="HasStageBounds"/>,
    /// <see cref="HasOrb"/>, <see cref="HasHazard"/>, <see cref="HasFloorSegments"/>.
    /// A Story adapter leaves them all at zero, which is what keeps stage-relative
    /// behaviour (off-stage recovery, pit awareness, hazard avoidance, orb pursuit)
    /// from firing inside a campaign level where the floor is nowhere near
    /// <c>y = 0</c>. <see cref="SuppressGameplayInput"/> is
    /// likewise defaulted to "live" so a hand-built observation behaves normally.
    /// </para>
    /// </remarks>
    public struct CpuDecisionObservation {
        // === Self ===
        public long SelfPositionXRaw;
        /// <summary>World-space Y (up positive). The stage floor plane is 0.</summary>
        public long SelfPositionYRaw;
        public long SelfVelocityXRaw;
        public long SelfVelocityYRaw;
        public int Stocks;
        public int HitstunFrames;
        public int DazeFrames;
        public int IsGrounded;
        /// <summary>
        /// Non-zero while the fighter hangs off a one-way platform end
        /// (gameplay-feel plan §2.11). A hang suppresses every attack and ability,
        /// so the decision table's only useful answer is "climb": Jump exits the
        /// hang without spending an air jump. Story fills this from
        /// <c>CharacterState.LedgeHanging</c>.
        /// </summary>
        public int IsLedgeHanging;
        /// <summary>Air jumps still available this airtime.</summary>
        public int RemainingJumps;
        public int SelfCurrentHP;
        public int SelfMaxHP;
        public long InfluenceRaw;
        public int SpecialOneCooldownFrames;
        public int SpecialTwoCooldownFrames;
        public int MovementCooldownFrames;
        /// <summary>
        /// Frames left on the self ability lockout A (special global cooldown,
        /// user direction 2026-10-08): while positive the Ultimate would be
        /// refused, and so would Special 1 and Special 2 unless the current
        /// swing has landed (D1, 2026-10-11 — see <see cref="SpecialLockoutFrames"/>
        /// and <see cref="SwingLanded"/>), so the decision table does not pick
        /// them. The sim fills it from
        /// <c>FighterRuntimeComponent.AbilityLockoutFrames</c>, Story from
        /// <c>PlayerController.AbilityLockoutFramesRemaining</c>; 0 (a hand-built
        /// observation) means clear.
        /// </summary>
        public int AbilityLockoutFrames;
        /// <summary>
        /// D1 (2026-10-11): frames left on the Special-sourced lockout S (armed
        /// with A by every Special or Ultimate acceptance). The sim fills it from
        /// <c>FighterRuntimeComponent.SpecialLockoutFrames</c>, Story from
        /// <c>PlayerController.SpecialLockoutFramesRemaining</c>; 0 by default.
        /// </summary>
        public int SpecialLockoutFrames;
        /// <summary>
        /// D1 (2026-10-11): 1 once the CPU's current swing has landed (D1a), so
        /// its Specials read <see cref="SpecialLockoutFrames"/> instead of
        /// <see cref="AbilityLockoutFrames"/>. 0 (a hand-built observation)
        /// keeps the Special gate equal to the whole lockout.
        /// </summary>
        public int SwingLanded;
        /// <summary>
        /// The fighter's basic-string reach-width percent
        /// (<c>BasicStringProfile.ReachWidthPercent</c>, 85–120), the one input
        /// the neutral melee band needs from the fighter's own kit
        /// (<see cref="FighterCpuController.MeleeBand(int,int,long)"/>, 2026-10-11). The sim
        /// fills it from the fighter's character ID, the Mirror adapter from the
        /// clone's hero ID. 0 (a hand-built observation) means unknown, and the
        /// neutral BasicAttack press then keeps the legacy close range.
        /// </summary>
        public int SelfStringReachWidthPercent;
        /// <summary>
        /// The string step a BasicAttack press would continue into right now:
        /// the next hit's index (1 or 2) while the fighter's own chain swing of
        /// hit 1 or hit 2 runs, any phase, the chain hold included; otherwise 0.
        /// It is 0 at rest, during the finisher (which cannot continue) and
        /// during a directional swing (which stands outside the chain), because
        /// a press there can at most open a fresh string. The melee band reads
        /// this step's box (<see cref="FighterCpuController.MeleeBand(int,int,long)"/>),
        /// at decision time and again, from the then-current value, when the
        /// press is delivered. The sim fills it from
        /// <c>FighterBasicAttackRules.NextChainStep</c>, Story from
        /// <c>PlayerController.NextChainStepForPress</c>; 0 (a hand-built
        /// observation) reads an opener.
        /// </summary>
        public int SelfNextStringStep;
        /// <summary>
        /// CM-CANCEL (combat and movement update 2026-10-16, decision 9.3, plan
        /// §2.9.3): the CPU's own Special cast phase, mode-neutral —
        /// <see cref="CpuCastPhase.None"/>, <see cref="CpuCastPhase.Startup"/>,
        /// <see cref="CpuCastPhase.Active"/> or <see cref="CpuCastPhase.Recovery"/>.
        /// Since decision 1.2 every Special plays its authored startup, active and
        /// recovery, and the cast discards the caster's presses, so the decision
        /// table reads its own cast before scheduling a Special, the Ultimate, a
        /// grab. The sim fills it through the world observer's verb
        /// layer from component 323 (Tubman's Delegated Foresight from component
        /// 322); the Mirror adapter from the clone's executing Special. 0 (a
        /// hand-built observation) is idle.
        /// </summary>
        public int SelfSpecialCastPhase;
        /// <summary>
        /// CM-CANCEL: frames left in <see cref="SelfSpecialCastPhase"/> (component
        /// 323's <c>SpecialPhaseFrames</c>, 322's <c>PhaseFrames</c>, or the
        /// clone's <c>BaseSpecial.PhaseFramesRemaining</c>); 0 when idle.
        /// </summary>
        public int SelfSpecialCastPhaseFrames;

        // === Target ===
        public long TargetPositionXRaw;
        public long TargetPositionYRaw;
        public int TargetCurrentHP;
        public int TargetMaxHP;
        public int TargetHitstunFrames;
        /// <summary>Opponent buttons pressed this tick, as a <c>GameplayButtons</c> bit mask.</summary>
        public int TargetPressedButtons;
        /// <summary>Opponent Ultimate meter, raw <c>FP64</c> on the 0–100 scale.</summary>
        public long TargetInfluenceRaw;
        /// <summary>
        /// Horizontal half-width of the hurtbox a basic swing must reach on the
        /// opponent, raw <c>FP64</c> world units. The sim fills its universal
        /// victim half-width, the Mirror adapter the campaign hero's real
        /// hurtbox. 0 (a hand-built observation) means the sim's value.
        /// </summary>
        public long TargetHurtboxHalfWidthRaw;

        // === Stage bounds (absent when HasStageBounds == 0) ===
        public int HasStageBounds;
        public long LeftWallRaw;
        public long RightWallRaw;
        public long CeilingRaw;
        public long BottomBlastZoneRaw;
        /// <summary>
        /// Authored one-way platform count. Non-zero also means the stage's base
        /// floor is solid (<c>FighterMovementSystem</c> uses the same switch), so a
        /// fighter below the floor plane there is genuinely off-stage.
        /// </summary>
        public int PlatformCount;
        public long NearestPlatformCenterXRaw;
        public long NearestPlatformSurfaceYRaw;
        public long NearestPlatformHalfWidthRaw;

        // === Main-floor topology (Package 11 A9; absent when HasFloorSegments == 0) ===
        /// <summary>
        /// Non-zero on an <b>Open</b> stage — one whose main floor is authored as
        /// segments with real pits between them (Paris, Vesuvius, Nassau). Zero
        /// means the floor is unbroken wall to wall, which covers every Sealed
        /// stage, the legacy flat arena and every Story adapter. A consumer must
        /// read zero as "there are no gaps to fall into": the design is explicit
        /// that closed stages and ordinary supported traversal never trigger
        /// emergency recovery.
        /// </summary>
        public int HasFloorSegments;
        /// <summary>
        /// Non-zero when solid main floor exists directly under the fighter's
        /// current X. Always 1 on a stage with an unbroken floor, so "unsupported
        /// over a gap" is <c>HasFloorSegments != 0 &amp;&amp;
        /// HasFloorSupportUnderSelf == 0</c>.
        /// </summary>
        public int HasFloorSupportUnderSelf;
        /// <summary>
        /// Non-zero when the fighter's current trajectory — X plus the horizontal
        /// distance covered while the current vertical velocity carries them back
        /// down to the floor plane — ends over a gap rather than over floor. This
        /// is the pit-risk signal DI and recovery planning read; zero whenever
        /// <see cref="HasFloorSegments"/> is zero.
        /// </summary>
        public int LaunchTrajectoryCrossesGap;
        /// <summary>
        /// Presence flag for <see cref="NearestFloorEdgeLeftXRaw"/>: the nearest
        /// main-floor segment end facing a pit to the fighter's LEFT.
        /// </summary>
        public int HasFloorEdgeLeft;
        /// <summary>Raw <c>FP64</c> X of that edge. Meaningless when the flag is 0.</summary>
        public long NearestFloorEdgeLeftXRaw;
        /// <summary>The same pair, to the fighter's RIGHT.</summary>
        public int HasFloorEdgeRight;
        public long NearestFloorEdgeRightXRaw;
        /// <summary>
        /// Width of the gap the fighter is currently over, raw <c>FP64</c>; zero
        /// when they are supported or the floor is unbroken. Derived from the two
        /// bounding segment ends, so it is exact fixed-point geometry, not an
        /// estimate.
        /// </summary>
        public long CurrentGapWidthRaw;

        // === Verb layer (Package 11 A9b; absent when HasVerbState == 0) ===
        /// <summary>
        /// Presence flag for the whole self verb block. Non-zero only when the
        /// caller supplied an <see cref="ICpuWorldObserver"/> that could resolve
        /// this fighter's verb component — i.e. the deterministic Fighter path.
        /// Zero on every Story adapter, which is what keeps the F19 grab
        /// branch out of a campaign level entirely. Every other
        /// field in this block is meaningless while it is zero.
        /// </summary>
        public int HasVerbState;
        /// <summary>1 facing right, 0 facing left. Grab reach is front-only.</summary>
        public int SelfFacingRight;
        /// <summary>
        /// The fighter's own basic-string phase (<c>FighterBasicAttackRules</c>:
        /// 0 none, 1 startup, 2 active, 3 recovery, 4 chain hold).
        /// </summary>
        public int SelfAttackPhase;
        /// <summary>0 none, 1 startup, 2 active, 3 whiff recovery, 4 holding, 5 throw.</summary>
        public int SelfGrabPhase;
        /// <summary>1 while this fighter is held by the opponent's grab.</summary>
        public int SelfBeingHeld;
        public int SelfShieldStunFrames;
        /// <summary>Non-zero while a universal roll runs (startup, travel or recovery).</summary>
        public int SelfRolling;
        /// <summary>
        /// CM-CANCEL (fix round A's CPU half): frames left in the CPU's own
        /// landing-tech action lock (<c>FighterVerbComponent.TechLockoutFrames</c>).
        /// The sim's grab gate refuses while it runs, so the CPU's mirror does too.
        /// </summary>
        public int SelfTechLockoutFrames;
        /// <summary>
        /// CM-CANCEL: non-zero while the CPU is in an M05 knockdown or its get-up
        /// (component 320), which the sim's grab gate refuses (fix round A).
        /// </summary>
        public int SelfKnockedDown;
        /// <summary>
        /// CM-CANCEL: non-zero while the CPU's own Ultimate activation is busy —
        /// wind-up, active, whiff recovery or cinematic (A02, component 321) —
        /// which the sim's grab gate refuses (fix round A).
        /// </summary>
        public int SelfUltimateCasterBusy;

        // === Target verb layer (absent when HasTargetVerbState == 0) ===
        /// <summary>Presence flag; see <see cref="HasVerbState"/>.</summary>
        public int HasTargetVerbState;
        public int TargetIsGrounded;
        /// <summary>Non-zero while the opponent holds a functioning block stance.</summary>
        public int TargetBlockStance;
        public int TargetShieldStunFrames;
        public int TargetInvulnerabilityFrames;
        public int TargetDazeFrames;
        public int TargetRolling;
        /// <summary>Non-zero while the opponent's post-throw regrab immunity runs.</summary>
        public int TargetThrowImmune;

        // === Nearest live Chronal Orb (absent when HasOrb == 0) ===
        public int HasOrb;
        /// <summary>0 heal, 1 speed, 2 jump, 3 aegis — matches <c>FighterOrbSystem</c>.</summary>
        public int OrbEffectType;
        public long OrbPositionXRaw;
        public long OrbPositionYRaw;

        // === Most relevant stage hazard (absent when HasHazard == 0) ===
        public int HasHazard;
        /// <summary>0 = telegraphed warning, 1 = active/damaging.</summary>
        public int HazardPhase;
        public long HazardPositionXRaw;
        public long HazardPositionYRaw;
        public long HazardHalfWidthRaw;

        // === Nearest hostile projectile (absent when HasHostileProjectile == 0) ===
        public int HasHostileProjectile;
        /// <summary>Projectile position minus self position, raw <c>FP64</c> world units (+Y up).</summary>
        public long ProjectileRelativeXRaw;
        public long ProjectileRelativeYRaw;
        /// <summary>
        /// Projectile velocity, raw <c>FP64</c> world units per second (+Y up). The
        /// decision table derives "closing" from the relative-position sign against
        /// the X velocity sign, so an adapter that only knows a travel direction can
        /// supply any magnitude with the correct sign.
        /// </summary>
        public long ProjectileVelocityXRaw;
        public long ProjectileVelocityYRaw;

        // === Gating ===
        /// <summary>
        /// Non-zero while the match is not accepting gameplay input (countdown,
        /// results, any future non-live <c>MatchState</c>). The decision table then
        /// emits no buttons and no movement. Zero — the default — means live.
        /// </summary>
        public int SuppressGameplayInput;
    }

    /// <summary>
    /// Read-only window onto the transient world entities the CPU reasons about.
    /// It exists so <see cref="FighterCpuController"/> can see orbs and hazards
    /// without the driver's per-frame call site having to marshal them, and so tests
    /// can script an orb or a telegraphed hazard without standing up a simulation.
    /// Implementations must be pure reads: the CPU is an input source and never
    /// mutates simulation state.
    /// </summary>
    public interface ICpuWorldObserver {
        /// <summary>Non-live match states suppress every gameplay button.</summary>
        bool IsMatchLive { get; }

        /// <summary>Nearest live orb to <paramref name="selfPosition"/>, if any.</summary>
        bool TryGetNearestOrb(in FPVector2 selfPosition, out FighterOrbComponent orb);

        /// <summary>
        /// The hazard the fighter most needs to care about: the one whose region is
        /// nearest to <paramref name="selfPosition"/>. Warning-phase hazards are
        /// preferred over active ones at equal distance so the CPU can leave before
        /// the first damage tick.
        /// </summary>
        bool TryGetRelevantHazard(in FPVector2 selfPosition, out FighterHazardComponent hazard);

        /// <summary>
        /// Nearest live projectile owned by any <i>other</i> fighter — the shot the
        /// CPU may need to block or blink through (M-8). Own projectiles are never
        /// reported.
        /// </summary>
        bool TryGetNearestHostileProjectile(
            int selfPlayerID, in FPVector2 selfPosition, out FighterProjectileComponent projectile);

        /// <summary>
        /// Package 11 A9b (F19 verb policy): the verb-layer state of one player —
        /// grab phases, shieldstun and throw immunity. Returning
        /// <c>false</c> leaves <see cref="CpuDecisionObservation.HasVerbState"/>
        /// (or <c>HasTargetVerbState</c>) at zero, which disables grabs
        /// outright — the default for every observer that has no verb layer,
        /// including every Story adapter.
        /// </summary>
        bool TryGetVerbState(int playerID, out CpuVerbState state) {
            state = default;
            return false;
        }
    }

    /// <summary>
    /// Mode-neutral projection of one fighter's verb-layer state, so the shared
    /// decision table can reason about grabs without
    /// <see cref="CpuDecisionObservation"/> taking a dependency on the
    /// deterministic component types. Package 11 A9b.
    /// </summary>
    public struct CpuVerbState {
        /// <summary>0 none, 1 startup, 2 active, 3 whiff recovery, 4 holding, 5 throw.</summary>
        public int GrabPhase;
        public int BeingHeld;
        public int ShieldStunFrames;
        public int ThrowImmunityFrames;
        /// <summary>Non-zero while the fighter holds a functioning block stance.</summary>
        public int BlockStance;
        /// <summary>CM-CANCEL: the fighter's own Special cast phase (<see cref="CpuCastPhase"/>).</summary>
        public int SpecialCastPhase;
        /// <summary>CM-CANCEL: frames left in <see cref="SpecialCastPhase"/>.</summary>
        public int SpecialCastPhaseFrames;
        /// <summary>CM-CANCEL: frames left in the landing-tech action lock.</summary>
        public int TechLockoutFrames;
        /// <summary>CM-CANCEL: non-zero during an M05 knockdown or its get-up.</summary>
        public int KnockedDown;
        /// <summary>CM-CANCEL: non-zero while the fighter's own Ultimate activation is busy (A02).</summary>
        public int UltimateCasterBusy;
    }

    /// <summary>
    /// CM-CANCEL (combat and movement update 2026-10-16, decision 9.3): the
    /// mode-neutral Special cast phase the CPU decision table reads
    /// (<see cref="CpuDecisionObservation.SelfSpecialCastPhase"/>). Its values
    /// equal <c>FighterSpecialCastRules</c>' clocked phases 0–3; Tubman's
    /// Delegated Foresight maps its component-322 stance onto them (startup →
    /// Startup, window → Active, whiff recovery and sidestep → Recovery), and
    /// the Mirror adapter maps the clone's <c>AbilityPhase</c> the same way.
    /// </summary>
    public static class CpuCastPhase {
        public const int None = 0;
        public const int Startup = 1;
        public const int Active = 2;
        public const int Recovery = 3;

        /// <summary>
        /// The sim's projection: component 323's clocked phase and its frames
        /// left, or for a Delegated cast (Foresight) component 322's phase and
        /// frames. Read-only.
        /// </summary>
        public static int FromSim(
            in FighterActionStateComponent cast, in FighterCounterComponent counter, out int phaseFrames) {
            if (FighterSpecialCastRules.IsCasting(in cast)) {
                phaseFrames = cast.SpecialPhaseFrames;
                return cast.SpecialPhase;
            }
            if (FighterSpecialCastRules.IsDelegated(in cast)) {
                phaseFrames = counter.PhaseFrames;
                return counter.Phase switch {
                    FighterForesightRules.PhaseStartup => Startup,
                    FighterForesightRules.PhaseWindow => Active,
                    FighterForesightRules.PhaseWhiffRecovery => Recovery,
                    FighterForesightRules.PhaseSidestep => Recovery,
                    // A Delegated marker whose stance already ended clears on the
                    // next movement pass; until then the cast lock still holds.
                    _ => Recovery
                };
            }
            phaseFrames = 0;
            return None;
        }
    }

    /// <summary>
    /// <see cref="ICpuWorldObserver"/> backed by the authoritative simulation. The
    /// driver hands one to the CPU at construction; queries run against whatever
    /// frame the simulation currently holds, which is the same state the CPU's
    /// component observation is taken from.
    /// </summary>
    public sealed class FighterSimulationWorldObserver : ICpuWorldObserver {
        private readonly FighterSimulation _simulation;
        private readonly List<FighterOrbComponent> _orbs = new(16);
        private readonly List<FighterHazardComponent> _hazards = new(16);
        private readonly List<FighterProjectileComponent> _projectiles = new(16);

        public FighterSimulationWorldObserver(FighterSimulation simulation) {
            _simulation = simulation;
        }

        public bool IsMatchLive => _simulation != null && _simulation.GetMatchState().MatchState == 1;

        public bool TryGetNearestOrb(in FPVector2 selfPosition, out FighterOrbComponent orb) {
            orb = default;
            if (_simulation == null) return false;
            _simulation.CopyOrbsTo(_orbs);
            bool found = false;
            FP64 best = FP64.Zero;
            for (int index = 0; index < _orbs.Count; index++) {
                FP64 distance = FP64.Abs(_orbs[index].Position.x - selfPosition.x)
                    + FP64.Abs(_orbs[index].Position.y - selfPosition.y);
                if (found && distance >= best) continue;
                best = distance;
                orb = _orbs[index];
                found = true;
            }
            return found;
        }

        public bool TryGetRelevantHazard(in FPVector2 selfPosition, out FighterHazardComponent hazard) {
            hazard = default;
            if (_simulation == null) return false;
            _simulation.CopyHazardsTo(_hazards);
            bool found = false;
            FP64 best = FP64.Zero;
            for (int index = 0; index < _hazards.Count; index++) {
                FighterHazardComponent candidate = _hazards[index];
                FP64 gap = FP64.Abs(candidate.Position.x - selfPosition.x) - candidate.HalfExtents.x;
                if (gap < FP64.Zero) gap = FP64.Zero;
                // Warning-phase hazards win ties: leaving before the first damage
                // tick is the whole point of the telegraph.
                if (found && (gap > best || (gap == best && candidate.Phase != 0))) continue;
                best = gap;
                hazard = candidate;
                found = true;
            }
            return found;
        }

        public bool TryGetNearestHostileProjectile(
            int selfPlayerID, in FPVector2 selfPosition, out FighterProjectileComponent projectile) {
            projectile = default;
            if (_simulation == null) return false;
            _simulation.CopyProjectilesTo(_projectiles);
            bool found = false;
            FP64 best = FP64.Zero;
            for (int index = 0; index < _projectiles.Count; index++) {
                if (_projectiles[index].OwnerPlayerID == selfPlayerID) continue;
                FP64 distance = FP64.Abs(_projectiles[index].Position.x - selfPosition.x)
                    + FP64.Abs(_projectiles[index].Position.y - selfPosition.y);
                if (found && distance >= best) continue;
                best = distance;
                projectile = _projectiles[index];
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Package 11 A9b: the verb layer straight out of the authoritative
        /// frame. Block stance comes from the shared
        /// <c>FighterBasicAttackRules.IsBlockStance</c> predicate rather than a
        /// second copy of the rule, so "the opponent is shielding" means exactly
        /// what it means everywhere else in the simulation.
        /// </summary>
        public bool TryGetVerbState(int playerID, out CpuVerbState state) {
            state = default;
            if (_simulation == null) return false;
            if (!_simulation.TryGetFighter(playerID, out FighterStateComponent fighter)) return false;
            if (!_simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)) return false;
            if (!_simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)) return false;
            // CM-CANCEL (decision 9.3): the fighter's own cast (323, or Foresight's
            // 322 stance), the tech lock, the knockdown (320) and the Ultimate
            // activation (321) — the self-state the sim's Special, Ultimate, grab
            // gates read. A missing component reads idle.
            _simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action);
            _simulation.TryGetFighterCounter(playerID, out FighterCounterComponent counter);
            _simulation.TryGetFighterKnockdown(playerID, out FighterKnockdownComponent knockdown);
            _simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation);
            int castPhase = CpuCastPhase.FromSim(in action, in counter, out int castPhaseFrames);
            state = new CpuVerbState {
                GrabPhase = verb.GrabPhase,
                BeingHeld = verb.BeingHeld,
                ShieldStunFrames = verb.ShieldStunFrames,
                ThrowImmunityFrames = verb.ThrowImmunityFrames,
                BlockStance = FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb) ? 1 : 0,
                SpecialCastPhase = castPhase,
                SpecialCastPhaseFrames = castPhaseFrames,
                TechLockoutFrames = verb.TechLockoutFrames,
                KnockedDown = FighterKnockdownRules.IsActive(in knockdown) ? 1 : 0,
                UltimateCasterBusy = FighterUltimateActivationRules.IsCasterBusy(in activation) ? 1 : 0
            };
            return true;
        }
    }
}
