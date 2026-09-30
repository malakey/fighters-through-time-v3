using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// A02 (Package 13 W6; Klotho ID 321, allocated by the Package 13 plan D7):
    /// the Fighter-mode Ultimate activation strike and the cinematic it opens.
    /// One per fighter. The same component plays both roles:
    ///
    /// <para><b>Caster side</b> — <see cref="Phase"/> walks
    /// Windup → Active → (WhiffRecovery | Cinematic) → None, counted by
    /// <see cref="PhaseFrames"/>. The meter is spent on acceptance, before the
    /// wind-up, and a whiff never refunds it.</para>
    ///
    /// <para><b>Victim side</b> — while <see cref="CapturedFrames"/> is above
    /// zero the fighter is held at <see cref="CaptureAnchor"/> by the player in
    /// <see cref="CaptorSlot"/>: no input, no movement, so the cinematic lands
    /// at its full authored damage. That is the sim's reading of "standard
    /// gameplay pauses and the cinematic plays" for the two fighters; the world
    /// (projectiles, hazards, the match clock) keeps running.</para>
    ///
    /// <para>6 ints + 1 <see cref="FP64"/> + 2 <see cref="FPVector2"/> = 64 bytes. Snapshot and hash
    /// state like every other component. All-zero is fully inactive: no
    /// activation, not captured (<see cref="CaptorSlot"/> is PlayerID + 1, so 0
    /// means "nobody"). Fields are written only through
    /// <see cref="FighterUltimateActivationRules"/>.</para>
    /// </summary>
    [KlothoComponent(321, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterUltimateActivationComponent : IComponent {
        /// <summary>Caster phase: see the <c>Phase*</c> constants on <see cref="FighterUltimateActivationRules"/>.</summary>
        public int Phase;
        /// <summary>Frames left in <see cref="Phase"/>.</summary>
        public int PhaseFrames;
        /// <summary>1 when the Ultimate was accepted airborne: gravity is 0 through the wind-up and active frames.</summary>
        public int StartedAerial;
        /// <summary>Facing committed at acceptance; the strike travels this way.</summary>
        public int FacingRight;
        /// <summary>Victim side: the captor's PlayerID + 1, or 0 when not captured.</summary>
        public int CaptorSlot;
        /// <summary>Victim side: frames left in the hold; &gt; 0 means captured.</summary>
        public int CapturedFrames;
        /// <summary>
        /// Caster side: a melee activation's lunge speed (units/s, signed by
        /// facing) through the active frames; zero for a projectile or wave.
        /// Stored at acceptance so the movement system needs no loadout.
        /// </summary>
        public FP64 LungeVelocityX;
        /// <summary>Caster side: the aerial anchor during the wind-up, then the strike's origin from the first active frame.</summary>
        public FPVector2 Origin;
        /// <summary>Victim side: where the victim is held for the cinematic (the contact point).</summary>
        public FPVector2 CaptureAnchor;
    }

    /// <summary>
    /// A02/D15 (Package 13 W6): one fighter's Ultimate sequence and activation
    /// strike, projected from the normalized <c>AbilityData</c> by
    /// <c>FighterLoadoutFactory</c>. LOADOUT data — never written into a
    /// component, so it moves no hash — handed to the systems through
    /// <see cref="FighterHitContractTable.UltimateFor"/>. It replaces the
    /// per-character timing constants <see cref="FighterUltimateRules"/> used
    /// to restate from the <c>.tres</c> files.
    /// </summary>
    public readonly struct FighterUltimateData {
        /// <summary>Regular hits before the finale (<c>AbilityData.HitCount</c>).</summary>
        public int HitCount { get; init; }
        /// <summary>Frames between hits (<c>AbilityData.DamageTickIntervalFrames</c>).</summary>
        public int TickIntervalFrames { get; init; }
        /// <summary>D15 finale damage, 0 = none.</summary>
        public int FinaleDamage { get; init; }
        /// <summary>D15: the finale is a launcher.</summary>
        public bool FinaleLaunches { get; init; }
        /// <summary><see cref="FTT.Combat.UltimateActivationShape"/> ordinal.</summary>
        public int ActivationShape { get; init; }
        /// <summary>Activation reach in world units.</summary>
        public FP64 ActivationRange { get; init; }
        /// <summary>Activation box half-extents in world units.</summary>
        public FPVector2 ActivationHalfExtents { get; init; }
        public int WindupFrames { get; init; }
        public int ActiveFrames { get; init; }
        public int WhiffRecoveryFrames { get; init; }

        public bool HasFinale => FinaleDamage > 0;

        /// <summary>The zero struct: a hand-built loadout that never projected an Ultimate.</summary>
        public bool IsUnset => WindupFrames <= 0 && ActiveFrames <= 0 && HitCount <= 0;

        /// <summary>Zone lifetime of the character cinematic: the regular hits only.</summary>
        public int SequenceFrames => System.Math.Max(1, HitCount) * System.Math.Max(1, TickIntervalFrames);

        /// <summary>Frames the victim is held after contact; see <see cref="FTT.Combat.UltimateActivationRules.CinematicHoldFrames"/>.</summary>
        public int CinematicFrames =>
            FTT.Combat.UltimateActivationRules.CinematicHoldFrames(HitCount, TickIntervalFrames, HasFinale);

        /// <summary>
        /// The generic sequence a hand-built test loadout gets: 5 × 18, no
        /// finale, a 6-unit straight shot at the A02 provisional 20/10/45. It is
        /// not a copy of any character's numbers — every shipped Ultimate is
        /// projected from its own resource.
        /// </summary>
        public static FighterUltimateData Default => new() {
            HitCount = 5,
            TickIntervalFrames = 18,
            FinaleDamage = 0,
            FinaleLaunches = false,
            ActivationShape = (int)FTT.Combat.UltimateActivationShape.Projectile,
            ActivationRange = FP64.FromInt(6),
            ActivationHalfExtents = new FPVector2(FP64.FromDouble(0.4), FP64.FromDouble(0.4)),
            WindupFrames = FTT.Combat.UltimateActivationRules.DefaultWindupFrames,
            ActiveFrames = FTT.Combat.UltimateActivationRules.DefaultActiveFrames,
            WhiffRecoveryFrames = FTT.Combat.UltimateActivationRules.DefaultWhiffRecoveryFrames
        };
    }

    /// <summary>
    /// A02 (Package 13 W6): the activation strike's state machine. Called from
    /// <see cref="FighterCombatSystem"/> (acceptance, the phase clock, contact,
    /// the cinematic and its finale) and <see cref="FighterMovementSystem"/>
    /// (the caster's input lock and motion, the victim's hold). Fixed-point
    /// only; nothing here reads Story state.
    /// </summary>
    public static class FighterUltimateActivationRules {
        public const int PhaseNone = 0;
        public const int PhaseWindup = 1;
        public const int PhaseActive = 2;
        public const int PhaseWhiffRecovery = 3;
        public const int PhaseCinematic = 4;

        private static readonly FPVector2 VictimHalfExtents = new(FP64.FromDouble(0.5), FP64.One);
        /// <summary>A ground wave reaches targets standing within this height of its own floor line.</summary>
        private static readonly FP64 GroundWaveFloorTolerance = FP64.FromDouble(0.5);
        private static readonly FP64 FramesPerSecond = FP64.FromInt(FighterSimulation.TickRate);

        /// <summary>The caster owns the Ultimate (any phase): inputs are discarded.</summary>
        public static bool IsCasterBusy(in FighterUltimateActivationComponent activation) =>
            activation.Phase != PhaseNone;

        /// <summary>The fighter is held by an opponent's cinematic.</summary>
        public static bool IsCaptured(in FighterUltimateActivationComponent activation) =>
            activation.CapturedFrames > 0;

        /// <summary>Hyper-armor window: wind-up and active frames.</summary>
        public static bool IsArmored(in FighterUltimateActivationComponent activation) =>
            activation.Phase == PhaseWindup || activation.Phase == PhaseActive;

        // --- Acceptance ---------------------------------------------------------

        /// <summary>
        /// Accepts an Ultimate: the meter is spent NOW (D03h is unchanged — a
        /// whiff keeps it spent), the swing and any universal movement are
        /// cancelled, and the wind-up starts with hyper-armor through wind-up +
        /// active. Airborne casters are frozen in place through those frames.
        /// </summary>
        internal static void Accept(
            ref FighterStateComponent caster,
            ref FighterRuntimeComponent casterRuntime,
            ref FighterUltimateActivationComponent activation,
            in FighterUltimateData data) {
            caster.Influence = FP64.Zero;
            FighterUniversalMovementRules.Cancel(ref casterRuntime);
            FighterBasicAttackRules.CancelString(ref casterRuntime);
            activation.Phase = PhaseWindup;
            activation.PhaseFrames = System.Math.Max(1, data.WindupFrames);
            activation.StartedAerial = caster.IsGrounded == 0 ? 1 : 0;
            activation.FacingRight = caster.FacingRight != 0 ? 1 : 0;
            activation.Origin = caster.Position;
            FP64 lunge = FP64.Zero;
            if (data.ActivationShape == (int)FTT.Combat.UltimateActivationShape.Melee) {
                lunge = data.ActivationRange * FramesPerSecond / FP64.FromInt(System.Math.Max(1, data.ActiveFrames));
                if (activation.FacingRight == 0) lunge = -lunge;
            }
            activation.LungeVelocityX = lunge;
            int armor = System.Math.Max(1, data.WindupFrames) + System.Math.Max(1, data.ActiveFrames);
            if (caster.HyperArmorFrames < armor) caster.HyperArmorFrames = armor;
        }

        /// <summary>Ends a caster's activation with nothing refunded.</summary>
        internal static void ClearCaster(ref FighterStateComponent caster, ref FighterUltimateActivationComponent activation) {
            if (IsArmored(in activation)) caster.HyperArmorFrames = 0;
            activation.Phase = PhaseNone;
            activation.PhaseFrames = 0;
            activation.StartedAerial = 0;
            activation.LungeVelocityX = FP64.Zero;
            activation.Origin = FPVector2.Zero;
        }

        internal static void ReleaseCapture(ref FighterUltimateActivationComponent activation) {
            activation.CaptorSlot = 0;
            activation.CapturedFrames = 0;
            activation.CaptureAnchor = FPVector2.Zero;
        }

        // --- Movement-system hooks ----------------------------------------------

        /// <summary>
        /// The caster is action-locked for the whole Ultimate: wind-up, active,
        /// whiff recovery and the cinematic. Discarding the tick's buttons before
        /// any verb reads them is what makes the whiff recovery impossible to
        /// undo with Echo Step (or to block, roll, jump or grab out of).
        /// </summary>
        internal static void DiscardInput(ref FighterRuntimeComponent runtime) {
            runtime.PressedButtons = 0;
            runtime.HeldButtons = 0;
            runtime.ReleasedButtons = 0;
            runtime.MoveX = 0;
            runtime.MoveY = 0;
        }

        /// <summary>
        /// Overrides the caster's velocity just before integration. Wind-up and
        /// active frames plant the caster (an airborne start freezes in the air,
        /// gravity 0); a melee activation lunges its full range over the active
        /// frames. Whiff recovery and the cinematic run ordinary physics.
        /// </summary>
        internal static void ApplyCasterMotion(
            ref FighterStateComponent caster,
            in FighterUltimateActivationComponent activation) {
            if (!IsArmored(in activation) || caster.HitstunFrames > 0) return;
            if (activation.StartedAerial != 0) caster.Velocity.y = FP64.Zero;
            caster.Velocity.x = activation.Phase == PhaseActive ? activation.LungeVelocityX : FP64.Zero;
        }

        /// <summary>Holds a captured victim at the contact point with no input.</summary>
        internal static void HoldCaptured(
            ref FighterStateComponent victim,
            ref FighterRuntimeComponent victimRuntime,
            in FighterUltimateActivationComponent activation) {
            DiscardInput(ref victimRuntime);
            FighterUniversalMovementRules.Cancel(ref victimRuntime);
            victim.Position = activation.CaptureAnchor;
            victim.Velocity = FPVector2.Zero;
        }

        // --- Contact geometry ---------------------------------------------------

        /// <summary>
        /// The world box the strike occupies on active frame
        /// <paramref name="activeIndex"/> (0-based). A melee box rides in front
        /// of the caster; a projectile or ground wave sweeps the segment its
        /// front travelled this frame, so a fast strike cannot tunnel past a
        /// target between frames.
        /// </summary>
        public static void ResolveStrikeBox(
            in FighterStateComponent caster,
            in FighterUltimateActivationComponent activation,
            in FighterUltimateData data,
            int activeIndex,
            out FPVector2 center,
            out FPVector2 halfExtents) {
            FP64 facing = activation.FacingRight != 0 ? FP64.One : -FP64.One;
            if (data.ActivationShape == (int)FTT.Combat.UltimateActivationShape.Melee) {
                center = new FPVector2(
                    caster.Position.x + facing * data.ActivationHalfExtents.x,
                    caster.Position.y);
                halfExtents = data.ActivationHalfExtents;
                return;
            }
            int active = System.Math.Max(1, data.ActiveFrames);
            int index = activeIndex < 0 ? 0 : activeIndex >= active ? active - 1 : activeIndex;
            FP64 previousReach = data.ActivationRange * FP64.FromInt(index) / FP64.FromInt(active);
            FP64 reach = data.ActivationRange * FP64.FromInt(index + 1) / FP64.FromInt(active);
            FP64 two = FP64.FromInt(2);
            center = new FPVector2(
                activation.Origin.x + facing * (previousReach + reach) / two,
                activation.Origin.y);
            halfExtents = new FPVector2(
                (reach - previousReach) / two + data.ActivationHalfExtents.x,
                data.ActivationHalfExtents.y);
        }

        /// <summary>
        /// A02 avoidance: invulnerability of any kind (roll, air dodge, tech,
        /// knockdown, respawn), the D04 Defy protected window, being out of
        /// stocks or already captured. Temporal Aegis and an HP barrier are NOT
        /// avoidance — an absorbed contact still starts the cinematic, and the
        /// cinematic's hits then resolve against them per D03b.
        /// </summary>
        public static bool IsAvoided(
            in FighterStateComponent target,
            in FighterDefenseComponent targetDefense,
            in FighterUltimateActivationComponent targetActivation) =>
            target.Stocks <= 0
            || target.RespawnFramesRemaining > 0
            || target.InvulnerabilityFrames > 0
            || FighterDefenseRules.IsDefyProtected(in targetDefense)
            || IsCaptured(in targetActivation);

        /// <summary>Whether the strike on active frame <paramref name="activeIndex"/> connects.</summary>
        public static bool StrikeConnects(
            in FighterStateComponent caster,
            in FighterUltimateActivationComponent activation,
            in FighterUltimateData data,
            int activeIndex,
            in FighterStateComponent target,
            in FighterDefenseComponent targetDefense,
            in FighterUltimateActivationComponent targetActivation) {
            if (IsAvoided(in target, in targetDefense, in targetActivation)) return false;
            ResolveStrikeBox(in caster, in activation, in data, activeIndex, out FPVector2 center, out FPVector2 half);
            if (data.ActivationShape == (int)FTT.Combat.UltimateActivationShape.GroundWave) {
                // LN05: a ground wave races along the floor — grounded targets
                // only, so any jump avoids it.
                if (target.IsGrounded == 0) return false;
                // A grounded caster's wave stays on its own floor tier; an
                // airborne slam sends it along whatever floor the target stands on.
                if (activation.StartedAerial == 0
                    && FP64.Abs(target.Position.y - activation.Origin.y) > GroundWaveFloorTolerance) return false;
                return FP64.Abs(center.x - target.Position.x) <= half.x + VictimHalfExtents.x;
            }
            return FighterEntityQueries.Overlaps(in center, in half, in target.Position, in VictimHalfExtents);
        }
    }
}
