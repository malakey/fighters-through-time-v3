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
    /// <item><b>Reach</b> (FIX-ENEMY, Phase C 2026-10-13): an engaged member
    ///       always comes within the hero's reach. A flyer settles to no higher
    ///       than <see cref="EncounterLockRules.FlyerHoverCapPixels"/> above the
    ///       hero's feet in every state but an attack or a stun, and never leaves
    ///       the top of its lock's camera rect; a melee ground member above the
    ///       hero walks off a ledge toward it when there is floor under the step
    ///       (the relocation rule's search depth) inside the area; and the stall
    ///       watchdog puts a member stalled on its anchor, or whose anchor is a
    ///       post, down beside the hero rather than back on the post.</item>
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
        // FIX-ENEMY: the top of the owning lock's camera rect (a flyer's band
        // top), read once at the join — the lock's rect is fixed at its build.
        // Negative infinity: no lock to read (no top clamp).
        private float _encounterViewTopY = float.NegativeInfinity;
        // FIX-ENEMY: the way (±1) a member seeking a way down off its ledge
        // walks; 0 while it is not seeking one.
        private int _encounterWayDown;

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
            _encounterViewTopY = InstanceFromId(token) is EncounterLock owner
                ? owner.CameraBounds.Position.Y
                : float.NegativeInfinity;
            SetEncounterDormant(dormant);
            return true;
        }

        /// <summary>
        /// Puts the member to sleep or wakes it. Going dormant drops whatever the
        /// mob was doing — the attack in flight, the target, a stun, a launch —
        /// and keeps its HP; waking leaves it idle until <see cref="Engage"/>.
        /// FIX-E (2026-10-13): going dormant also leaves the engagement director,
        /// so a dormant body holds no slot or wait role (plan §2.6: dormant lock
        /// members never join) and a re-engage queues it afresh by priority.
        /// </summary>
        public void SetEncounterDormant(bool dormant) {
            _encounterDormant = dormant;
            if (!dormant || CurrentState == EnemyState.Dead) return;
            StoryEngagementDirector.Leave(this);
            Executor.Cancel();
            _attackHitbox?.Deactivate();
            _attackCommitted = false;
            _reactionFramesRemaining = 0;
            _armoredAnswerPending = false;
            _standOffEngaged = false;
            _secondaryBlinkInFlight = false;
            _stunTimer = 0f;
            _encounterWayDown = 0;
            ResetLaunchCarry();
            _target = null;
            if (CurrentState != EnemyState.Patrol) CurrentState = EnemyState.Patrol;
            Velocity = new Vector2(0f, IsFlying ? 0f : Velocity.Y);
            SetFacing(_encounterFacesRight);
        }

        /// <summary>
        /// The lock's engage: wakes the member into Chase on <paramref name="hero"/>,
        /// whatever its aggro radius says. A member already fighting keeps what
        /// it is doing. FIX-E (2026-10-13): a member woken into Chase joins the
        /// engagement director here, before any member ticks, so the first melee
        /// grant of a lock's simultaneous wake sees every woken member and the
        /// plan §2.6 priority (elites first) decides it — not tree order. The
        /// join is idempotent; a lone mob's ordinary aggro path is untouched.
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
            if (CurrentState == EnemyState.Chase) StoryEngagementDirector.Join(this, hero);
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
            _encounterViewTopY = float.NegativeInfinity;
            _encounterWayDown = 0;
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
        /// FIX-ENEMY (2026-10-13): a flyer's body also stays below the top of its
        /// lock's camera rect (<see cref="EncounterLockRules.ClampFlyerFeetY"/>),
        /// its rise stopped at the edge.
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
            if (IsFlying) {
                float headHeight = _bodyHeight * 0.5f - _bodyCenterY;
                float feetY = EncounterLockRules.ClampFlyerFeetY(GlobalPosition.Y, _encounterViewTopY, headHeight);
                if (feetY > GlobalPosition.Y) {
                    GlobalPosition = new Vector2(GlobalPosition.X, feetY);
                    if (Velocity.Y < 0f) Velocity = new Vector2(Velocity.X, 0f);
                }
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
        /// FIX-ENEMY (2026-10-13): the stall watchdog's other destination. Puts
        /// the member down on the hero's floor, its own side of the hero first,
        /// at the rank-0 gap of the engagement director's wait ring for the
        /// current difficulty (where the director would hold it anyway), inside
        /// the area. The relocation rule validates it: the member's box swept out
        /// from the hero's feet (so no wall or closed barrier is crossed) and a
        /// floor under the landing. A side whose landing ends inside pushbox
        /// contact or has no floor is refused, then the other side is tried.
        /// Returns false when neither side takes the member (nothing moves).
        /// </summary>
        public bool RelocateBesideEncounterHero(FTT.Characters.PlayerController hero, string reason) {
            if (hero == null || !IsInstanceValid(hero) || !hero.IsInsideTree()) return false;
            Vector2 from = GlobalPosition;
            Vector2 heroFeet = hero.GlobalPosition;
            float gap = FTT.Combat.StoryEngagementRules.WaitGapForRank(0, StoryEngagementDirector.CurrentDifficulty);
            float contact = EngagementContactGapPixels;
            FTT.Combat.RelocationBody? body = FTT.Combat.SafeRelocation.BodyOf(this,
                needsFloor: !IsFlying, passesWalls: Data?.PhasesThroughWalls == true);
            float firstSide = from.X >= heroFeet.X ? 1f : -1f;
            for (int attempt = 0; attempt < 2; attempt++) {
                float side = attempt == 0 ? firstSide : -firstSide;
                float x = EncounterLockRules.ClampMemberX(heroFeet.X + side * gap,
                    _encounterBounds.Position.X, _encounterBounds.End.X, _bodyHalfWidth);
                FTT.Combat.RelocationResult result = FTT.Combat.SafeRelocation.Resolve(
                    this, heroFeet, new Vector2(x, heroFeet.Y), body);
                if (result.Outcome is FTT.Combat.RelocationOutcome.HeldAtOrigin
                    or FTT.Combat.RelocationOutcome.OriginUnsupported) continue;
                if (Mathf.Abs(result.Position.X - heroFeet.X) < contact) continue;
                GlobalPosition = result.Position;
                Velocity = Vector2.Zero;
                ResetLaunchCarry();
                _slamBouncePending = false;
                _slamBounceSpeed = 0f;
                _thrownFlight = false;
                EncounterRelocations++;
                GD.Print($"[encounter-lock] '{Data?.EnemyID}' of '{_encounterLockID}' ({reason}) at "
                    + $"({from.X:0},{from.Y:0}); put down beside the hero at ({result.Position.X:0},{result.Position.Y:0}).");
                return true;
            }
            GD.Print($"[encounter-lock] '{Data?.EnemyID}' of '{_encounterLockID}' ({reason}) at "
                + $"({from.X:0},{from.Y:0}): no supported point beside the hero; left where it stands.");
            return false;
        }

        /// <summary>
        /// FIX-ENEMY (2026-10-13), the flyer settle, after the AI's state step:
        /// an engaged flying member in Chase, Patrol, Returning or Idle — waiting
        /// on the ring, holding its ranged band, resting after its turn — never
        /// keeps an altitude above <see cref="EncounterLockRules.FlyerHoverLineY"/>;
        /// it descends to it at its own speed. A flyer used to keep whatever
        /// height it spawned at or ended a chase at, because every hold zeroes its
        /// vertical speed, and a drone holding its band 290 px over the hero
        /// could not be reached by the roster's weakest jumper. Attacks and stuns
        /// keep their own hover; a dormant member keeps its post.
        /// </summary>
        private void SettleEncounterFlyer(float dt) {
            if (!IsFlying || !IsEncounterMember || IsEncounterDormant) return;
            if (CurrentState is not (EnemyState.Chase or EnemyState.Patrol or EnemyState.Returning or EnemyState.Idle)) return;
            FTT.Characters.PlayerController hero = EncounterHero();
            if (hero == null) return;
            Velocity = new Vector2(Velocity.X, EncounterLockRules.FlyerSettleVelocityY(
                GlobalPosition.Y, hero.GlobalPosition.Y, Velocity.Y, MoveSpeedPixels, dt));
        }

        /// <summary>The hero an engaged member measures itself against: its target, else its lock's hero.</summary>
        private FTT.Characters.PlayerController EncounterHero() {
            FTT.Characters.PlayerController hero = _target;
            if (hero == null || !IsInstanceValid(hero)) {
                hero = (InstanceFromId(_encounterToken) as EncounterLock)?.Hero;
            }
            if (hero == null || !IsInstanceValid(hero) || !hero.IsInsideTree()) return null;
            return hero.CurrentState is FTT.Characters.CharacterState.Dead or FTT.Characters.CharacterState.Respawning
                ? null
                : hero;
        }

        /// <summary>
        /// FIX-ENEMY (2026-10-13): true while an engaged melee ground member's
        /// target stands lower than the chase's ledge probe reaches
        /// (<see cref="EncounterLockRules.SeeksWayDown"/>): it walks toward a way
        /// down instead of holding its band over the hero. Ranged members keep
        /// their posts — shooting from one is their job — and outside a lock P4
        /// is unchanged.
        /// </summary>
        private bool EncounterSeeksWayDown(Vector2 toTarget) =>
            !IsFlying && IsEncounterMember && !IsEncounterDormant && EngagementWantsMeleeSlot
            && EncounterLockRules.SeeksWayDown(toTarget.Y);

        /// <summary>
        /// FIX-ENEMY: whether a member seeking a way down may take the step in
        /// <paramref name="direction"/> although the chase's ledge probe found no
        /// floor within its depth: no wall ahead, the step's landing inside the
        /// area, and a floor under it within the relocation rule's search depth
        /// (<see cref="FTT.Combat.RelocationRules.FloorSearchDepthPixels"/> — a
        /// drop, never a void), through the chase's own edge probe.
        /// </summary>
        private bool MayStepOffLedgeInEncounter(float direction, Vector2 toTarget) {
            if (direction == 0f || !EncounterSeeksWayDown(toTarget) || !IsOnFloor()) return false;
            if (Data?.PhasesThroughWalls != true
                && ((IsOnWall() && GetWallNormal().X * direction < -0.5f) || HasWallAhead(direction))) return false;
            float stepX = GlobalPosition.X + Mathf.Sign(direction)
                * (_bodyHalfWidth + FTT.Combat.StoryEnemyCombatRules.LedgeProbeAheadMarginPixels);
            if (!EncounterLockRules.StepOffLandsInArea(stepX, _encounterBounds.Position.X, _encounterBounds.End.X,
                    _bodyHalfWidth)) return false;
            return HasFloorAhead(direction, FTT.Combat.RelocationRules.FloorSearchDepthPixels);
        }

        /// <summary>
        /// FIX-ENEMY: the way a member seeking a way down walks. Walking toward
        /// the target's x leads nowhere while the target stands under the same
        /// ledge (the member turns back each time it passes over it), so the
        /// member picks a way once — toward the target when it stands beyond the
        /// overhead hold's band, else the way it faces — and keeps it until it
        /// is down or meets a dead end (<see cref="TurnAtEncounterDeadEnd"/>).
        /// Returns <paramref name="sign"/> whenever it is not seeking a way down.
        /// </summary>
        private float ResolveEncounterWayDownSign(float sign, Vector2 toTarget) {
            if (!EncounterSeeksWayDown(toTarget)) {
                _encounterWayDown = 0;
                return sign;
            }
            if (_encounterWayDown == 0) {
                _encounterWayDown = Mathf.Abs(toTarget.X) > FTT.Combat.StoryEnemyCombatRules.ChaseHoldUnderTargetPixels
                    ? (int)Mathf.Sign(toTarget.X)
                    : _facingRight ? 1 : -1;
            }
            return _encounterWayDown;
        }

        /// <summary>
        /// FIX-ENEMY: a member seeking a way down that meets a wall, or a drop
        /// with no floor within the search depth, turns round to try the other
        /// end of its ledge next tick.
        /// </summary>
        private void TurnAtEncounterDeadEnd(Vector2 toTarget) {
            if (!EncounterSeeksWayDown(toTarget) || _encounterWayDown == 0) return;
            _encounterWayDown = -_encounterWayDown;
            SetFacing(_encounterWayDown > 0);
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
