using System.Collections.Generic;
using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Package 13 W5 — shared Story helpers for Harriet Tubman's kit (the
    /// roster swap, D3). Kept private to this file: nothing outside the kit
    /// needs them.
    /// </summary>
    internal static class TubmanKitStory {
        public const int MaxQueryResults = 16;

        /// <summary>The hurtbox layer this owner's hits target.</summary>
        public static uint TargetHurtboxLayer(PlayerController owner) =>
            owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;

        /// <summary>
        /// "Grounded" for a Story target: the nearest <see cref="CharacterBody2D"/>
        /// ancestor stands on the floor. A strike surface with no body (a
        /// checkpoint fracture, an extractor, a prop) sits on the ground and counts.
        /// </summary>
        public static bool IsGrounded(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) return body.IsOnFloor();
                current = current.GetParent();
            }
            return true;
        }

        public static IEnumerable<Hurtbox> Query(PlayerController owner, Shape2D shape, Vector2 center) {
            var space = owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) yield break;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = shape,
                Transform = new Transform2D(0f, center),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(TargetHurtboxLayer(owner))
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, MaxQueryResults)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == owner.PlayerIndex) continue;
                yield return hurtbox;
            }
        }
    }

    /// <summary>
    /// Special 1 — Conductor's Call (design §5, ABILITY_DATA). She raises her
    /// lantern and calls; a line of spectral Union scouts rushes <b>5 units</b>
    /// forward along the ground, dealing <b>18 damage</b> with a push to
    /// <b>grounded</b> targets. 12 startup, 18 recovery, 9 s; Special-class.
    ///
    /// <para>The rush is a moving ground box swept over the authored active
    /// window at the authored <c>ProjectileSpeed</c> (600 px/s × 30 frames =
    /// 300 px = 5 units); each target is struck at most once per cast and an
    /// airborne target is passed over. A local stand-in for the ground-wave
    /// primitive W7b owns (recorded in the P13 W5 handoff). The Story-only
    /// Minor Call Distance lengthens it by scaling the rush speed
    /// (<c>AbilityRange</c>, scoped to this ability).</para>
    ///
    /// <para><b>Safe Passage</b> (Story-only Major): the rush leaves a 2 s
    /// lantern trail along its path; while standing on it Tubman moves 20%
    /// faster.</para>
    /// </summary>
    public partial class TubmanConductorsCall : BaseSpecial {
        public const string SafePassagePerkKey = "safe_passage";
        /// <summary>Safe Passage: the trail's life after the rush finishes laying it (design: 2 s).</summary>
        public const float SafePassageTrailSeconds = 2f;
        /// <summary>Safe Passage: +20% move speed on the trail.</summary>
        public const float SafePassageSpeedMultiplier = 1.2f;
        /// <summary>Vertical tolerance for "on the trail", px (placeholder geometry).</summary>
        private const float TrailHalfHeightPixels = 40f;
        /// <summary>The scouts' box height above the ground line, px (placeholder geometry).</summary>
        private const float RushHeightPixels = 60f;

        private readonly HashSet<ulong> _struck = new();
        private float _facing = 1f;
        private float _originX;
        private float _groundY;
        private float _frontDistance;
        private float _rushSpeed;
        private float _trailSecondsLeft;
        private float _trailMinX;
        private float _trailMaxX;
        private float _trailY;

        /// <summary>How far the rush front has travelled this cast, px. Test seam.</summary>
        public float RushDistancePixels => _frontDistance;
        /// <summary>Seconds of Safe Passage trail left. Test seam.</summary>
        public float TrailSecondsRemaining => _trailSecondsLeft;

        /// <summary>The rush's full reach in px: authored speed × active frames, scaled by the Story-only Call Distance lane.</summary>
        public float RushReachPixels =>
            (Data?.ProjectileSpeed ?? 600f) * Mathf.Max(1, Data?.ActiveFrames ?? 30) / 60f
            * (Owner?.StoryScoped("AbilityRange", Data?.AbilityID ?? "tubman_conductors_call") ?? 1f);

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _facing = Owner.IsFacingRight ? 1f : -1f;
            _struck.Clear();
            _frontDistance = 0f;
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _originX = Owner.GlobalPosition.X;
            _groundY = Owner.GlobalPosition.Y;
            _frontDistance = 0f;
            _rushSpeed = RushReachPixels * 60f / Mathf.Max(1, Data?.ActiveFrames ?? 30);
            // F5: the scouts run exactly the authored active window on their own
            // count — a ground wave, like the sim's — so a caster freeze, which
            // holds the cast's phase clock, neither stops the rush nor stretches it.
            _rushFramesRemaining = PhaseFramesRemaining;
        }

        /// <summary>H-4: an interrupted call stops its rush (the trail is laid only by a finished one).</summary>
        protected override void OnInterrupted() => _rushFramesRemaining = 0;

        private int _rushFramesRemaining;

        /// <summary>True while the scouts are still running (test seam).</summary>
        public bool RushActive => _rushFramesRemaining > 0;

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            if (Owner != null && Owner.HasStoryPerk(SafePassagePerkKey) && _frontDistance > 0f) {
                float end = _originX + _facing * _frontDistance;
                _trailMinX = Mathf.Min(_originX, end);
                _trailMaxX = Mathf.Max(_originX, end);
                _trailY = _groundY;
                _trailSecondsLeft = SafePassageTrailSeconds;
            }
        }

        public override void _PhysicsProcess(double delta) {
            // F5: the rush and the Safe Passage trail are the world's and keep
            // running through a caster freeze; only the cast clock (base) holds.
            if (_rushFramesRemaining > 0) {
                _rushFramesRemaining--;
                AdvanceRush((float)delta);
            }
            TickSafePassage((float)delta);
            base._PhysicsProcess(delta);
        }

        private void AdvanceRush(float dt) {
            if (Owner == null) return;
            float previous = _frontDistance;
            _frontDistance = Mathf.Min(RushReachPixels, _frontDistance + _rushSpeed * dt);
            float boxWidth = Mathf.Max(Data?.HitboxSize.X ?? 60f, _frontDistance - previous);
            float centerDistance = Mathf.Max(0f, _frontDistance - boxWidth / 2f);
            var center = new Vector2(_originX + _facing * centerDistance, _groundY - RushHeightPixels / 2f);
            var shape = new RectangleShape2D { Size = new Vector2(boxWidth, RushHeightPixels) };
            foreach (Hurtbox hurtbox in TubmanKitStory.Query(Owner, shape, center)) {
                if (!TubmanKitStory.IsGrounded(hurtbox)) continue;
                if (!_struck.Add(hurtbox.GetInstanceId())) continue;
                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "tubman_conductors_call",
                    HitboxID = "conductors_call",
                    AttackClass = AttackClass.Special,
                    Damage = (Data?.BaseDamage ?? 18f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = Data?.KnockbackForce ?? new Vector2(4f, 0f),
                    HitstunDuration = Data?.HitstunDuration ?? 0.4f,
                    // The scouts push the target onward, along the rush.
                    HitOrigin = hurtbox.GlobalPosition - new Vector2(_facing * 30f, 0f),
                    AttackerFacingRight = _facing > 0f,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                float dealt = hurtbox.TakeHit(hit);
                // The impact VFX lands inside Credit.
                Credit(in hit, dealt, hurtbox.GlobalPosition);
            }
        }

        /// <summary>Safe Passage: while the trail lives, standing on it refreshes a +20% speed buff.</summary>
        private void TickSafePassage(float dt) {
            if (_trailSecondsLeft <= 0f || Owner == null) return;
            _trailSecondsLeft = Mathf.Max(0f, _trailSecondsLeft - dt);
            Vector2 at = Owner.GlobalPosition;
            bool onTrail = at.X >= _trailMinX && at.X <= _trailMaxX
                && Mathf.Abs(at.Y - _trailY) <= TrailHalfHeightPixels;
            // Never downgrade a stronger temporary buff (an orb's 1.4x).
            if (onTrail && Owner.StoryTemporarySpeedMultiplier <= SafePassageSpeedMultiplier) {
                Owner.ApplyStorySpeedBuff(SafePassageSpeedMultiplier, 2f / 60f);
            }
        }

        /// <summary>Test seam: is <paramref name="position"/> on the live trail?</summary>
        public bool IsOnTrail(Vector2 position) =>
            _trailSecondsLeft > 0f
            && position.X >= _trailMinX && position.X <= _trailMaxX
            && Mathf.Abs(position.Y - _trailY) <= TrailHalfHeightPixels;
    }

    /// <summary>
    /// Special 2 — Foresight (design §5, ABILITY_DATA): a counter stance drawn
    /// from her visions. After a 4-frame startup she holds a 20-frame window;
    /// a strike or projectile that would hit her from the front or rear is
    /// nullified (no damage, hitstun or block cost) and she sidesteps; if the
    /// attacker is within 2.5 units she answers with a 20-damage lantern strike
    /// that launches. One trigger per activation. Grabs and Ultimates beat it;
    /// windboxes, construct and zone ticks and hazards neither trigger it nor
    /// are stopped by it, and a counter strike cannot trigger another. A
    /// whiffed stance has 24 frames of recovery; 10 s cooldown.
    ///
    /// <para>The frame counts are the authored <c>.tres</c> phases, pinned equal
    /// to <see cref="TubmanKitRules"/> (the sim's copy of the same numbers).
    /// The Story-only Minor Foresight Window lengthens the window through the
    /// scoped <c>AbilityDuration</c> lane (+20% of 20 = +4 frames).
    /// <b>Never Lost a Passenger</b> (Story-only Major): a successful counter
    /// reclaims an extra 50% of the Rally echo pool on top of the counter
    /// strike's own damage-scaled reclaim.</para>
    ///
    /// <para><c>PlayerController.ResolveIncomingHit</c> asks
    /// <see cref="TryCounter"/> right after the Defy gate, so a caught hit
    /// reaches no shield, block or HP layer.</para>
    /// </summary>
    public partial class TubmanForesight : BaseSpecial {
        public const string NeverLostAPassengerPerkKey = "never_lost_a_passenger";
        /// <summary>Never Lost a Passenger: the extra Rally echo fraction reclaimed on a counter.</summary>
        public const float NeverLostAPassengerEchoFraction = 0.5f;

        private bool _triggered;
        private int _sidestepFrames;
        private int _answerDelayFrames;
        private int _windowFrames;

        /// <summary>True while the counter window is open (Active phase, not yet triggered).</summary>
        public bool IsWindowOpen => CurrentPhase == AbilityPhase.Active && !_triggered;
        /// <summary>True through the post-trigger sidestep: Tubman cannot be hit.</summary>
        public bool IsSidestepping => _sidestepFrames > 0;
        /// <summary>Counters landed by this node (test and presentation read-out).</summary>
        public int CountersLanded { get; private set; }
        /// <summary>The resolved window length for the current cast, frames. Test seam.</summary>
        public int WindowFrames => _windowFrames;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _triggered = false;
            _answerDelayFrames = 0;
            Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
        }

        protected override void OnActive() {
            float scale = Owner?.StoryScoped("AbilityDuration", Data?.AbilityID ?? "tubman_foresight") ?? 1f;
            _windowFrames = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, Data?.ActiveFrames ?? TubmanKitRules.ForesightWindowFrames) * scale));
            PhaseTimer = _windowFrames / 60f;
        }

        protected override void OnRecovery() {
            // A caught read recovers through the sidestep; a whiff pays the
            // authored 24-frame recovery.
            if (_triggered) {
                PhaseTimer = TubmanKitRules.ForesightSidestepFrames / 60f;
            } else {
                UseAuthoredPhaseFrames();
            }
        }

        protected override void OnInterrupted() {
            _triggered = false;
            _answerDelayFrames = 0;
        }

        public override void _PhysicsProcess(double delta) {
            // F5: the stance, its sidestep and the answer are the cast's, so they
            // hold with a frozen caster (the sim's component 322 does the same).
            if (CastClockSuspended) return;
            if (_sidestepFrames > 0) _sidestepFrames--;
            if (IsExecuting && Owner != null) {
                // Planted: the stance holds her ground.
                Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
            }
            if (_answerDelayFrames > 0 && --_answerDelayFrames == 0) Answer();
            base._PhysicsProcess(delta);
        }

        /// <summary>
        /// The trigger test. Eligible: a damaging Basic- or Special-class direct
        /// hit (a strike or a projectile) that is not a throw and not another
        /// Foresight's counter strike. Ultimates, zone/construct ticks, hazards,
        /// windboxes (zero damage) and grabs never trigger it.
        /// </summary>
        public bool TryCounter(in HitPayload hit) {
            if (!IsWindowOpen || Owner == null) return false;
            if (!IsCounterable(in hit)) return false;
            _triggered = true;
            CountersLanded++;
            _sidestepFrames = TubmanKitRules.ForesightSidestepFrames;
            // The answer lands on the next tick, after the caught hit resolves.
            _answerDelayFrames = 1;
            AdvanceToRecovery();
            return true;
        }

        public static bool IsCounterable(in HitPayload hit) =>
            hit.Damage > 0f
            && hit.AttackClass is AttackClass.Basic or AttackClass.Special
            && hit.Delivery == HitDelivery.DirectHit
            && hit.Origin != HitOrigin.Throw
            && hit.Origin != HitOrigin.Ultimate
            && hit.HitboxID != "foresight_answer";

        /// <summary>
        /// The lantern strike: the nearest hostile hurtbox within 2.5 units of
        /// Tubman takes her Special 2 damage as a launching Special-class hit.
        /// An attacker out of range (a projectile fired from afar) is not answered.
        /// </summary>
        private void Answer() {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            float reach = TubmanKitRules.ForesightAnswerRangeUnits * KitMotionRules.StoryPixelsPerUnit;
            Hurtbox nearest = null;
            float best = float.MaxValue;
            foreach (Hurtbox hurtbox in TubmanKitStory.Query(Owner, new CircleShape2D { Radius = reach }, Owner.GlobalPosition)) {
                // Answer a combatant (the attacker), never a strike-surface prop.
                if ((hurtbox.CollisionLayer & TubmanKitStory.TargetHurtboxLayer(Owner)) == 0) continue;
                float distance = hurtbox.GlobalPosition.DistanceTo(Owner.GlobalPosition);
                if (distance < best) {
                    best = distance;
                    nearest = hurtbox;
                }
            }
            if (nearest == null) return;
            Owner.IsFacingRight = nearest.GlobalPosition.X >= Owner.GlobalPosition.X;
            HitPayload hit = Stamp(new HitPayload {
                AttackerIndex = Owner.PlayerIndex,
                AttackID = Data?.AbilityID ?? "tubman_foresight",
                HitboxID = "foresight_answer",
                AttackClass = AttackClass.Special,
                Damage = (Data?.BaseDamage ?? 20f) * Owner.StorySpecialDamageMultiplier,
                Knockback = Data?.KnockbackForce ?? new Vector2(4f, -5f),
                HitstunDuration = Data?.HitstunDuration ?? 0.5f,
                HitOrigin = Owner.GlobalPosition,
                AttackerFacingRight = Owner.IsFacingRight,
                AppliedStatus = FTT.Core.StatusType.None,
                StatusDuration = 0f,
                StatusIntensity = 1f,
                ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.25f,
                ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
            });
            float dealt = nearest.TakeHit(hit);
            // The impact VFX lands inside Credit.
            Credit(in hit, dealt, nearest.GlobalPosition);
            if (dealt > 0f) {
                if (Owner.HasStoryPerk(NeverLostAPassengerPerkKey)) {
                    Owner.ReclaimRallyEchoFraction(NeverLostAPassengerEchoFraction);
                }
            }
        }
    }

    /// <summary>
    /// Movement — North Star Leap (design §5, ABILITY_DATA): a guided leap of
    /// <b>4 units in any of 8 directions over 18 frames</b>, usable in the air
    /// for recovery. If the leap ends within reach of a ledge she snaps to it
    /// from <b>0.5 units</b> farther than the normal capture box. The Lightning
    /// Blink pattern: direction latched from the held stick (neutral = facing),
    /// no carried momentum at the end.
    ///
    /// <para><b>Star Guide</b> (Story-only traversal): pressing Movement again
    /// mid-flight re-aims the remaining travel once, in the newly held
    /// direction, without restarting the cooldown. <b>North Star Ward</b>
    /// (Story-only Major, the third <c>ResonanceBarrier</c> source): each
    /// ACCEPTED activation grants the shared 10%-max-HP barrier; a Star Guide
    /// redirect is not a new activation.</para>
    /// </summary>
    public partial class TubmanNorthStarLeap : BaseSpecial {
        public const string StarGuidePerkKey = "star_guide";
        public const string NorthStarWardPerkKey = "north_star_ward";

        private Vector2 _direction = Vector2.Right;
        private Vector2 _startPosition;
        private float _speed;
        private bool _redirectUsed;
        private int _activationId;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        /// <summary>The leap's current unit direction. Test seam.</summary>
        public Vector2 LeapDirection => _direction;
        /// <summary>True once this leap's Star Guide redirect has been spent. Test seam.</summary>
        public bool RedirectUsed => _redirectUsed;

        /// <summary>Distance in Story px (the resource's <c>DistanceMoved</c>, 240 = 4 units).</summary>
        public float LeapDistancePixels =>
            MovementData?.DistanceMoved > 0f
                ? MovementData.DistanceMoved
                : TubmanKitRules.NorthStarLeapDistanceUnits * KitMotionRules.StoryPixelsPerUnit;

        /// <summary>The extended snap, px: 0.5 units beyond the normal capture box.</summary>
        public static float LedgeSnapBonusPixels =>
            TubmanKitRules.NorthStarLeapLedgeSnapBonusUnits * KitMotionRules.StoryPixelsPerUnit;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _redirectUsed = false;
            _direction = HeldDirection();
            _startPosition = Owner.GlobalPosition;
            int travelFrames = Mathf.Max(1, Data?.ActiveFrames ?? TubmanKitRules.NorthStarLeapTravelFrames);
            _speed = LeapDistancePixels * 60f / travelFrames;
            Owner.Velocity = Vector2.Zero;
            if (Owner.HasStoryPerk(NorthStarWardPerkKey)) {
                // D02a grant identity: one per ACCEPTED activation.
                Owner.GrantResonanceBarrier(StoryShieldEffect.NorthStarWard, ++_activationId);
            }
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            Owner.Velocity = Vector2.Zero;
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "North Star Leap",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
            // The extended snap: a ledge within reach of the leap's end catches her.
            FTT.Environment.LedgeGrabPoint ledge = Owner.FindLedgeWithinSnapReach(LedgeSnapBonusPixels);
            if (ledge != null) {
                AdvanceToCleanup();
                Owner.SnapToLedge(ledge);
            }
        }

        /// <summary>
        /// Star Guide: once per leap, while it is still travelling, re-aim the
        /// remaining travel along the newly held direction. Returns true when it
        /// took. It restarts nothing: no cooldown, no barrier grant.
        /// </summary>
        public bool TryStarGuideRedirect() {
            if (CurrentPhase != AbilityPhase.Active || _redirectUsed || Owner == null) return false;
            if (!Owner.HasStoryPerk(StarGuidePerkKey)) return false;
            Vector2 held = new(Owner.CurrentInputFrame.Horizontal, Owner.CurrentInputFrame.Vertical);
            if (held == Vector2.Zero) return false;
            _redirectUsed = true;
            _direction = EightWay(held);
            return true;
        }

        public override void _PhysicsProcess(double delta) {
            // F5: the leap carries its caster, so it holds with a frozen caster.
            if (CastClockSuspended) return;
            if (CurrentPhase == AbilityPhase.Startup) {
                Owner.Velocity = Vector2.Zero;
            } else if (CurrentPhase == AbilityPhase.Active) {
                Owner.Velocity = _direction * _speed;
            }
            base._PhysicsProcess(delta);
        }

        private Vector2 HeldDirection() {
            Vector2 held = new(Owner.CurrentInputFrame.Horizontal, Owner.CurrentInputFrame.Vertical);
            if (held == Vector2.Zero) held = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            return EightWay(held);
        }

        /// <summary>Snaps a stick vector to the nearest of the eight directions (Godot Y down).</summary>
        public static Vector2 EightWay(Vector2 held) {
            float x = Mathf.Abs(held.X) > 0.3f ? Mathf.Sign(held.X) : 0f;
            float y = Mathf.Abs(held.Y) > 0.3f ? Mathf.Sign(held.Y) : 0f;
            var snapped = new Vector2(x, y);
            return snapped == Vector2.Zero ? Vector2.Right : snapped.Normalized();
        }
    }

    /// <summary>
    /// Ultimate — The Freedom Line (design §5, ABILITY_DATA, D15): for one
    /// moment the Underground Railroad is made literal — a spectral train of
    /// lantern light roars across the stage, carrying her opponents along its
    /// line. <b>7 hits × 8, then a 20-damage final rush = 76</b>. Story Mode's
    /// version sweeps the screen: a wide owner-centred band
    /// (<c>HitboxSize</c>) struck every <c>DamageTickIntervalFrames</c> across
    /// the active window; the finale launches along her facing. The Fighter
    /// activation strike (a straight 6-unit lantern beam) is the sim's and the
    /// Mirror Paradox clone's (A02, W6's framework).
    /// </summary>
    public partial class TubmanFreedomLine : BaseSpecial {
        private int _hitsRemaining;
        private int _hitIndex;
        private int _countdownFrames;
        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate()
                && (_meter?.IsFull ?? Owner.CurrentUltimateMeter >= UltimateMeter.MaxValue);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _hitsRemaining = 0;
            _hitIndex = 0;
            _countdownFrames = 0;
            Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
            Owner.DrainUltimateMeter(UltimateMeter.MaxValue);
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            // D15: HitCount lantern pulses, then the final rush.
            _hitsRemaining = Data?.IsMultiHit == true ? Data.CinematicHitCount : 1;
            _hitIndex = 0;
            _countdownFrames = 0;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            // F5: the train's strikes are paced by the cast's active window, so
            // they hold with the frozen caster (the phase clock does too).
            if (CastClockSuspended) return;
            if (CurrentPhase != AbilityPhase.Active || _hitsRemaining <= 0) return;
            Owner.Velocity = new Vector2(0f, Owner.Velocity.Y);
            if (_countdownFrames > 0) {
                _countdownFrames--;
                return;
            }
            _hitIndex++;
            _hitsRemaining--;
            DealTrainHit(_hitIndex);
            _countdownFrames = Mathf.Max(1, Data?.DamageTickIntervalFrames ?? 18) - 1;
        }

        private void DealTrainHit(int hitIndex) {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            bool finale = Data?.IsFinaleHit(hitIndex) ?? false;
            bool carriesStatus = Data?.CinematicHitCarriesStatus(hitIndex) ?? false;
            Vector2 size = Data?.HitboxSize ?? new Vector2(960f, 240f);
            Vector2 offset = Data?.HitboxOffset ?? Vector2.Zero;
            if (!Owner.IsFacingRight) offset.X = -offset.X;
            foreach (Hurtbox hurtbox in TubmanKitStory.Query(
                         Owner, new RectangleShape2D { Size = size }, Owner.GlobalPosition + offset)) {
                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "tubman_freedom_line",
                    HitboxID = $"freedom_line_{hitIndex}",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.CinematicHitDamage(hitIndex) ?? 8f) * Owner.StorySpecialDamageMultiplier,
                    // The train carries its passengers along the line: only the
                    // final rush throws them, along her facing.
                    Knockback = finale ? Data?.KnockbackForce ?? new Vector2(6f, -4f) : Vector2.Zero,
                    HitstunDuration = finale ? UltimateActivationRules.FinaleHitstunSeconds : Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = carriesStatus ? Data?.AppliedStatus ?? FTT.Core.StatusType.None : FTT.Core.StatusType.None,
                    StatusDuration = carriesStatus ? Data?.StatusDuration ?? 0f : 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.6f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.3f
                });
                float dealt = hurtbox.TakeHit(hit);
                // D03h: Ultimate-origin damage earns no meter; Rally reclaim retained (D03g).
                Credit(in hit, dealt, hurtbox.GlobalPosition);
            }
        }
    }
}
