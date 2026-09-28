using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Ultimate — The Cosmological Constant: Einstein collapses an equation of
    /// light into a screen-clearing micro black hole ahead of him. Caught targets
    /// are dragged toward the singularity every frame (an impulse-free positional
    /// pull like the sandstorm vortex) while it lands the authored HitCount ticks
    /// of BaseDamage across the active window; the final tick is the explosive
    /// launch carrying the authored KnockbackForce toward the blast zones. All
    /// timing/damage structure comes from the authored AbilityData
    /// (StartupFrames/ActiveFrames/RecoveryFrames, BaseDamage, HitCount,
    /// DamageTickIntervalFrames, KnockbackForce). Hits use the Ultimate attack
    /// class, so they bypass block. Cinematic presentation (starfield, frozen
    /// enemies, equation scribble) is Package 8; this is the mechanics pass.
    /// </summary>
    public partial class EinsteinUltimate : BaseSpecial {

        public const float SingularityRadiusPixels = 400f;
        private const float ForwardOffsetPixels = 120f;
        private const float UpOffsetPixels = 40f;
        // Stronger than the sandstorm vortex's 180 px/s drag, befitting a black
        // hole; mirrors the Fighter sim's 0.08 units/frame singularity pull.
        private const float PullPixelsPerSecond = 288f;

        private UltimateMeter _meter;
        private Vector2 _singularityCenter;
        private float _tickInterval;
        private float _tickTimer;
        private int _hitsDone;

        private float DamagePerHit => Data?.BaseDamage ?? 15f;
        private int HitCount => Data?.HitCount > 0 ? Data.HitCount : 5;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            EmitCharacterVfx(
                "res://scenes/vfx/einstein/EinsteinCosmologicalConstantVfx.tscn",
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 120f : -120f, -70f));
            _hitsDone = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _singularityCenter = Owner.GlobalPosition + new Vector2(
                Owner.IsFacingRight ? ForwardOffsetPixels : -ForwardOffsetPixels,
                -UpOffsetPixels);
            _tickInterval = (Data?.DamageTickIntervalFrames ?? 18) / 60f;
            if (_tickInterval <= 0f) {
                _tickInterval = Mathf.Max(1, Data?.ActiveFrames ?? 90) / 60f / HitCount;
            }
            _tickTimer = _tickInterval;

            // Presentation only: damage, the pull, and the launch all run through
            // the hurtbox queries below so enemies participate alongside fighters.
            SpawnPlaceholderZone(
                _singularityCenter,
                0f,
                Data?.Lifetime > 0f ? Data.Lifetime : 1.5f,
                1f,
                new Color(0.25f, 0.1f, 0.5f),
                SingularityRadiusPixels * 0.25f);
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            // Guarantee the full authored hit total: any tick the frame-quantized
            // active window did not fire lands now, ending with the launch hit.
            while (_hitsDone < HitCount) DealSingularityHit();
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                PullTargetsTowardSingularity(dt);
                _tickTimer -= dt;
                while (_tickTimer <= 0f && _hitsDone < HitCount) {
                    _tickTimer += _tickInterval;
                    DealSingularityHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void PullTargetsTowardSingularity(float dt) {
            float step = PullPixelsPerSecond * dt;
            foreach (Hurtbox hurtbox in QueryTargetHurtboxes()) {
                CharacterBody2D body = FindTargetBody(hurtbox);
                if (body == null) continue;
                Vector2 toCenter = _singularityCenter - body.GlobalPosition;
                if (toCenter.Length() <= step) {
                    body.GlobalPosition = _singularityCenter;
                } else {
                    body.GlobalPosition += toCenter.Normalized() * step;
                }
            }
        }

        /// <summary>
        /// One multi-hit tick of the collapsing singularity. The final tick is
        /// the explosive launch: it carries the authored KnockbackForce and
        /// hitstun; earlier ticks are pure impulse-free damage so the pull keeps
        /// its grip.
        /// </summary>
        private void DealSingularityHit() {
            _hitsDone++;
            bool isLaunchHit = _hitsDone >= HitCount;
            foreach (Hurtbox hurtbox in QueryTargetHurtboxes()) {
                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "einstein_cosmological_constant",
                    HitboxID = isLaunchHit ? "singularity_launch" : "singularity_tick",
                    AttackClass = AttackClass.Ultimate,
                    Damage = DamagePerHit * Owner.StorySpecialDamageMultiplier,
                    Knockback = isLaunchHit
                        ? Data?.KnockbackForce ?? new Vector2(6, -4)
                        : Vector2.Zero,
                    HitstunDuration = isLaunchHit ? Data?.HitstunDuration ?? 0.5f : 0f,
                    HitOrigin = _singularityCenter,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration ?? 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.6f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.3f
                });
                float dealt = hurtbox.TakeHit(hit);
                // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its caster
                // ZERO damage-dealt meter, regardless of HP removed, target count or
                // when it lands. Direct-hit Rally reclaim is retained (D03g).
                Credit(in hit, dealt);
            }
        }

        private static CharacterBody2D FindTargetBody(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) return body;
                current = current.GetParent();
            }
            return null;
        }

        private System.Collections.Generic.List<Hurtbox> QueryTargetHurtboxes() {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = Owner?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = SingularityRadiusPixels },
                Transform = new Transform2D(0f, _singularityCenter),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is Hurtbox hurtbox
                    && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    results.Add(hurtbox);
                }
            }
            return results;
        }
    }
}
