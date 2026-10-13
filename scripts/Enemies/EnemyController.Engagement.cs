using Godot;
using FTT.Combat;
using FTT.Core;

namespace FTT.Enemies {

    /// <summary>
    /// Gameplay update 2026-10-12, request 6 (workstream GU-F): the mob's half of
    /// the Story engagement director. The user: "I want the enemies to maintain
    /// more separation, especially on normal mode. They shouldn't just make a
    /// beeline for the character. They should wait their turn to come and attack
    /// the character."
    ///
    /// <para><b>Gate decisions, not actions.</b> Only the AI's own commit points
    /// are gated: <see cref="ProcessChase"/>'s commit and the engage blink. A
    /// melee mob without one of the target's melee slots holds on a spaced wait
    /// ring instead of walking in; a ranged mob without a free ranged permit
    /// holds in its band instead of firing. <see cref="BeginAttack"/>,
    /// <see cref="CommitArmoredAnswer"/>, <see cref="TryPressureExitAttack"/>
    /// and <see cref="TryReactivePhaseSkip"/> are never gated, so retaliation
    /// stays V7.4-legal (and only ever fires in reach), and scripted callers are
    /// untouched. A lone mob takes a slot on its first Chase tick and behaves
    /// exactly as it did before.</para>
    ///
    /// <para><b>Rotation.</b> A holder keeps its slot through its cooldown, a
    /// stun, a grab, a thrown flight and a freeze. When its attack ends (or its
    /// finisher flight touches down) with a rested waiter able to take the slot,
    /// it hands the slot off and becomes a waiter with a rest
    /// (<see cref="StoryEngagementRules.RestFramesAfterHandoff"/>); a holder that
    /// spends <see cref="StoryEngagementRules.StaleSlotFrames"/> Chase ticks
    /// without committing while such a waiter exists hands off too. Every counter
    /// ticks only in the mob's own unfrozen, un-hitstopped ticks.</para>
    ///
    /// <para><b>The wait ring.</b> A waiter stands at
    /// <c>aim.X + side × WaitGapForRank(rank)</c> on the side it stands on,
    /// approaching at 0.8 and backing off at 0.6 of its chase speed (never above
    /// 0.9 of the hero's run speed), facing the hero, and stopping at ledges and
    /// walls either way. When a wall, a ledge or a barrier cuts off its place on
    /// its side (rank 1 or more) and the other side is less occupied, the side
    /// is rebalanced: the waiter crosses to the other side, its pushbox stood
    /// down for the crossing so the hero's does not hold it back.</para>
    ///
    /// <para><b>Ranged permits</b> are counted from the members mid ranged
    /// attack; among shooters refused for want of one, the one refused longest
    /// takes the next, so tree order never starves a shooter.</para>
    ///
    /// <para>Story only, never persisted or snapshotted; nothing here reaches the
    /// Fighter simulation.</para>
    /// </summary>
    public partial class EnemyController {

        private StoryEngagementRules.EngagementRole _engagementRole;
        private ulong _engagementTargetId;
        // Own unfrozen Chase ticks spent waiting since the mob last held a slot.
        private int _engagementWaitFrames;
        // Own unfrozen ticks of rest left after a hand-off.
        private int _engagementRestFrames;
        // Consecutive Chase ticks a holder spent without committing while an
        // eligible waiter existed.
        private int _engagementStaleFrames;
        // The side a waiter rings (−1 left of the target, +1 right; 0 = unset).
        private int _engagementRingSide;
        private bool _engagementCrossing;
        private int _engagementCrossCooldownFrames;
        // The waiter's last ring step away from the target was blocked.
        private bool _engagementBlockedAway;
        // The pushbox this mob stood down for a crossing.
        private bool _engagementPushSuppressed;
        // Consecutive ticks this mob's ready ranged commit was refused for want
        // of a permit (ranged fairness), and whether it asked in the last tick.
        private int _engagementRangedRefusedFrames;
        private bool _engagementRangedAsked;
        // The side of the target the engage blink in flight lands on (read only
        // while IsEngageBlinking).
        private int _engagementBlinkLandingSide;

        // === Test seams ===

        /// <summary>The mob's place in its target's rotation. Test seam.</summary>
        public StoryEngagementRules.EngagementRole EngagementRole => _engagementRole;

        /// <summary>
        /// The mob's wait-ring rank on its side (0 = nearest the target), or −1
        /// when it is not a waiter holding on the ring. Test seam.
        /// </summary>
        public int WaitRingRank => StoryEngagementDirector.WaitRank(this);

        /// <summary>The gap, pivot to pivot, its current rank stands at (0 when not ringing). Test seam.</summary>
        public float WaitRingGapPixels {
            get {
                int rank = WaitRingRank;
                return rank < 0 ? 0f : StoryEngagementRules.WaitGapForRank(rank, StoryEngagementDirector.CurrentDifficulty);
            }
        }

        /// <summary>Own ticks of rest left after a hand-off. Test seam.</summary>
        public int EngagementRestFramesRemaining => _engagementRestFrames;

        /// <summary>True while the waiter crosses to the other side of the target. Test seam.</summary>
        public bool IsEngagementCrossing => _engagementCrossing;

        // === The director's view (StoryEngagementDirector) ===

        internal ulong EngagementTargetId => _engagementTargetId;
        internal bool IsEngagementElite => Data?.Tier == EnemyTier.Elite;
        internal bool EngagementRested => _engagementRestFrames <= 0;
        internal int EngagementWaitedFrames => _engagementWaitFrames;
        internal int EngagementRangedRefusedFrames => _engagementRangedRefusedFrames;

        /// <summary>The side of its aim the mob stands on: −1 left, +1 right (facing breaks a tie).</summary>
        internal int EngagementSide {
            get {
                float dx = GlobalPosition.X - TargetAimPosition.X;
                if (dx != 0f) return dx < 0f ? -1 : 1;
                return _facingRight ? -1 : 1;
            }
        }

        /// <summary>The side a waiter rings: the side it is crossing to, else the side it stands on.</summary>
        internal int EngagementRingSide => _engagementCrossing && _engagementRingSide != 0 ? _engagementRingSide : EngagementSide;

        /// <summary>
        /// The side a slot holder counts on: an engage blink in flight counts on
        /// the side it lands on from its first telegraph frame (review fix
        /// 2026-10-13), so no other mob is granted that side's slot while the
        /// blinker still stands on the near side; otherwise the side it stands on.
        /// </summary>
        internal int EngagementHolderSide =>
            IsEngageBlinking && _engagementBlinkLandingSide != 0 ? _engagementBlinkLandingSide : EngagementSide;

        /// <summary>The side of the target an engage blink fired now lands on (the executor's far-side rule).</summary>
        private int EngageBlinkLandingSide =>
            EnemyAbilityExecutor.TeleportLandingSide(GlobalPosition.X, _target.GlobalPosition.X);

        /// <summary>The horizontal gap to its aim, pivot to pivot, in pixels.</summary>
        internal float EngagementGapPixels => Mathf.Abs(GlobalPosition.X - TargetAimPosition.X);

        /// <summary>The attack this mob throws next needs a melee slot.</summary>
        internal bool EngagementWantsMeleeSlot => ClassOfNext(PreviewNextAttack()) == StoryEngagementRules.EngagementClass.MeleeSlot;

        /// <summary>A joined waiter in Chase whose next attack needs a melee slot: it holds on the wait ring.</summary>
        internal bool IsRingingWaiter =>
            _engagementRole == StoryEngagementRules.EngagementRole.Waiting
            && CurrentState == EnemyState.Chase
            && EngagementWantsMeleeSlot;

        /// <summary>A ringing waiter that is not mid-crossing: it may be granted a free slot.</summary>
        internal bool IsMeleeSlotCandidate => IsRingingWaiter && !_engagementCrossing;

        /// <summary>
        /// The class of the attack an <c>Attacking</c> mob has in flight, or is
        /// about to start during its reaction delay (the ranged-permit count).
        /// </summary>
        internal StoryEngagementRules.EngagementClass EngagementInFlightClass {
            get {
                if (CurrentState != EnemyState.Attacking) return StoryEngagementRules.EngagementClass.Free;
                if (Executor.IsBusy) {
                    EnemyAbilityData active = Executor.ActiveAbility;
                    return active == null
                        ? StoryEngagementRules.EngagementClass.Free
                        : StoryEngagementRules.ClassOf(active.Archetype, isEngageBlink: _secondaryBlinkInFlight);
                }
                return _attackCommitted ? StoryEngagementRules.EngagementClass.Free : ClassOfNext(PreviewNextAttack());
            }
        }

        private static StoryEngagementRules.EngagementClass ClassOfNext(EnemyAbilityData next) =>
            next == null
                ? StoryEngagementRules.EngagementClass.Free
                : StoryEngagementRules.ClassOf(next.Archetype, isEngageBlink: false);

        /// <summary>The director puts the mob in a group as a fresh, rested waiter.</summary>
        internal void BeginEngagement(ulong targetId) {
            ClearEngagementState();
            _engagementTargetId = targetId;
            _engagementRole = StoryEngagementRules.EngagementRole.Waiting;
        }

        /// <summary>
        /// Clears every engagement field and stands a crossing's pushbox back up.
        /// Called by the director when it drops the mob, and by every leave hook.
        /// </summary>
        internal void ClearEngagementState() {
            EndEngagementCrossing(armCooldown: false);
            _engagementRole = StoryEngagementRules.EngagementRole.None;
            _engagementTargetId = 0;
            _engagementWaitFrames = 0;
            _engagementRestFrames = 0;
            _engagementStaleFrames = 0;
            _engagementRingSide = 0;
            _engagementCrossCooldownFrames = 0;
            _engagementBlockedAway = false;
            _engagementRangedRefusedFrames = 0;
            _engagementRangedAsked = false;
            _engagementBlinkLandingSide = 0;
        }

        // === Hooks (one-line calls from EnemyController.cs) ===

        /// <summary>
        /// Leave hook: StartReturning, Die, OnSpawn, OnDespawn, the resetting
        /// rewind restore and _ExitTree. Frees whatever slot the mob held.
        /// </summary>
        private void LeaveEngagement() => StoryEngagementDirector.Leave(this);

        /// <summary>
        /// The counter hook, once per unfrozen, un-hitstopped, unheld tick before
        /// the state switch: rest and the crossing cooldown run down in every such
        /// tick; waited frames count only in Chase; the stale count and a
        /// crossing end as soon as the mob leaves Chase; and the ranged refusal
        /// count lapses unless the mob asked for a permit in its last tick.
        /// </summary>
        private void TickEngagementCounters() {
            if (_engagementRole == StoryEngagementRules.EngagementRole.None) return;
            if (_engagementRestFrames > 0) _engagementRestFrames--;
            if (_engagementCrossCooldownFrames > 0) _engagementCrossCooldownFrames--;
            if (!_engagementRangedAsked) _engagementRangedRefusedFrames = 0;
            _engagementRangedAsked = false;
            if (CurrentState != EnemyState.Chase) {
                _engagementStaleFrames = 0;
                if (_engagementCrossing) EndEngagementCrossing(armCooldown: true);
                return;
            }
            if (_engagementRole == StoryEngagementRules.EngagementRole.Waiting && _engagementWaitFrames < int.MaxValue) {
                _engagementWaitFrames++;
            }
        }

        /// <summary>
        /// The engage-blink gate: the blink is a melee commit from range, so it
        /// needs one of the target's melee slots — <b>on the side it lands
        /// on</b>, the target's far side (review fix 2026-10-13: the slot used to
        /// be judged against the side the mob stood on and then spent on the
        /// other, which put two attackers on one side on Normal). A waiter is
        /// granted a slot here only when that side has room; a holder blinks only
        /// when that side stays inside its per-side cap, and otherwise keeps its
        /// slot and walks in. A mob without a standard Teleport, or whose blink
        /// would not fire this tick, is never refused here —
        /// <see cref="TryEngageBlink"/> refuses on its own and the wait ring
        /// decides.
        /// </summary>
        private bool EngagementPermitsEngageBlink() {
            if (FindStandardTeleport() == null || !EnsureEngaged()) return true;
            if (!EngageBlinkReady(out _)) return true;
            int landingSide = EngageBlinkLandingSide;
            if (_engagementRole == StoryEngagementRules.EngagementRole.Slot) {
                return StoryEngagementDirector.SideHasRoomForHolder(this, landingSide);
            }
            return HoldsOrTakesMeleeSlot(landingSide);
        }

        /// <summary>
        /// The Chase gate before the commit: keeps the holder's stale count (and
        /// hands a stale slot off), lets a waiter whose next attack needs a melee
        /// slot take a free one, and otherwise holds it on the wait ring. Returns
        /// true when it moved the mob this tick (the caller returns).
        /// </summary>
        private bool EngagementHoldsOnWaitRing(EnemyAbilityData next, Vector2 aim, float dt) {
            if (!EnsureEngaged()) return false;
            if (_engagementRole == StoryEngagementRules.EngagementRole.Slot) {
                if (StoryEngagementDirector.HasEligibleWaiterFor(this)) _engagementStaleFrames++;
                else _engagementStaleFrames = 0;
                if (!StoryEngagementRules.IsStale(_engagementStaleFrames)) return false;
                HandOffEngagementSlot();
            }
            if (ClassOfNext(next) != StoryEngagementRules.EngagementClass.MeleeSlot) return false;
            if (HoldsOrTakesMeleeSlot()) return false;
            HoldOnWaitRing(aim, dt);
            return true;
        }

        /// <summary>
        /// The commit gate after the reach test: a ranged-class attack needs a
        /// free ranged permit, and takes it only when no shooter has been refused
        /// longer; anything else commits (a melee commit only gets here holding a
        /// slot).
        /// </summary>
        private bool EngagementPermitsCommit(EnemyAbilityData next) {
            if (!EnsureEngaged()) return true;
            if (ClassOfNext(next) != StoryEngagementRules.EngagementClass.RangedPermit) return true;
            _engagementRangedAsked = true;
            if (StoryEngagementDirector.IsRangedPermitFree(this)
                && !StoryEngagementDirector.HasLongerRangedWaiter(this)) {
                _engagementRangedRefusedFrames = 0;
                return true;
            }
            if (_engagementRangedRefusedFrames < int.MaxValue) _engagementRangedRefusedFrames++;
            return false;
        }

        /// <summary>
        /// The hand-off at the end of an attack (ProcessAttacking's end): a holder
        /// whose slot a rested waiter could take hands it off. An engage blink is
        /// the approach, not the attack, so it never hands off.
        /// </summary>
        private void EngagementOnAttackEnded(bool engageBlink) {
            if (engageBlink || _engagementRole != StoryEngagementRules.EngagementRole.Slot) return;
            _engagementStaleFrames = 0;
            if (StoryEngagementDirector.HasEligibleWaiterFor(this)) HandOffEngagementSlot();
        }

        /// <summary>A D6a finisher flight touching down hands the slot off only when a waiter is eligible.</summary>
        private void EngagementOnFinisherTouchdown() {
            if (_engagementRole != StoryEngagementRules.EngagementRole.Slot) return;
            if (StoryEngagementDirector.HasEligibleWaiterFor(this)) HandOffEngagementSlot();
        }

        // === Internals ===

        /// <summary>
        /// Joins the target's group when the director applies (a valid campaign
        /// hero target, director not bypassed); leaves any group otherwise.
        /// </summary>
        private bool EnsureEngaged() {
            if (_target != null && IsInstanceValid(_target) && StoryEngagementDirector.Join(this, _target)) return true;
            if (_engagementRole != StoryEngagementRules.EngagementRole.None) LeaveEngagement();
            return false;
        }

        /// <summary>
        /// True when the mob holds a melee slot, taking a free one it is granted.
        /// <paramref name="sideOverride"/> (±1) judges the grant as if the mob
        /// stood on that side (the engage blink's landing side); 0 uses the side
        /// it stands on.
        /// </summary>
        private bool HoldsOrTakesMeleeSlot(int sideOverride = 0) {
            if (_engagementRole == StoryEngagementRules.EngagementRole.Slot) return true;
            if (!StoryEngagementDirector.IsGrantedMeleeSlot(this, sideOverride)) return false;
            EndEngagementCrossing(armCooldown: false);
            _engagementRole = StoryEngagementRules.EngagementRole.Slot;
            _engagementWaitFrames = 0;
            _engagementStaleFrames = 0;
            _engagementBlockedAway = false;
            return true;
        }

        private void HandOffEngagementSlot() {
            _engagementRole = StoryEngagementRules.EngagementRole.Waiting;
            _engagementRestFrames = StoryEngagementRules.RestFramesAfterHandoff(StoryEngagementDirector.CurrentDifficulty);
            _engagementWaitFrames = 0;
            _engagementStaleFrames = 0;
            _engagementRingSide = 0;
        }

        /// <summary>
        /// The hero's run speed in px/s, read at runtime from the target: its
        /// authored speed with the Story perk and drop-buff multipliers. Status
        /// and terrain multipliers are left out, so a rooted or wading hero does
        /// not freeze the ring.
        /// </summary>
        private float EngagementHeroRunSpeedPixels =>
            _target == null || !IsInstanceValid(_target)
                ? 0f
                : (_target.Data?.MaxMoveSpeed ?? 8f) * _target.StoryMoveSpeedMultiplier
                    * _target.StoryTemporarySpeedMultiplier * PixelsPerUnit;

        /// <summary>
        /// Holds a waiter on its wait-ring rank: rebalances its side, then walks
        /// toward <c>aim.X + side × gap</c> at the waiter speed, facing the
        /// target, and stops inside the tolerance or at a ledge or wall.
        /// </summary>
        private void HoldOnWaitRing(Vector2 aim, float dt) {
            // The band's hysteresis restarts cleanly once the mob gets a slot.
            _standOffEngaged = false;
            Difficulty difficulty = StoryEngagementDirector.CurrentDifficulty;
            RebalanceRingSide(aim, difficulty);
            int side = EngagementRingSide;
            int rank = Mathf.Max(0, StoryEngagementDirector.WaitRank(this));
            float desiredX = aim.X + side * StoryEngagementRules.WaitGapForRank(rank, difficulty);
            float dx = desiredX - GlobalPosition.X;
            if (aim.X != GlobalPosition.X) SetFacing(aim.X > GlobalPosition.X);

            if (!_engagementCrossing && Mathf.Abs(dx) <= StoryEngagementRules.WaitRingTolerancePixels) {
                _engagementBlockedAway = false;
                float decel = StandOffDecelerationPerSecond * dt;
                Velocity = new Vector2(
                    Mathf.MoveToward(Velocity.X, 0f, decel),
                    IsFlying ? Mathf.MoveToward(Velocity.Y, 0f, decel) : Velocity.Y);
                PlayAnimation("idle");
                return;
            }

            float direction = Mathf.Sign(dx);
            bool towardTarget = direction == Mathf.Sign(aim.X - GlobalPosition.X);
            if (IsWaitStepBlocked(direction)) {
                // A ledge or a wall stops the step on the spot, either way.
                _engagementBlockedAway = !towardTarget;
                if (_engagementCrossing) EndEngagementCrossing(armCooldown: true);
                Velocity = new Vector2(0f, IsFlying ? 0f : Velocity.Y);
                PlayAnimation("idle");
                return;
            }
            _engagementBlockedAway = false;
            float speed = StoryEngagementRules.WaiterSpeedPixels(MoveSpeedPixels, EngagementHeroRunSpeedPixels, towardTarget);
            float verticalVelocity = IsFlying
                ? Mathf.Clamp(aim.Y - GlobalPosition.Y, -MoveSpeedPixels, MoveSpeedPixels)
                : Velocity.Y;
            Velocity = new Vector2(direction * speed, verticalVelocity);
            PlayAnimation("patrol");
        }

        /// <summary>
        /// The ledge-and-wall stop for a ring step, in either direction: a ground
        /// walker uses the chase probes; a flyer stops only at a wall, and a
        /// phaser never.
        /// </summary>
        private bool IsWaitStepBlocked(float direction) {
            if (direction == 0f) return false;
            if (!IsFlying) return IsChaseStepBlocked(direction);
            return Data?.PhasesThroughWalls != true && HasWallAhead(direction);
        }

        /// <summary>
        /// Side rebalancing. A crossing ends once the waiter stands on its new
        /// side. Otherwise, out of its crossing cooldown, a waiter of rank 1 or
        /// more that has to back off to its ring position, and finds that
        /// position cut off (its last step away was blocked, or a wall stands
        /// between it and the position, or there is no floor there), crosses to
        /// the other side when that side holds fewer engaged melee mobs — and only
        /// when its ring position over there has floor under it and no wall in
        /// the way. On open floor nobody crosses.
        /// </summary>
        private void RebalanceRingSide(Vector2 aim, Difficulty difficulty) {
            int physicalSide = EngagementSide;
            if (_engagementCrossing) {
                // Done once it stands on its new side clear of pushbox contact,
                // so standing its pushbox back up never shoves the hero.
                if (physicalSide == _engagementRingSide && EngagementGapPixels >= EngagementContactGapPixels) {
                    EndEngagementCrossing(armCooldown: true);
                }
                return;
            }
            _engagementRingSide = physicalSide;
            if (_engagementCrossCooldownFrames > 0) return;
            int rank = StoryEngagementDirector.WaitRank(this);
            if (rank < 1) return;
            float ownX = aim.X + physicalSide * StoryEngagementRules.WaitGapForRank(rank, difficulty);
            // Only a waiter that has to move outward can find its place cut off.
            if ((ownX - GlobalPosition.X) * physicalSide <= StoryEngagementRules.WaitRingTolerancePixels) return;
            bool cutOff = _engagementBlockedAway || !CanReachRingPosition(ownX);
            StoryEngagementDirector.CountSides(this, out _, out _, out int presenceLeft, out int presenceRight);
            int own = physicalSide < 0 ? presenceLeft : presenceRight;
            int other = physicalSide < 0 ? presenceRight : presenceLeft;
            if (!StoryEngagementRules.ShouldCrossToOtherSide(rank, cutOff, own, other)) return;
            int farSide = -physicalSide;
            float farX = aim.X + farSide * StoryEngagementRules.WaitGapForRank(
                StoryEngagementDirector.RingingWaitersOn(this, farSide), difficulty);
            if (!CanReachRingPosition(farX)) return;
            _engagementRingSide = farSide;
            _engagementCrossing = true;
            _engagementBlockedAway = false;
            if (_pushbox != null && _pushbox.PushEnabled) {
                // The crossing walks through the hero's pushbox, which would
                // otherwise hold it on its side for good.
                _pushbox.SetPushEnabled(false);
                _engagementPushSuppressed = true;
            }
        }

        /// <summary>
        /// The pivot-to-pivot gap at which this mob's pushbox clears the hero's:
        /// both half-widths plus the D6c contact clearance (the band's own
        /// contact term).
        /// </summary>
        private float EngagementContactGapPixels {
            get {
                float ownHalfWidth = _pushbox != null ? _pushbox.BoxSize.X * 0.5f : _bodyHalfWidth;
                float heroHalfWidth = BasicComboRules.PushboxWidthUnits * KitMotionRules.StoryPixelsPerUnit / 2f;
                return ownHalfWidth + heroHalfWidth + StoryEnemyCombatRules.StandOffContactClearancePixels;
            }
        }

        /// <summary>
        /// The far-side ring position is reachable: no Environment wall between
        /// the body centre and it (a phaser ignores walls), and, for a ground
        /// walker, floor under it within the ledge probe depth.
        /// </summary>
        private bool CanReachRingPosition(float x) {
            Vector2 centre = GlobalPosition + new Vector2(0f, _bodyCenterY);
            if (Data?.PhasesThroughWalls != true
                && ProbeHits(centre, new Vector2(x, centre.Y), CollisionLayers.Environment, hitFromInside: false, fallback: true)) {
                return false;
            }
            if (IsFlying) return true;
            return ProbeHits(
                new Vector2(x, GlobalPosition.Y - StoryEnemyCombatRules.LedgeProbeRisePixels),
                new Vector2(x, GlobalPosition.Y + StoryEnemyCombatRules.LedgeProbeDepthPixels),
                CollisionLayers.EnemyBodyMask, hitFromInside: true, fallback: false);
        }

        private void EndEngagementCrossing(bool armCooldown) {
            if (_engagementPushSuppressed) {
                _engagementPushSuppressed = false;
                // A dead body keeps the pushbox Die() stood down.
                if (CurrentState != EnemyState.Dead) _pushbox?.SetPushEnabled(true);
            }
            if (!_engagementCrossing) return;
            _engagementCrossing = false;
            if (armCooldown) _engagementCrossCooldownFrames = StoryEngagementRules.CrossingCooldownFrames;
        }
    }
}
