using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 2 — Relativity Rift: a localized distortion field that inflicts
    /// TimeDilation (-50% enemy movement/jump/animation speed) and continuous chip
    /// damage (authored: 3 per 0.5 s tick over 3 s). If Einstein stands inside
    /// his own rift he gains +25% movement speed, mirroring the Fighter-sim zone.
    /// All tuning comes from the authored AbilityData resource.
    ///
    /// <para><b>Package 13 W7a (E03).</b> The rift is thrown up to 5 units ahead
    /// (stopping short of a wall), ground-snapped when cast grounded and left
    /// mid-air when cast airborne, with a 1.5-unit radius, stationary for 3 s.
    /// Time Dilation is refreshed every frame a target stands inside and lingers
    /// the authored <c>StatusDuration</c> (0.5 s) after it leaves; re-entry
    /// refreshes without stacking (the shared stronger-wins rule).</para>
    ///
    /// <para><b>E01 Rift Collapse.</b> An E=mc² burst inside this active rift
    /// ends it at once and pulls every eligible opponent inside to its centre
    /// over 6 frames, then launches them upward — a 0-damage, Special-class hit
    /// of the same E=mc² execution: blockers, invulnerable targets and immovable
    /// bosses are not caught, it spends no block charge of its own and earns no
    /// meter or Rally.</para>
    /// </summary>
    public partial class EinsteinRelativityRift : BaseSpecial {

        private const float PixelsPerUnit = KitMotionRules.StoryPixelsPerUnit;
        private static readonly float RiftRadius = KitMotionRules.RelativityRiftRadiusUnits * PixelsPerUnit;
        private static readonly float ThrowDistance = KitMotionRules.RelativityRiftThrowUnits * PixelsPerUnit;
        private const float OwnerSpeedMultiplier = 1.25f;
        /// <summary>Linger fallback when the resource authors no status duration (0.5 s).</summary>
        private const float DefaultLingerSeconds = KitMotionRules.RelativityRiftLingerFrames / 60f;
        /// <summary>The wall probe runs this far above the feet so a floor lip never stops the throw.</summary>
        private const float WallProbeHeight = 30f;
        private const float GroundProbeDepth = 400f;
        /// <summary>
        /// E01: the hitstun of the collapse's 0-damage launch, in frames (0.5 s).
        /// Named for D4's first-Special allowance model
        /// (<c>StoryFirstSpecialTaxTests</c>); the value is unchanged.
        /// </summary>
        public const int CollapseLaunchHitstunFrames = 30;

        /// <summary>
        /// Lingering-kit pass: the owner's landed basic string finisher (hitbox <c>combo_3</c>) on a
        /// target inside this rift collapses it, like an E=mc2 burst does.
        /// </summary>
        public const string FinisherHitboxID = "combo_3";

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            EmitCharacterVfx(
                "res://scenes/vfx/einstein/EinsteinRelativityRiftVfx.tscn",
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 72f : -72f, -26f));
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            SpawnRift();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        /// <summary>
        /// Event Horizon (Story-only Resonance major, V7.6): hostile
        /// projectiles crossing the rift are slowed to half speed. The rift's
        /// live radius and centre, so the sweep below can find them.
        /// </summary>
        public const string EventHorizonPerkKey = EinsteinEmc2Blast.EventHorizonPerkKey;

        /// <summary>V7.6 Event Horizon: speed a projectile keeps while inside the rift.</summary>
        public const float EventHorizonProjectileSpeedScale = 0.5f;

        private Vector2 _riftCenter;
        private float _riftRadius;
        private float _riftLifetime;
        private PlaceholderZone _riftZone;
        private readonly System.Collections.Generic.List<CharacterBody2D> _collapseTargets = new();
        private readonly System.Collections.Generic.List<Hurtbox> _collapseHurtboxes = new();
        private int _collapseFramesRemaining;
        /// <summary>
        /// D4 (2026-10-11): the <c>AttackID</c> of the E=mc2 execution whose burst
        /// collapsed the rift, carried on the collapse's launch so it rides that
        /// execution's first-Special slot (design E01: "a 0-damage, Special-class
        /// hit of the same E=mc2 execution"). Null = the rift's own ID. Set by
        /// <see cref="TryCollapse"/>, cleared once the caught targets launch.
        /// </summary>
        private string _collapseSourceAttackID;
        /// <summary>
        /// Lingering-kit pass: the cast that burst the rift (E=mc2) and the execution serial it burst
        /// under, so the collapse landing can arm that cast's D3 flag. Null for a finisher collapse.
        /// </summary>
        private BaseSpecial _collapseSourceCast;
        private int _collapseSourceSerial;
        private bool _subscribed;

        /// <summary>The deployed rift's effective radius after Story minors. Test seam.</summary>
        public float ActiveRiftRadiusPixels => _riftRadius;

        /// <summary>The deployed rift's centre (E03 placement). Test seam.</summary>
        public Vector2 ActiveRiftCenter => _riftCenter;

        /// <summary>True while a deployed rift is still open. Test seam.</summary>
        public bool RiftActive => _riftLifetime > 0f;

        /// <summary>True while an E01 collapse is still pulling its caught targets. Test seam.</summary>
        public bool IsCollapsing => _collapseFramesRemaining > 0;

        public override void _PhysicsProcess(double delta) {
            // F5: the cast clock (base) holds through a caster freeze; the open
            // rift and its collapse are world objects and keep running.
            base._PhysicsProcess(delta);
            if (_collapseFramesRemaining > 0) {
                AdvanceCollapse();
                return;
            }
            if (_riftLifetime <= 0f) return;
            _riftLifetime -= (float)delta;
            if (_riftLifetime <= 0f) Unsubscribe();
            RefreshLinger();
            SlowCrossingProjectiles();
        }

        /// <summary>
        /// Event Horizon's second clause. Halves the velocity of every hostile
        /// projectile inside the rift, each frame it remains inside, and
        /// restores nothing on exit - the projectile simply keeps whatever
        /// speed it left with, which is the design's "slowed to half speed"
        /// without a per-shot bookkeeping record. Never touches beams, zones or
        /// hazards: only pooled enemy projectiles join the group.
        /// </summary>
        private void SlowCrossingProjectiles() {
            if (Owner == null || !Owner.HasStoryPerk(EventHorizonPerkKey) || !IsInsideTree()) return;
            Godot.Collections.Array<Node> shots =
                GetTree().GetNodesInGroup(FTT.Enemies.EnemyProjectile.GroupName);
            using var lifetime = shots.AsDisposable();
            foreach (Node node in shots) {
                if (node is not FTT.Enemies.EnemyProjectile shot || !shot.IsInsideTree()) continue;
                if (shot.GlobalPosition.DistanceTo(_riftCenter) > _riftRadius) continue;
                shot.ScaleVelocity(EventHorizonProjectileSpeedScale);
            }
        }

        private void SpawnRift() {
            if (Owner == null) return;
            float tickInterval = (Data?.DamageTickIntervalFrames ?? 30) / 60f;
            if (tickInterval <= 0f) tickInterval = 0.5f;
            _riftRadius = RiftRadius * Owner.StoryZoneRadiusMultiplier
                * Owner.StoryScoped("AbilityRange", "relativity_rift");
            _riftCenter = ResolvePlacement(_riftRadius);
            _riftLifetime = Data?.Lifetime > 0f ? Data.Lifetime : 3f;
            _collapseFramesRemaining = 0;
            _collapseSourceAttackID = null;
            _collapseSourceCast = null;
            Subscribe();
            _riftZone = SpawnPlaceholderZone(
                _riftCenter,
                Data?.BaseDamage ?? 1.5f,
                _riftLifetime,
                tickInterval,
                new Color(0.4f, 0.3f, 0.9f),
                // Package 11 A4: V7.6 re-scopes "Minor Rift Range" from the
                // character-wide ZoneRadius lane onto
                // AbilityRange(relativity_rift). Neutral 1.0 outside Story.
                _riftRadius,
                Data?.AppliedStatus ?? FTT.Core.StatusType.TimeDilation,
                LingerSeconds,
                Data?.StatusIntensity ?? 1f,
                OwnerSpeedMultiplier);
        }

        private float LingerSeconds => Data?.StatusDuration > 0f ? Data.StatusDuration : DefaultLingerSeconds;

        /// <summary>
        /// E03 placement: up to 5 units ahead, stopping short of a wall; snapped
        /// onto the surface below when Einstein is grounded, mid-air otherwise.
        /// </summary>
        private Vector2 ResolvePlacement(float radius) {
            Vector2 origin = Owner.GlobalPosition;
            float direction = Owner.IsFacingRight ? 1f : -1f;
            float distance = ThrowDistance;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space != null) {
                Vector2 probeFrom = origin + new Vector2(0f, -WallProbeHeight);
                var wallQuery = PhysicsRayQueryParameters2D.Create(
                    probeFrom, probeFrom + new Vector2(direction * ThrowDistance, 0f), CollisionLayers.Environment);
                using Godot.Collections.Dictionary wall = space.IntersectRay(wallQuery);
                if (wall != null && wall.Count > 0) {
                    distance = Mathf.Max(0f, Mathf.Abs(wall["position"].AsVector2().X - origin.X) - 1f);
                }
            }
            Vector2 center = origin + new Vector2(direction * distance, 0f);
            if (space != null && Owner.IsOnFloor()) {
                Vector2 groundFrom = center + new Vector2(0f, -WallProbeHeight);
                var groundQuery = PhysicsRayQueryParameters2D.Create(
                    groundFrom, groundFrom + new Vector2(0f, GroundProbeDepth),
                    CollisionLayers.Environment | CollisionLayers.OneWayPlatform);
                using Godot.Collections.Dictionary ground = space.IntersectRay(groundQuery);
                if (ground != null && ground.Count > 0) center.Y = ground["position"].AsVector2().Y;
            }
            return center;
        }

        /// <summary>
        /// E03 linger: every frame, each opponent inside the rift is (re)given its
        /// Time Dilation for the authored linger, so the slow lasts exactly that
        /// long after it leaves; re-entry refreshes, never stacks.
        /// </summary>
        private void RefreshLinger() {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            FTT.Core.StatusType status = Data?.AppliedStatus ?? FTT.Core.StatusType.TimeDilation;
            if (status == FTT.Core.StatusType.None) return;
            float intensity = Data?.StatusIntensity ?? 1f;
            foreach (Hurtbox hurtbox in QueryOpponentsInRift(_riftRadius)) {
                LingeringZoneStatus.TryApply(
                    LingeringZoneStatus.FindStatusNode(hurtbox.GetParent()), status, LingerSeconds, intensity);
            }
        }

        /// <summary>
        /// E01 trigger, called by E=mc²'s burst. True when the burst point lies
        /// inside this rift while it is open: the rift ends at once (ticks and the
        /// speed buff with it) and the caught opponents begin their 6-frame pull.
        /// <paramref name="sourceAttackID"/> is the collapsing E=mc² execution's
        /// ability ID: the collapse's launch carries it as its <c>AttackID</c>, so
        /// a mob's stun chain counts the collapse as part of that E=mc² Special
        /// (D4). Nothing else reads the collapse's <c>AttackID</c>: it is a
        /// 0-damage tick, so S2, meter, Rally and the landed-hit report never see it.
        /// </summary>
        public bool TryCollapse(Vector2 burstPoint, string sourceAttackID = null,
                BaseSpecial sourceCast = null, int sourceSerial = 0) {
            if (!RiftActive || _collapseFramesRemaining > 0) return false;
            if (burstPoint.DistanceTo(_riftCenter) > _riftRadius) return false;
            Unsubscribe();
            _collapseSourceAttackID = sourceAttackID;
            _collapseSourceCast = sourceCast;
            _collapseSourceSerial = sourceSerial;
            _collapseTargets.Clear();
            _collapseHurtboxes.Clear();
            foreach (Hurtbox hurtbox in QueryOpponentsInRift(_riftRadius)) {
                CharacterBody2D body = FindBody(hurtbox);
                if (body == null || _collapseTargets.Contains(body) || !CanBeCaught(body)) continue;
                _collapseTargets.Add(body);
                _collapseHurtboxes.Add(hurtbox);
            }
            _riftLifetime = 0f;
            if (_riftZone != null && IsInstanceValid(_riftZone)) _riftZone.ReturnToPool();
            _riftZone = null;
            _collapseFramesRemaining = KitMotionRules.RiftCollapsePullFrames;
            return true;
        }

        private void AdvanceCollapse() {
            float remaining = _collapseFramesRemaining;
            for (int index = 0; index < _collapseTargets.Count; index++) {
                CharacterBody2D body = _collapseTargets[index];
                if (body == null || !IsInstanceValid(body)) continue;
                body.GlobalPosition += (_riftCenter - body.GlobalPosition) / remaining;
                body.Velocity = Vector2.Zero;
            }
            _collapseFramesRemaining--;
            if (_collapseFramesRemaining > 0) return;
            for (int index = 0; index < _collapseHurtboxes.Count; index++) {
                Hurtbox hurtbox = _collapseHurtboxes[index];
                if (hurtbox == null || !IsInstanceValid(hurtbox)) continue;
                LaunchCaughtTarget(hurtbox);
            }
            _collapseTargets.Clear();
            _collapseHurtboxes.Clear();
            _collapseSourceAttackID = null;
            _collapseSourceCast = null;
            // Lingering-kit pass: Momentum Detonation once per collapse, never per body.
            if (_collapsePaid && Owner != null && IsInstanceValid(Owner)) {
                Owner.PayLingeringMomentum(LingeringMomentumEvent.Detonation);
            }
            _collapsePaid = false;
        }

        private bool _collapsePaid;

        private void Subscribe() {
            if (_subscribed || Owner == null) return;
            Owner.OwnerHitLanded += OnOwnerHitLanded;
            _subscribed = true;
        }

        private void Unsubscribe() {
            if (!_subscribed) return;
            _subscribed = false;
            if (Owner != null && IsInstanceValid(Owner)) Owner.OwnerHitLanded -= OnOwnerHitLanded;
        }

        public override void _ExitTree() {
            Unsubscribe();
            base._ExitTree();
        }

        private void OnOwnerHitLanded(OwnerHitLandedInfo info) {
            if (!RiftActive || _collapseFramesRemaining > 0) return;
            if (!info.Landed || info.Payload.HitboxID != FinisherHitboxID) return;
            if (info.Target == null || !IsInstanceValid(info.Target)) return;
            Vector2 point = info.Target.GlobalPosition;
            if (point.DistanceTo(_riftCenter) > _riftRadius) return;
            // Raised from a Hitbox, the seam runs inside the physics callback and the collapse
            // queries the space, so hop off the flush; a direct call stays synchronous.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) CallDeferred(nameof(CollapseFromFinisher), point);
            else CollapseFromFinisher(point);
        }

        private void CollapseFromFinisher(Vector2 point) => TryCollapse(point);

        private void LaunchCaughtTarget(Hurtbox hurtbox) {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            // A target that raised the stance mid-pull is not launched: the
            // collapse never spends a block charge of its own.
            if (FindBody(hurtbox) is PlayerController player && player.CurrentState == CharacterState.Blocking) return;
            HitPayload hit = Stamp(new HitPayload {
                AttackerIndex = Owner.PlayerIndex,
                // D4: the collapse belongs to the E=mc² execution that burst it (E01).
                AttackID = _collapseSourceAttackID ?? Data?.AbilityID ?? "einstein_relativity_rift",
                HitboxID = "rift_collapse",
                AttackClass = AttackClass.Special,
                // Lingering-kit pass: the collapse hurts (8, through the Story damage composition).
                Damage = Owner.StoryAbilityDamage(LingeringKitRules.RiftCollapseDamage, Data?.AbilityID),
                Knockback = new Vector2(0f, -KitMotionRules.RiftCollapseLaunchUnits),
                HitstunDuration = CollapseLaunchHitstunFrames / 60f,
                HitOrigin = _riftCenter,
                AttackerFacingRight = Owner.IsFacingRight,
                AppliedStatus = FTT.Core.StatusType.None,
                StatusDuration = 0f,
                StatusIntensity = 1f,
                ScreenShakeIntensity = 0.2f,
                ScreenShakeDuration = 0.1f
            });
            hit.Launches = true;
            hit.CountsAsCombo = true;
            hit.SourceExecutionSerial = _collapseSourceSerial;
            float dealt = hurtbox.TakeHit(hit);
            Credit(hurtbox, in hit, dealt, hurtbox.GlobalPosition);
            if (dealt > 0f && hurtbox.DidLand(hit.ContactId)) {
                _collapsePaid = true;
                // D3: a collapse landing inside the E=mc2 cast arms that cast's recovery cancel.
                if (_collapseSourceCast != null && IsInstanceValid(_collapseSourceCast)) {
                    HitPayload landed = hit;
                    landed.ContactLanded = true;
                    _collapseSourceCast.ConfirmAbilityHit(in landed, dealt, hurtbox.GlobalPosition);
                }
            }
            // The player damage path stops at a zero-damage hit (enemies still
            // take its knockback and stun), so a caught PlayerController — the
            // Level 13 Mirror, a sparring partner — gets the launch directly.
            // CM-FIXA-STORY (2.4 / Q2): the launch is this hit's knockback and
            // stun, so it reads the same armour ResolveIncomingHit reads for every
            // other hit — an Ultimate's startup armour, an armoured cast or a
            // Story hyper-armour window denies both, and the cast is not
            // interrupted. It is read at launch, not at the catch, because an
            // Ultimate accepted during the six-frame pull is armoured too.
            if (dealt <= 0f && FindBody(hurtbox) is PlayerController caughtPlayer && IsInstanceValid(caughtPlayer)
                && caughtPlayer.CurrentState != CharacterState.Dead
                && !caughtPlayer.HasActiveHyperArmorAgainst(hit.AttackClass)) {
                caughtPlayer.Velocity = hit.Knockback * 60f;
                caughtPlayer.ApplyStun(hit.HitstunDuration);
            }
        }

        private static bool CanBeCaught(CharacterBody2D body) {
            if (body is PlayerController player) {
                return player.CurrentState is not (CharacterState.Blocking or CharacterState.Dead
                    or CharacterState.Grabbing or CharacterState.Thrown)
                    && !player.IsRollInvulnerable
                    && !player.IsPostRewindInvulnerable
                    && !player.IsDefyProtected;
            }
            if (body is FTT.Enemies.BossController) return false; // immovable bosses
            return true;
        }

        private System.Collections.Generic.List<Hurtbox> QueryOpponentsInRift(float radius) {
            var found = new System.Collections.Generic.List<Hurtbox>();
            var space = Owner?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return found;
            uint statusMask = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = radius },
                Transform = new Transform2D(0f, _riftCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = statusMask
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                found.Add(hurtbox);
            }
            return found;
        }

        private static CharacterBody2D FindBody(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) return body;
                current = current.GetParent();
            }
            return null;
        }
    }
}
