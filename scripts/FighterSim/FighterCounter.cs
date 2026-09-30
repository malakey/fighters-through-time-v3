using System.Runtime.InteropServices;
using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Package 13 W5 (Klotho ID 322, allocated by the Package 13 plan D7):
    /// Harriet Tubman's Foresight counter stance. One per fighter; all-zero is
    /// fully inactive, so every other character carries it idle.
    ///
    /// <para><see cref="Phase"/> walks Startup (4) → Window (20) →
    /// WhiffRecovery (24) → None, or Window → Sidestep (12) → None when a
    /// strike or projectile is caught. <see cref="PhaseFrames"/> counts the
    /// current phase down; the clock pauses in hitstop like every other
    /// per-fighter timer (it advances in <c>FighterMovementSystem</c> after the
    /// hitstop gate).</para>
    ///
    /// <para><see cref="AnswerPending"/> is set on the trigger tick and consumed
    /// by <c>FighterCombatSystem</c>, which lands the counter strike on the
    /// opponent if they are still within
    /// <see cref="TubmanKitRules.ForesightAnswerRangeUnits"/>.
    /// <see cref="CountersLanded"/> counts triggers for presentation and tests.</para>
    ///
    /// <para>4 ints = <b>16 bytes</b>. Snapshot and hash state like every other
    /// component. Fields are written only through
    /// <see cref="FighterForesightRules"/>.</para>
    /// </summary>
    [KlothoComponent(322, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterCounterComponent : IComponent {
        /// <summary>See the <c>Phase*</c> constants on <see cref="FighterForesightRules"/>.</summary>
        public int Phase;
        /// <summary>Frames left in <see cref="Phase"/>.</summary>
        public int PhaseFrames;
        /// <summary>1 when a trigger owes the counter strike (resolved by the combat system).</summary>
        public int AnswerPending;
        /// <summary>Triggers this match (never reset by a stock loss); presentation and tests only.</summary>
        public int CountersLanded;
    }

    /// <summary>
    /// Package 13 W5 — Foresight (design §5, ABILITY_DATA): "After a 4-frame
    /// startup she holds a 20-frame window: if a strike or projectile would hit
    /// her from the front or rear, that hit is nullified (no damage, hitstun or
    /// block cost) and she sidesteps; if the attacker is within 2.5 units she
    /// answers with a lantern strike for 20 damage that launches. One trigger
    /// per activation. Grabs and Ultimates beat it; windboxes, construct and
    /// zone ticks and hazards neither trigger it nor are stopped by it, and a
    /// counter strike cannot trigger another Foresight. A whiffed stance has 24
    /// frames of recovery."
    ///
    /// <para><b>What is a "strike or projectile".</b> The hit paths that call
    /// <see cref="TryCounter"/>: a basic string swing, a directional swing
    /// (Up-Attack, Down-Air), a melee Special intent, and a fighter-fired
    /// projectile (Special or Ultimate origin). Every other path — throws and
    /// grabs, Ultimate activation strikes and cinematic zones, zone and
    /// construct ticks, hazards, the Lorentz chain arcs, the Ultimate finale
    /// and the counter strike itself — never asks, so none of them can
    /// trigger it. The stance is omnidirectional ("front or rear").</para>
    ///
    /// <para><b>The sidestep</b> is <see cref="TubmanKitRules.ForesightSidestepFrames"/>
    /// of invulnerability and action lock with no displacement (PROVISIONAL —
    /// the design names a sidestep but gives no distance).</para>
    ///
    /// <para>Fixed-point only; nothing here reads Story state (the Story-only
    /// Minor Foresight Window never reaches the sim).</para>
    /// </summary>
    public static class FighterForesightRules {
        public const int PhaseNone = 0;
        public const int PhaseStartup = 1;
        public const int PhaseWindow = 2;
        public const int PhaseWhiffRecovery = 3;
        public const int PhaseSidestep = 4;

        /// <summary>The answer reach, squared (units²).</summary>
        private static readonly FP64 AnswerRangeSquared = FP64.FromDouble(
            (double)TubmanKitRules.ForesightAnswerRangeUnits * TubmanKitRules.ForesightAnswerRangeUnits);

        /// <summary>Any phase: the fighter is action-locked (input discarded).</summary>
        public static bool IsBusy(in FighterCounterComponent counter) => counter.Phase != PhaseNone;

        public static bool IsWindowOpen(in FighterCounterComponent counter) => counter.Phase == PhaseWindow;

        /// <summary>Accepted Special 2 press: the 4-frame startup begins.</summary>
        public static void Begin(ref FighterCounterComponent counter) {
            counter.Phase = PhaseStartup;
            counter.PhaseFrames = TubmanKitRules.ForesightStartupFrames;
            counter.AnswerPending = 0;
        }

        /// <summary>Drops the stance (hit, grab, capture, stock loss). A pending answer is dropped too.</summary>
        public static void Clear(ref FighterCounterComponent counter) {
            counter.Phase = PhaseNone;
            counter.PhaseFrames = 0;
            counter.AnswerPending = 0;
        }

        /// <summary>
        /// One tick of the stance clock, called by <c>FighterMovementSystem</c>
        /// after the hitstop gate. A hit that lands in the startup or the whiff
        /// recovery (hitstun or daze), a grab, an Ultimate capture or a stock
        /// loss ends the stance at once; the sidestep is invulnerable, so only a
        /// non-hit event can end it early.
        /// </summary>
        public static void Advance(
            ref FighterCounterComponent counter,
            in FighterStateComponent fighter,
            in FighterVerbComponent verb,
            bool captured) {
            if (counter.Phase == PhaseNone) return;
            if (fighter.Stocks <= 0
                || fighter.RespawnFramesRemaining > 0
                || fighter.HitstunFrames > 0
                || fighter.DazeFrames > 0
                || FighterGrabRules.IsBusy(in verb)
                || captured) {
                // A pending answer survives only a sidestep that ends normally.
                Clear(ref counter);
                return;
            }
            counter.PhaseFrames--;
            if (counter.PhaseFrames > 0) return;
            switch (counter.Phase) {
                case PhaseStartup:
                    counter.Phase = PhaseWindow;
                    counter.PhaseFrames = TubmanKitRules.ForesightWindowFrames;
                    return;
                case PhaseWindow:
                    counter.Phase = PhaseWhiffRecovery;
                    counter.PhaseFrames = TubmanKitRules.ForesightWhiffRecoveryFrames;
                    return;
                default:
                    counter.Phase = PhaseNone;
                    counter.PhaseFrames = 0;
                    return;
            }
        }

        /// <summary>
        /// The one trigger test, called by each eligible hit path BEFORE it
        /// applies the hit. Returns true — and the caller must then apply
        /// nothing (no damage, hitstun, block cost, status, meter or Rally) —
        /// when the defender's window is open. The trigger closes the window
        /// (one per activation), arms the sidestep and owes the answer.
        /// </summary>
        public static bool TryCounter(ref FighterCounterComponent counter, ref FighterStateComponent defender) {
            // A hit that could not land anyway (the defender is already
            // invulnerable) is not a read: it neither triggers nor spends the window.
            if (counter.Phase != PhaseWindow || defender.Stocks <= 0 || defender.InvulnerabilityFrames > 0) return false;
            counter.Phase = PhaseSidestep;
            counter.PhaseFrames = TubmanKitRules.ForesightSidestepFrames;
            counter.AnswerPending = 1;
            counter.CountersLanded++;
            if (defender.InvulnerabilityFrames < TubmanKitRules.ForesightSidestepFrames) {
                defender.InvulnerabilityFrames = TubmanKitRules.ForesightSidestepFrames;
            }
            return true;
        }

        /// <summary>True when <paramref name="attacker"/> is within the answer's 2.5-unit reach of <paramref name="defender"/>.</summary>
        public static bool AnswerReaches(in FighterStateComponent defender, in FighterStateComponent attacker) {
            FP64 dx = attacker.Position.x - defender.Position.x;
            FP64 dy = attacker.Position.y - defender.Position.y;
            return dx * dx + dy * dy <= AnswerRangeSquared;
        }
    }

    /// <summary>
    /// Package 13 W5 — Conductor's Call (Special 1): "a line of spectral Union
    /// scouts rushes 5 units forward along the ground, dealing 18 damage with a
    /// push to grounded targets". In the sim it is the ordinary Special 1
    /// projectile (the loadout's speed and lifetime: 10 units/s for 0.4 s from
    /// one unit ahead = 5 units of reach) with one extra rule, owned here: it
    /// contacts <b>grounded</b> targets only, and passing over an airborne one
    /// does not consume it. A local stand-in for the ground-wave primitive W7b
    /// owns; recorded in the handoff.
    /// </summary>
    public static class FighterConductorsCallRules {
        /// <summary><c>CharacterID * 10 + slot</c> for Tubman's Special 1 projectile.</summary>
        public const int ProjectileTypeID = (int)FighterCharacterID.Tubman * 10 + 1;

        public static bool IsConductorsCall(int projectileTypeID) => projectileTypeID == ProjectileTypeID;

        /// <summary>False only for the call passing over an airborne target.</summary>
        public static bool CanStrike(int projectileTypeID, in FighterStateComponent target) =>
            !IsConductorsCall(projectileTypeID) || target.IsGrounded != 0;
    }
}
