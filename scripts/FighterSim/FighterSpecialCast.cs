using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Klotho component <b>323</b> (combat and movement update 2026-10-16, plan
    /// §1.5; allocated by CM-SIM in Phase A0 and by nobody else): the per-fighter
    /// action state the update's sim rules need. Nine ints = <b>36 bytes per
    /// fighter, 72 per snapshot</b>. Snapshot and hash state like every other
    /// component; all-zero is idle in every field, <see cref="BlockTiming"/>
    /// included (a zero lowered-age reads as "never raised this stock").
    ///
    /// <para><b>Lifecycle.</b> Added in <c>FighterWorldSystem.SpawnFighter</c>
    /// beside component 322. Held at zero in every field while
    /// <c>FighterMatchFlowRules.IsOnRespawnPlatform</c> is true (the stock-loss
    /// reset: <c>FighterSimulationRules.ApplyStockLoss</c> has no Frame, so the
    /// movement system zeroes it while the platform holds the fighter, the way
    /// <c>AirDodgeUsed</c> is refreshed). The F22 Sudden Death setup reset in
    /// <c>FighterMatchSystem</c> zeroes it. CM-HIT sets
    /// <see cref="ActionFlagRespawnProtection"/> at the platform drop, after the
    /// hold. Read seam: <c>FighterSimulation.TryGetFighterActionState</c>.</para>
    ///
    /// <para><b>Owners.</b> Each field names the workstream that writes it. No
    /// other workstream allocates a component ID, grows 312 or 320, or adds a
    /// field anywhere else; a need for more state uses only the reserved
    /// <see cref="ActionFlags"/> bits 4–31.</para>
    ///
    /// <para><b>The zero-byte reuses reserved beside it (plan §1.5).</b>
    /// Component 305 <c>AttackFlags</c> 256 = <c>FighterBasicAttackRules.FlagActiveFacingRight</c>
    /// (the string box facing latched at active start; CM-SIM) and 512 =
    /// <c>FlagLedgeAttack</c> (CM-MOVE). Component 305's universal-movement slot
    /// values 8, 9 and 10 = <c>LedgeClimb</c>, <c>LedgeRoll</c>,
    /// <c>LedgeAttackClimb</c> (CM-MOVE; 8–15 are free, the air dodge owns 5–7
    /// and the kit phases 16–31). Component 310 <c>Tumble</c> value 2 = tumbling
    /// after hitstun ended (CM-HIT).</para>
    /// </summary>
    [KlothoComponent(323, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterActionStateComponent : IComponent {
        /// <summary>The Special being cast: 0 none, 1 Special 1, 2 Special 2. (CM-SIM)</summary>
        public int SpecialSlot;
        /// <summary>
        /// The cast phase: 0 none, 1 Startup, 2 Active, 3 Recovery, 4 Delegated
        /// (Tubman's Foresight runs its own timeline on component 322; this
        /// records the cast so the cast lock, the CPU and D3 have one owner).
        /// See <c>FighterSpecialCastRules</c>. (CM-SIM)
        /// </summary>
        public int SpecialPhase;
        /// <summary>Frames left in <see cref="SpecialPhase"/>. (CM-SIM)</summary>
        public int SpecialPhaseFrames;
        /// <summary>Frames elapsed in <see cref="SpecialPhase"/>, 0 on its first frame (the recovery frame index). (CM-SIM)</summary>
        public int SpecialPhaseElapsed;
        /// <summary>
        /// Cast flags: 1 StartedAerial, 2 EffectFired, 4 MeleeResolved (CM-SIM),
        /// 8 LandedDirectHit (CM-CANCEL), 16 FallbackTiming (CM-SIM: the
        /// contract carried no authored frames and runs as 0 / 1 / 0). Bits above
        /// 16 are free.
        /// </summary>
        public int SpecialFlags;
        /// <summary>
        /// The D3 cancel buffer (CM-CANCEL, Phase B): bits 0–2 the verb (0 none,
        /// 1 roll, 2 jump, 3 basic, 4 movement), bits 3–6 its age, bit 7 Up held
        /// at the press, bit 8 Down held at the press. Zero in Phase A.
        /// </summary>
        public int SpecialCancelBuffer;
        /// <summary>
        /// The perfect-block clock (CM-BLOCK, Phase B): bits 0–7 perfect-block
        /// frames left; bits 8–15 frames since the stance last dropped (0 = never
        /// raised this stock, otherwise 1..255 saturating); bit 16 the stance was
        /// up last tick; bits 17–18 per-tick bookkeeping and 20–23 the perfect
        /// blocks resolved (CM-BLOCK: see <c>FighterBlockTimingRules</c>).
        /// </summary>
        public int BlockTiming;
        /// <summary>The double-tap Down window, 0..18 (CM-MOVE, Phase B). Zero in Phase A.</summary>
        public int DownTapFrames;
        /// <summary>
        /// Bit 0 <see cref="ActionFlagRespawnProtection"/> (CM-HIT); bit 1
        /// <see cref="ActionFlagJumpReleasedSincePress"/> (CM-MOVE); bit 2
        /// <see cref="ActionFlagLedgeStickRearmed"/> (CM-MOVE); bit 3
        /// <see cref="ActionFlagRiseFromOwnJump"/> (CM-MOVE, the Q5 ruling); bits
        /// 4–31 reserved, 0. Zero in Phase A.
        /// </summary>
        public int ActionFlags;

        /// <summary><see cref="ActionFlags"/> bit 0: Fighter respawn protection is live (CM-HIT, 2.4.2).</summary>
        public const int ActionFlagRespawnProtection = 1;
        /// <summary><see cref="ActionFlags"/> bit 1: Jump was released since its last press (CM-MOVE, the short hop).</summary>
        public const int ActionFlagJumpReleasedSincePress = 2;
        /// <summary><see cref="ActionFlags"/> bit 2: the ledge stick re-armed (CM-MOVE, the get-ups).</summary>
        public const int ActionFlagLedgeStickRearmed = 4;
        /// <summary><see cref="ActionFlags"/> bit 3: the current rise began with this airtime's own Jump press (CM-MOVE, Q5).</summary>
        public const int ActionFlagRiseFromOwnJump = 8;
    }

    /// <summary>
    /// The Fighter Special cast (decision 1.2, plan §2.1.6): the authored
    /// startup, active and recovery of every Special, run on component 323.
    /// Integer frame arithmetic only; nothing here reads Story state.
    ///
    /// <para><b>The cast lock.</b> While <see cref="IsBusy"/> is true the
    /// caster's input is discarded on every tick, its own hitstop ticks
    /// included (<c>FighterMovementSystem</c>: the hitstop branch and the cast
    /// region before <c>TickCounters</c>). The only exit is the grounded recovery
    /// Block cancel (and, from Phase B, CM-CANCEL's D3). The second gates are
    /// <c>FighterGrabRules.CanStartGrab</c>,
    /// <c>FighterAbilityLockoutRules.CanReadAbilityPress</c> and the Ultimate
    /// predicate (<c>FighterCombatSystem.UltimatePressPayable</c>).</para>
    ///
    /// <para><b>Interrupts.</b> Hitstun, daze, a grab on either side, an
    /// activation-strike capture, a knockdown or its get-up (fix round A), a
    /// stock loss or the respawn platform end the cast
    /// (<see cref="ClearIfInterrupted"/>): the cooldown and lockout stay spent,
    /// what already spawned survives, an unfired effect never fires.</para>
    /// </summary>
    public static class FighterSpecialCastRules {
        public const int PhaseNone = 0;
        public const int PhaseStartup = 1;
        public const int PhaseActive = 2;
        public const int PhaseRecovery = 3;
        /// <summary>Tubman's Foresight: the stance runs on component 322; 323 records the cast.</summary>
        public const int PhaseDelegated = 4;

        /// <summary><see cref="FighterActionStateComponent.SpecialFlags"/>: the cast began airborne (half gravity through startup and active).</summary>
        public const int FlagStartedAerial = 1;
        /// <summary>The slot's effect (projectile, construct, zone, lunge) has fired.</summary>
        public const int FlagEffectFired = 2;
        /// <summary>A melee slot resolved its one contact this execution.</summary>
        public const int FlagMeleeResolved = 4;
        /// <summary>CM-CANCEL (Phase B): the cast landed a direct hit (arms D3).</summary>
        public const int FlagLandedDirectHit = 8;
        /// <summary>The contract carried no authored frames: the cast runs as 0 / 1 / 0, today's press-tick timing.</summary>
        public const int FlagFallbackTiming = 16;

        /// <summary>Any cast phase, Delegated included: the caster is action-locked.</summary>
        public static bool IsBusy(in FighterActionStateComponent cast) => cast.SpecialPhase != PhaseNone;

        /// <summary>A clocked cast (Startup, Active or Recovery) — not Delegated.</summary>
        public static bool IsCasting(in FighterActionStateComponent cast) =>
            cast.SpecialPhase is PhaseStartup or PhaseActive or PhaseRecovery;

        /// <summary>Joan's Divine Piercing: Special 2 authored with the Melee execution (the lunge).</summary>
        public static bool IsPiercingLunge(
            in FighterStateComponent fighter, in FighterAbilityModeComponent modes, int slot) =>
            fighter.CharacterID == (int)FighterCharacterID.Joan
            && slot == 2
            && modes.SpecialTwoExecutionType == MeleeExecutionType;

        /// <summary>Tubman's Foresight: Special 2 runs as the component-322 stance (Delegated).</summary>
        public static bool IsForesight(in FighterStateComponent fighter, int slot) =>
            fighter.CharacterID == (int)FighterCharacterID.Tubman && slot == 2;

        /// <summary><c>AbilityExecutionType.Melee</c> as the loadout's int.</summary>
        public const int MeleeExecutionType = 0;

        public static bool IsStartup(in FighterActionStateComponent cast) => cast.SpecialPhase == PhaseStartup;
        public static bool IsActive(in FighterActionStateComponent cast) => cast.SpecialPhase == PhaseActive;
        public static bool IsRecovery(in FighterActionStateComponent cast) => cast.SpecialPhase == PhaseRecovery;
        public static bool IsDelegated(in FighterActionStateComponent cast) => cast.SpecialPhase == PhaseDelegated;

        /// <summary>The 0-based recovery frame (the tick's own frame), or -1 outside recovery.</summary>
        public static int RecoveryFrame(in FighterActionStateComponent cast) =>
            cast.SpecialPhase == PhaseRecovery ? cast.SpecialPhaseElapsed : -1;

        public static bool HasFlag(in FighterActionStateComponent cast, int flag) => (cast.SpecialFlags & flag) != 0;

        /// <summary>
        /// The cast's startup / active / recovery frames from the slot's
        /// projected contract. A contract with no authored frames
        /// (<c>ActiveFrames == 0</c>: a hand-built <c>FighterAbilityLoadout.Default</c>)
        /// runs as 0 / 1 / 0, which reproduces the press-tick timing the hand-built
        /// harnesses were written against (ledger <c>PROVISIONAL-SIM-SPECIAL-PHASE-RULES</c>).
        /// </summary>
        public static void Frames(
            in FighterAbilityHitData contract, out int startup, out int active, out int recovery) {
            if (IsFallback(in contract)) {
                startup = 0;
                active = 1;
                recovery = 0;
                return;
            }
            startup = contract.StartupFrames > 0 ? contract.StartupFrames : 0;
            active = contract.ActiveFrames > 0 ? contract.ActiveFrames : 1;
            recovery = contract.RecoveryFrames > 0 ? contract.RecoveryFrames : 0;
        }

        /// <summary>True for a contract with no authored frames (the 0 / 1 / 0 fallback).</summary>
        public static bool IsFallback(in FighterAbilityHitData contract) => contract.ActiveFrames <= 0;

        /// <summary>
        /// The accepted press (tick T): Startup with S frames, or Active on T
        /// itself when S is 0 (the fire pass then fires on T). The cost — the
        /// cooldown, the ability lockout, the Momentum reset — is spent by the
        /// caller at acceptance, never here.
        /// </summary>
        public static void Begin(
            ref FighterActionStateComponent cast, int slot, in FighterAbilityHitData contract, bool startedAerial) {
            Frames(in contract, out int startup, out int active, out _);
            cast.SpecialSlot = slot;
            cast.SpecialPhaseElapsed = 0;
            cast.SpecialFlags = (startedAerial ? FlagStartedAerial : 0)
                | (IsFallback(in contract) ? FlagFallbackTiming : 0);
            if (startup > 0) {
                cast.SpecialPhase = PhaseStartup;
                cast.SpecialPhaseFrames = startup;
            } else {
                cast.SpecialPhase = PhaseActive;
                cast.SpecialPhaseFrames = active;
            }
        }

        /// <summary>
        /// Tubman's Foresight: the stance runs on component 322; this records the
        /// cast. Fix round A (CM-FIXA-SIM): an airborne Foresight records
        /// <see cref="FlagStartedAerial"/> like any other Special, so it falls at
        /// the aerial-Special half gravity through its startup and window.
        /// </summary>
        public static void BeginDelegated(ref FighterActionStateComponent cast, int slot, bool startedAerial) {
            cast.SpecialSlot = slot;
            cast.SpecialPhase = PhaseDelegated;
            cast.SpecialPhaseFrames = 0;
            cast.SpecialPhaseElapsed = 0;
            cast.SpecialFlags = startedAerial ? FlagStartedAerial : 0;
        }

        /// <summary>
        /// One unfrozen tick of the cast clock (<c>FighterMovementSystem</c>,
        /// after the hitstop gate, so a cast pauses in its own hitstop exactly as
        /// Story's does). Startup → Active → Recovery → none; a zero-frame
        /// recovery ends the cast as the active window ends. Returns true on the
        /// tick the cast enters Active (T+S). A Delegated cast is not clocked here.
        /// </summary>
        public static bool Advance(ref FighterActionStateComponent cast, in FighterAbilityHitData contract) {
            if (cast.SpecialPhase is PhaseNone or PhaseDelegated) return false;
            cast.SpecialPhaseElapsed++;
            cast.SpecialPhaseFrames--;
            if (cast.SpecialPhaseFrames > 0) return false;
            Frames(in contract, out _, out int active, out int recovery);
            switch (cast.SpecialPhase) {
                case PhaseStartup:
                    cast.SpecialPhase = PhaseActive;
                    cast.SpecialPhaseFrames = active;
                    cast.SpecialPhaseElapsed = 0;
                    return true;
                case PhaseActive:
                    if (recovery > 0) {
                        cast.SpecialPhase = PhaseRecovery;
                        cast.SpecialPhaseFrames = recovery;
                        cast.SpecialPhaseElapsed = 0;
                    } else {
                        Clear(ref cast);
                    }
                    return false;
                default:
                    Clear(ref cast);
                    return false;
            }
        }

        /// <summary>Ends the cast: the slot, the phase, its clock and the cast flags. The other 323 fields are left alone.</summary>
        public static void Clear(ref FighterActionStateComponent cast) {
            cast.SpecialSlot = 0;
            cast.SpecialPhase = PhaseNone;
            cast.SpecialPhaseFrames = 0;
            cast.SpecialPhaseElapsed = 0;
            cast.SpecialFlags = 0;
        }

        /// <summary>
        /// The interrupt set: hitstun, daze, a grab on either side, a capture,
        /// out of stocks, the respawn platform — and (fix round A, CM-FIXA-SIM)
        /// the M05 knockdown or its get-up (<paramref name="knockedDown"/>,
        /// <c>FighterKnockdownRules.IsActive</c> on component 320): a downed
        /// fighter takes no action, so a cast never runs through a knockdown and
        /// its effect never fires from the floor.
        /// </summary>
        public static bool IsInterrupted(
            in FighterStateComponent fighter, in FighterVerbComponent verb, bool captured, bool knockedDown) =>
            fighter.HitstunFrames > 0
            || fighter.DazeFrames > 0
            || FighterGrabRules.IsBusy(in verb)
            || captured
            || knockedDown
            || fighter.Stocks <= 0
            || FighterMatchFlowRules.IsOnRespawnPlatform(in fighter);

        /// <summary>Clears a busy cast the interrupt set ends. Returns true when it did.</summary>
        public static bool ClearIfInterrupted(
            ref FighterActionStateComponent cast,
            in FighterStateComponent fighter,
            in FighterVerbComponent verb,
            bool captured,
            bool knockedDown) {
            if (!IsBusy(in cast) || !IsInterrupted(in fighter, in verb, captured, knockedDown)) return false;
            Clear(ref cast);
            return true;
        }
    }
}
