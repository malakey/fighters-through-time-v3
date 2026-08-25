using Godot;
using FTT.Combat;
using FTT.Core;

namespace FTT.Enemies {

    /// <summary>
    /// Pooled Story-side enemy/boss projectile. Always joins the
    /// <c>enemy_projectile</c> group so <see cref="FTT.Environment.ChronalRewindManager"/>
    /// can clear live shots when a rewind starts. Placeholder visuals are built in
    /// code; production art replaces them by scene reassignment.
    /// </summary>
    public partial class EnemyProjectile : PooledNode, IPoolable, FTT.Environment.IStoryRewindSimulation {
        public const string GroupName = "enemy_projectile";

        private FTT.Combat.Hitbox _hitbox;
        private ColorRect _visual;
        private AnimatedSprite2D _authoredVisual;
        private CollisionShape2D _shape;

        private Vector2 _velocity;
        private float _gravity;
        private float _lifetime;
        private bool _pierces;
        private bool _lockVertical;
        private float _lockedY;
        private bool _frozen;

        /// <summary>Owning enemy/boss ID, carried for presentation and debugging.</summary>
        public string SourceID { get; private set; } = "";
        public Vector2 Velocity => _velocity;
        public float LifetimeRemaining => _lifetime;
        public bool PiercesTargets => _pierces;

        public override void _Ready() {
            AddToGroup(GroupName);
            AddToGroup("projectile");
            EnsureNodes();
        }

        /// <summary>Configures one shot from authored ability data.</summary>
        public void Setup(
            EnemyAbilityData ability,
            Vector2 velocity,
            float scaledDamage,
            string sourceID,
            bool lockVertical = false,
            bool guardCrush = false,
            bool unblockable = false) {
            EnsureNodes();
            SourceID = sourceID ?? "";
            _velocity = velocity;
            _gravity = ability?.ProjectileGravity ?? 0f;
            _lifetime = Mathf.Max(0.05f, ability?.ProjectileLifetime ?? 2f);
            _pierces = ability?.PiercesTargets ?? false;
            _lockVertical = lockVertical;
            _lockedY = GlobalPosition.Y;
            _frozen = false;

            Vector2 size = ability?.HitboxSize ?? new Vector2(24f, 12f);
            if (size.X <= 0f || size.Y <= 0f) size = new Vector2(24f, 12f);
            _visual.Size = size;
            _visual.Position = -size / 2f;
            _visual.Color = ability?.TelegraphTint ?? new Color(0.95f, 0.4f, 0.2f);
            bool hasAuthoredVisual = EnemyAbilityVisualLibrary.Apply(
                _authoredVisual, ability?.PresentationEventID, 0.38f, velocity.X < 0f);
            _visual.Visible = !hasAuthoredVisual;
            if (_shape.Shape is RectangleShape2D rect) rect.Size = size;

            // Travel direction owns the knockback sign; a pull ability (negative
            // authored X) drags the target back toward the caster instead.
            float travelSign = _velocity.X >= 0f ? 1f : -1f;
            float knockbackSign = (ability?.IsPullKnockback ?? false) ? -travelSign : travelSign;

            _hitbox.AttackID = ability?.AbilityID ?? "enemy_projectile";
            _hitbox.HitboxID = "projectile";
            // V7.2 classification: mob fire is ambient pressure — Basic-class
            // against the block (1 charge, never a shatter). Guard-Crush and
            // boss-only unblockables are explicit flags.
            _hitbox.AttackClass = FTT.Combat.AttackClass.Basic;
            _hitbox.BlockChargeCost = guardCrush ? 2 : 0;
            _hitbox.Unblockable = unblockable;
            _hitbox.Damage = Mathf.Max(0f, scaledDamage);
            _hitbox.KnockbackForce = new Vector2(
                knockbackSign * Mathf.Abs(ability?.KnockbackForce.X ?? 2f),
                ability?.KnockbackForce.Y ?? -1f);
            _hitbox.HitstunDuration = Mathf.Max(0f, ability?.HitstunDuration ?? 0.15f);
            _hitbox.AppliedStatus = ability?.AppliedStatus ?? StatusType.None;
            _hitbox.StatusDuration = Mathf.Max(0f, ability?.StatusDuration ?? 0f);
            _hitbox.StatusIntensity = (ability?.StatusIntensity ?? 1f) <= 0f ? 1f : ability.StatusIntensity;
            _hitbox.OwnerPlayerIndex = -1;
            _hitbox.SourcePlayer = null;
            _hitbox.CollisionLayer = CollisionLayers.Projectile;
            // The centralized projectile mask (audit Low: the hand-rolled mask
            // dropped Environment, letting ranged mobs shoot through walls). The
            // EnemyHurtbox bit is harmless: Hitbox skips hurtboxes sharing the -1
            // owner index, so enemy shots still never damage enemies.
            _hitbox.CollisionMask = CollisionLayers.ProjectileMask;
            _hitbox.Monitorable = true;
            _hitbox.Activate();
        }

        private void EnsureNodes() {
            if (_hitbox != null) return;
            _visual = new ColorRect {
                Name = "Visual",
                Size = new Vector2(24f, 12f),
                Position = new Vector2(-12f, -6f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_visual);

            _authoredVisual = new AnimatedSprite2D {
                Name = "AuthoredVisual",
                Centered = true,
                Visible = false
            };
            AddChild(_authoredVisual);

            _hitbox = new FTT.Combat.Hitbox { Name = "Hitbox" };
            _shape = new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(24f, 12f) }
            };
            _hitbox.AddChild(_shape);
            AddChild(_hitbox);
            _hitbox.HitConfirmed += OnHitConfirmed;
            _hitbox.BodyEntered += OnBodyEnteredSignal;
        }

        private void OnHitConfirmed(FTT.Combat.HitPayload payload, float damageApplied) {
            if (_pierces) return;
            // The release is deferred past the physics flush this handler runs
            // in; stop the hitbox now so no second target is hit meanwhile.
            _hitbox?.Deactivate();
            ReturnToPool();
        }

        // Signal wrapper: engine BodyEntered arrives mid-flush and must run
        // guarded; tests call HandleBodyContact directly and stay synchronous.
        private void OnBodyEnteredSignal(Node2D body) {
            using var scope = PhysicsCallbackGuard.Enter();
            HandleBodyContact(body);
        }

        /// <summary>
        /// Terrain contact: an enemy shot despawns on level geometry instead of
        /// travelling through walls (audit Low; distance-only aggro made ranged
        /// mobs hit through room dividers). The hitbox's ProjectileMask includes
        /// the Environment layer, so static level bodies report here; any body on
        /// another layer (a PersistentObject construct, say) is ignored — hurtbox
        /// hits keep their own confirmed-hit path. Even piercing shots stop at a
        /// wall: piercing is about targets, not terrain.
        /// </summary>
        public void HandleBodyContact(Node2D body) {
            if (body is not CollisionObject2D collider) return;
            if ((collider.CollisionLayer & CollisionLayers.Environment) == 0) return;
            _hitbox?.Deactivate();
            ReturnToPool();
        }

        public override void _PhysicsProcess(double delta) {
            if (_frozen) return;
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0f) {
                ReturnToPool();
                return;
            }
            if (_gravity != 0f) _velocity = new Vector2(_velocity.X, _velocity.Y + _gravity * dt);
            Vector2 next = GlobalPosition + _velocity * dt;
            if (_lockVertical) next.Y = _lockedY;
            GlobalPosition = next;
        }

        public void OnSpawn() {
            EnsureNodes();
            _frozen = false;
            _lifetime = 0f;
            _velocity = Vector2.Zero;
            Visible = true;
            SetPhysicsProcess(true);
            if (!IsInGroup(GroupName)) AddToGroup(GroupName);
        }

        public void OnDespawn() {
            _hitbox?.Deactivate();
            _velocity = Vector2.Zero;
            _gravity = 0f;
            _lifetime = 0f;
            _pierces = false;
            _lockVertical = false;
            _lockedY = 0f;
            _frozen = false;
            SourceID = "";
            Rotation = 0f;
            Scale = Vector2.One;
            Modulate = Colors.White;
            _visual.Visible = true;
            if (_authoredVisual != null) {
                _authoredVisual.Stop();
                _authoredVisual.Visible = false;
                _authoredVisual.SpriteFrames = null;
            }
            SetPhysicsProcess(false);
        }

        public void SetStoryRewindFrozen(bool frozen) => _frozen = frozen;
    }
}
