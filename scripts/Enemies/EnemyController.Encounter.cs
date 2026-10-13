using Godot;
using System;
using FTT.Environment;

namespace FTT.Enemies {

    /// <summary>
    /// Gameplay update 2026-10-12 (GU-E, user request 3, plan §2.3): what a mob
    /// needs to be an <see cref="EncounterLock"/> member. Story only; nothing in
    /// <c>scripts/FighterSim/</c> reads any of it.
    ///
    /// <list type="bullet">
    /// <item><b>Dormancy</b> (<see cref="IsEncounterDormant"/>): no aggro, chase
    ///       or attack, idle facing the lock's entry — and every player hit is
    ///       refused exactly like the boss arena ward (no damage, knockback,
    ///       status, mark, hitstop, meter or Rally; a cold flash and a grey "0"),
    ///       so a future area can never be emptied from outside or through a
    ///       barrier.</item>
    /// <item><b>Bounds</b> (<see cref="EncounterBounds"/>): an x-clamp into the
    ///       lock's area after every move, in every state, dormant included —
    ///       the only answer for a wall-phasing Rift Phantom — and a fall net:
    ///       feet more than <see cref="EncounterLockRules.FallNetMarginPixels"/>
    ///       below the area's bottom put the body back on its authored spawn
    ///       through the relocation rule, velocity zeroed.</item>
    /// <item><b>Identity</b>: a per-instance <see cref="Died"/> event and a
    ///       <see cref="SpawnGeneration"/> that every pool spawn increments, so a
    ///       lock holding a recycled body counts it as removed.</item>
    /// </list>
    ///
    /// <para>Membership is registered at the spawn call by the lock
    /// (<see cref="JoinEncounter"/>), never inferred from the node's parent, and
    /// it is all reset on both halves of a pool cycle. A non-preserve rewind
    /// keeps it (the lock owns it) and only re-applies the clamp and the facing.</para>
    /// </summary>
    public partial class EnemyController {

        /// <summary>The refused hit's cold flash (the boss arena ward's colour and length).</summary>
        private static readonly Color EncounterWardFlashColor = FTT.UI.UIPalette.UnboundCold;
        private static readonly Color EncounterWardNumberColor = FTT.UI.UIPalette.Slate;
        private const float EncounterWardFlashSeconds = 0.12f;

        // The owning lock's instance ID; 0 = not a member.
        private ulong _encounterToken;
        private string _encounterLockID = "";
        private bool _encounterDormant;
        private Rect2 _encounterBounds;
        private Vector2 _encounterAnchor;
        private bool _encounterFacesRight;

        /// <summary>
        /// Raised once by <see cref="Die"/>. Per instance — the global
        /// <c>EventBus.OnEnemyKilled</c> carries no identity. Its subscribers are
        /// dropped on both halves of a pool cycle, so a recycled body never
        /// reports to its previous owner.
        /// </summary>
        public event Action<EnemyController> Died;

        /// <summary>
        /// Incremented by every pool <see cref="OnSpawn"/>: a lock that registered
        /// a body at one generation and sees another knows the pool recycled it.
        /// </summary>
        public int SpawnGeneration { get; private set; }

        /// <summary>True while this mob belongs to an encounter lock.</summary>
        public bool IsEncounterMember => _encounterToken != 0;

        /// <summary>The owning lock's instance ID (0 = none). The lock's membership check.</summary>
        public ulong EncounterToken => _encounterToken;

        /// <summary>The owning lock's ID, or "".</summary>
        public string EncounterLockID => _encounterLockID;

        /// <summary>True while a lock holds this member dormant: no AI, and every player hit refused.</summary>
        public bool IsEncounterDormant => _encounterDormant && _encounterToken != 0;

        /// <summary>The lock's area this member is clamped into (global); default when not a member.</summary>
        public Rect2 EncounterBounds => _encounterToken != 0 ? _encounterBounds : default;

        /// <summary>The authored spawn point the fall net and the stall watchdog return the member to.</summary>
        public Vector2 EncounterAnchor => _encounterAnchor;

        /// <summary>Times the encounter fall net caught this body since it last spawned. Test seam.</summary>
        public int EncounterFallRecoveries { get; private set; }

        /// <summary>Times this body was put back on its anchor (fall net and stall watchdog). Test seam.</summary>
        public int EncounterRelocations { get; private set; }

        /// <summary>Player hits the dormant ward refused since this body last spawned. Test seam.</summary>
        public int EncounterWardedHitsRefused { get; private set; }

        /// <summary>
        /// Registers this body with a lock. <paramref name="token"/> is the lock's
        /// instance ID, <paramref name="bounds"/> its area (only the x-span
        /// clamps; its bottom is the fall net's reference),
        /// <paramref name="anchor"/> the authored spawn point and
        /// <paramref name="facesRight"/> the way a dormant member faces (toward
        /// the entry). A summon or a drill-invulnerable body is never a member.
        /// </summary>
        public bool JoinEncounter(ulong token, string lockID, Rect2 bounds, Vector2 anchor, bool dormant, bool facesRight) {
            if (token == 0 || IsSummoned || DrillInvulnerable || CurrentState == EnemyState.Dead) return false;
            _encounterToken = token;
            _encounterLockID = lockID ?? "";
            _encounterBounds = bounds;
            _encounterAnchor = anchor;
            _encounterFacesRight = facesRight;
            SetEncounterDormant(dormant);
            return true;
        }

        /// <summary>
        /// Puts the member to sleep or wakes it. Going dormant drops whatever the
        /// mob was doing — the attack in flight, the target, a stun, a launch —
        /// and keeps its HP; waking leaves it idle until <see cref="Engage"/>.
        /// </summary>
        public void SetEncounterDormant(bool dormant) {
            _encounterDormant = dormant;
            if (!dormant || CurrentState == EnemyState.Dead) return;
            Executor.Cancel();
            _attackHitbox?.Deactivate();
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
            _armoredAnswerPending = false;
            _standOffEngaged = false;
            _secondaryBlinkInFlight = false;
            _stunTimer = 0f;
            ResetLaunchCarry();
            _target = null;
            if (CurrentState != EnemyState.Patrol) CurrentState = EnemyState.Patrol;
            Velocity = new Vector2(0f, IsFlying ? 0f : Velocity.Y);
            SetFacing(_encounterFacesRight);
        }

        /// <summary>
        /// The lock's engage: wakes the member into Chase on <paramref name="hero"/>,
        /// whatever its aggro radius says. A member already fighting keeps what
        /// it is doing.
        /// </summary>
        public void Engage(FTT.Characters.PlayerController hero) {
            if (CurrentState == EnemyState.Dead) return;
            _encounterDormant = false;
            if (hero == null || !IsInstanceValid(hero)) return;
            _target = hero;
            if (CurrentState is EnemyState.Patrol or EnemyState.Returning or EnemyState.Idle) {
                CurrentState = EnemyState.Chase;
                _standOffEngaged = false;
            }
        }

        /// <summary>True while an engaged member has lost its target and needs re-engaging. The lock's poll.</summary>
        public bool NeedsEncounterEngage =>
            IsEncounterMember && !IsEncounterDormant && CurrentState is EnemyState.Patrol or EnemyState.Returning or EnemyState.Idle;

        /// <summary>Drops this body's membership (bounds, dormancy, subscribers). The lock's release and the pool hooks.</summary>
        public void LeaveEncounter() => ResetEncounterMembership();

        private void ResetEncounterMembership() {
            _encounterToken = 0;
            _encounterLockID = "";
            _encounterDormant = false;
            _encounterBounds = default;
            _encounterAnchor = Vector2.Zero;
            _encounterFacesRight = false;
            Died = null;
        }

        /// <summary>The pool's <see cref="OnSpawn"/> hook: a new generation with no membership.</summary>
        private void BeginEncounterSpawnGeneration() {
            SpawnGeneration++;
            ResetEncounterMembership();
            EncounterFallRecoveries = 0;
            EncounterRelocations = 0;
            EncounterWardedHitsRefused = 0;
        }

        /// <summary>
        /// The non-preserve <see cref="ApplyStoryRewind"/> hook: the body went back
        /// to its spawn or checkpoint. Membership is the lock's, so it is kept;
        /// the clamp and the dormant facing are re-applied to the restored body.
        /// </summary>
        private void OnEncounterRewindRestored() {
            if (!IsEncounterMember) return;
            ApplyEncounterBounds();
            if (IsEncounterDormant) SetFacing(_encounterFacesRight);
        }

        private void RaiseDied() => Died?.Invoke(this);

        /// <summary>
        /// The dormant tick, in place of the AI's state step: decelerate to rest,
        /// face the entry, idle. Returns false when the mob is not dormant.
        /// </summary>
        private bool TickEncounterDormant(float dt) {
            if (!IsEncounterDormant) return false;
            float decel = StandOffDecelerationPerSecond * dt;
            Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, decel), IsFlying ? 0f : Velocity.Y);
            SetFacing(_encounterFacesRight);
            PlayAnimation("idle");
            return true;
        }

        /// <summary>
        /// The member clamp and fall net, after every move: the pivot kept its
        /// half-width inside the area, and a body whose feet have dropped past the
        /// fall net line put back on its anchor. A held body is the hero's.
        /// </summary>
        private void ApplyEncounterBounds() {
            if (!IsEncounterMember || CurrentState == EnemyState.Dead || _isHeldByPlayer) return;
            float x = GlobalPosition.X;
            float clamped = EncounterLockRules.ClampMemberX(
                x, _encounterBounds.Position.X, _encounterBounds.End.X, _bodyHalfWidth);
            if (!Mathf.IsEqualApprox(x, clamped)) {
                GlobalPosition = new Vector2(clamped, GlobalPosition.Y);
                Velocity = new Vector2(0f, Velocity.Y);
            }
            if (_encounterBounds.Size.Y > 0f
                && EncounterLockRules.IsBelowFallNet(GlobalPosition.Y, _encounterBounds.End.Y)) {
                EncounterFallRecoveries++;
                RelocateToEncounterAnchor("fall");
            }
        }

        /// <summary>Test seam: the clamp and fall net outside a physics step.</summary>
        internal void ApplyEncounterBoundsForTest() => ApplyEncounterBounds();

        /// <summary>
        /// Puts the member back on its authored spawn point through the
        /// relocation rule — asked from the anchor itself, so it validates the
        /// landing's own floor and never sweeps up through the floor a fallen body
        /// is under — with its velocity, launch and slam bounce cleared. Used by
        /// the fall net and by the lock's stall watchdog; never a kill.
        /// </summary>
        public FTT.Combat.RelocationResult RelocateToEncounterAnchor(string reason) {
            Vector2 from = GlobalPosition;
            Vector2 anchor = _encounterAnchor;
            FTT.Combat.RelocationResult result = FTT.Combat.SafeRelocation.Resolve(this, anchor, anchor,
                FTT.Combat.SafeRelocation.BodyOf(this, needsFloor: !IsFlying, passesWalls: Data?.PhasesThroughWalls == true));
            GlobalPosition = result.Position;
            Velocity = Vector2.Zero;
            ResetLaunchCarry();
            _slamBouncePending = false;
            _slamBounceSpeed = 0f;
            _thrownFlight = false;
            EncounterRelocations++;
            GD.Print($"[encounter-lock] '{Data?.EnemyID}' of '{_encounterLockID}' ({reason}) at "
                + $"({from.X:0},{from.Y:0}); returned to ({result.Position.X:0},{result.Position.Y:0}).");
            return result;
        }

        /// <summary>
        /// The dormant ward at the hit intake: a dormant member refuses the hit
        /// outright and reads as refused — a cold flash and, for a player-dealt
        /// hit, a grey "0". Returns true when the hit was refused.
        /// </summary>
        private bool RefuseEncounterHit(in FTT.Combat.HitPayload hit) {
            if (!IsEncounterDormant || CurrentState == EnemyState.Dead) return false;
            EncounterWardedHitsRefused++;
            _glow?.FlashHit(EncounterWardFlashColor, EncounterWardFlashSeconds);
            if (IsInsideTree()
                && FTT.Combat.StoryEnemyCombatRules.IsPlayerDealt(hit.AttackerIndex, hit.AttackClass, hit.Delivery)) {
                FTT.UI.FloatingDamageNumber.Show(0, DamageNumberPosition, GetParent(), EncounterWardNumberColor);
            }
            return true;
        }

        /// <summary>
        /// An engaged member keeps chasing a target anywhere inside its lock's
        /// area, whatever the P4 leash says: the hero cannot leave the area, so
        /// "too far away" only means "on the far side of the fight".
        /// </summary>
        private bool KeepsChasingInEncounter() {
            if (!IsEncounterMember || IsEncounterDormant) return false;
            if (_target == null || !IsInstanceValid(_target)) return false;
            float x = _target.GlobalPosition.X;
            return x >= _encounterBounds.Position.X && x <= _encounterBounds.End.X;
        }
    }
}
