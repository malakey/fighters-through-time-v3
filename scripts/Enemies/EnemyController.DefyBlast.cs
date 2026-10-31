using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// The Defy History blast's mob half (meter and Defy update 2026-10-31, plan
    /// sections 2.5, P7, P8, P9, P22). Story only; nothing under <c>scripts/FighterSim/</c>
    /// reads it.
    /// </summary>
    public partial class EnemyController {

        /// <summary>
        /// A Defy blast on this mob: a fixed non-launching stagger with an assigned push and a
        /// short victim hitstop. It is not a hit: no damage, meter, Rally echo, landed signal or
        /// Momentum, and it ignores the V7.4 getup and Armored Recovery windows (P22), charges no
        /// stagger budget or standard poise, and takes no D4 chain step. Dormant lock members and
        /// <see cref="DrillInvulnerable"/> bodies are skipped. Returns whether the mob was staggered.
        /// </summary>
        public bool ApplyDefyBlast(Vector2 push, int staggerFrames, int hitstopFrames) {
            if (CurrentState == EnemyState.Dead || DrillInvulnerable || IsEncounterDormant) return false;
            if (staggerFrames <= 0) return false;

            // The armour windows end: the blast goes through them (P22), and the stun that
            // follows ends in an ordinary natural expiry that arms a fresh getup window.
            _getupArmorFramesRemaining = 0;
            _armoredRecoveryTimer = 0f;
            RefreshArmorPresentation();

            Executor.InterruptSiphonTether();
            Executor.Cancel();
            _attackCommitted = false;
            _secondaryBlinkInFlight = false;
            _reactionFramesRemaining = 0;
            _armoredAnswerPending = false;
            // A new launch state replaces any finisher flight and bounce latch.
            ResetLaunchCarry();
            _stunTimer = staggerFrames / 60f;
            CurrentState = EnemyState.Stunned;
            // Assigned, not weight-scaled (P7). The stunned friction and the drop-edge rule (S1)
            // then act on it as on any knockback.
            Velocity = push;
            PlayAnimation("hitstun");
            ApplyHitstop(hitstopFrames);
            return true;
        }
    }
}
