using Godot;
using System;
using FTT.Core;

namespace FTT.Enemies {

    public enum EnemyAbilityPhase {
        Idle,
        Telegraph,
        Active,
        Recovery
    }

    /// <summary>
    /// Shared telegraph -> active -> recovery driver used by BOTH
    /// <see cref="EnemyController"/> elite abilities and every
    /// <see cref="BossController"/> attack. Frames advance once per physics tick
    /// (60 Hz); the caller applies <see cref="DashVelocity"/> to its body while the
    /// active phase of a ChargeDash runs. Story-only: nothing here is deterministic
    /// simulation state and none of it reaches scripts/FighterSim.
    /// </summary>
    public sealed class EnemyAbilityExecutor {
        public const string ProjectilePoolID = "enemy_projectile";
        public const string ProjectileScenePath = "res://scenes/enemies/EnemyProjectile.tscn";
        private const int ProjectilePoolWarmUp = 8;
        private const int ProjectilePoolCapacity = 40;

        private readonly Node2D _owner;
        private AnimatedSprite2D _sprite;
        private FTT.Combat.Hitbox _hitbox;
        private CollisionShape2D _hitboxShape;
        private Node2D _abilityOrigin;

        private bool _facingRight = true;
        private Vector2 _targetPosition;
        private Color _spriteBaseModulate = Colors.White;
        private bool _tintApplied;

        public EnemyAbilityExecutor(Node2D owner) {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Owning enemy/boss ID, echoed on presentation events.</summary>
        public string SourceID { get; set; } = "";

        /// <summary>Story difficulty damage scaling applied to every hitbox/projectile.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Seeded so tests and replays get repeatable spread/teleport rolls.</summary>
        public Random Rng { get; set; } = new(20260807);

        public EnemyAbilityPhase Phase { get; private set; } = EnemyAbilityPhase.Idle;
        public EnemyAbilityData ActiveAbility { get; private set; }
        public int FramesRemainingInPhase { get; private set; }
        public bool IsBusy => Phase != EnemyAbilityPhase.Idle;
        public bool IsTelegraphing => Phase == EnemyAbilityPhase.Telegraph;

        /// <summary>Horizontal velocity a ChargeDash wants applied during its active phase.</summary>
        public Vector2 DashVelocity { get; private set; }

        public float ShieldDamageReduction { get; private set; }
        public float ShieldSecondsRemaining { get; private set; }
        public bool HasActiveShield => ShieldSecondsRemaining > 0f && ShieldDamageReduction > 0f;

        /// <summary>Raised the frame an ability's active phase begins.</summary>
        public event Action<EnemyAbilityData> AbilityActivated;

        public void Bind(AnimatedSprite2D sprite, FTT.Combat.Hitbox hitbox, Node2D abilityOrigin) {
            _sprite = sprite;
            _hitbox = hitbox;
            _abilityOrigin = abilityOrigin;
            _spriteBaseModulate = sprite?.Modulate ?? Colors.White;
            _hitboxShape = hitbox?.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (_hitboxShape == null && hitbox != null) {
                Godot.Collections.Array<Node> children = hitbox.GetChildren();
                using var childrenLifetime = children.AsDisposable();
                foreach (Node child in children) {
                    if (child is CollisionShape2D found) { _hitboxShape = found; break; }
                }
            }
            // Scene sub-resources are shared between instances by default; a local
            // copy lets every enemy resize its own hitbox per ability.
            if (_hitboxShape?.Shape is RectangleShape2D shared) {
                _hitboxShape.Shape = (Shape2D)shared.Duplicate();
            } else if (_hitboxShape != null && _hitboxShape.Shape == null) {
                _hitboxShape.Shape = new RectangleShape2D { Size = new Vector2(48f, 48f) };
            }
        }

        /// <summary>Records the sprite tint the telegraph restores to.</summary>
        public void SetBaseModulate(Color modulate) {
            _spriteBaseModulate = modulate;
            if (!_tintApplied && _sprite != null) _sprite.Modulate = modulate;
        }

        public bool Begin(EnemyAbilityData ability, Vector2 targetPosition, bool facingRight) {
            if (ability == null) return false;
            ClearActiveEffects();
            ActiveAbility = ability;
            _facingRight = facingRight;
            _targetPosition = targetPosition;
            Phase = EnemyAbilityPhase.Telegraph;
            FramesRemainingInPhase = Math.Max(0, ability.TelegraphFrames);
            ApplyTelegraphTint();
            RaisePresentation(EnemyPresentationPhase.Telegraph);
            if (FramesRemainingInPhase <= 0) EnterActive();
            return true;
        }

        /// <summary>One 60 Hz step. Delta only drives the shield timer.</summary>
        public void Tick(float delta) {
            if (ShieldSecondsRemaining > 0f) {
                ShieldSecondsRemaining -= delta;
                if (ShieldSecondsRemaining <= 0f) {
                    ShieldSecondsRemaining = 0f;
                    ShieldDamageReduction = 0f;
                }
            }
            if (Phase == EnemyAbilityPhase.Idle) return;

            FramesRemainingInPhase--;
            if (FramesRemainingInPhase > 0) return;
            switch (Phase) {
                case EnemyAbilityPhase.Telegraph: EnterActive(); break;
                case EnemyAbilityPhase.Active: EnterRecovery(); break;
                default: EnterIdle(); break;
            }
        }

        /// <summary>Interruption during telegraph: skip the payload, keep the recovery cost.</summary>
        public void CancelIntoRecovery() {
            if (Phase == EnemyAbilityPhase.Idle) return;
            RestoreTint();
            _hitbox?.Deactivate();
            DashVelocity = Vector2.Zero;
            int recovery = Math.Max(1, ActiveAbility?.RecoveryFrames ?? 1);
            Phase = EnemyAbilityPhase.Recovery;
            FramesRemainingInPhase = recovery;
        }

        /// <summary>Hard stop, used by freeze/despawn/death.</summary>
        public void Cancel() {
            ClearActiveEffects();
            Phase = EnemyAbilityPhase.Idle;
            ActiveAbility = null;
            FramesRemainingInPhase = 0;
        }

        /// <summary>Full reset for pool reuse: also drops any active shield.</summary>
        public void Reset() {
            Cancel();
            ShieldDamageReduction = 0f;
            ShieldSecondsRemaining = 0f;
        }

        private void ClearActiveEffects() {
            RestoreTint();
            _hitbox?.Deactivate();
            DashVelocity = Vector2.Zero;
        }

        private void EnterActive() {
            RestoreTint();
            Phase = EnemyAbilityPhase.Active;
            FramesRemainingInPhase = ActiveAbility?.ResolvedActiveFrames ?? 1;
            ExecuteArchetype();
            RaisePresentation(EnemyPresentationPhase.Active);
            AbilityActivated?.Invoke(ActiveAbility);
        }

        private void EnterRecovery() {
            _hitbox?.Deactivate();
            DashVelocity = Vector2.Zero;
            int recovery = Math.Max(0, ActiveAbility?.RecoveryFrames ?? 0);
            RaisePresentation(EnemyPresentationPhase.Recovery);
            if (recovery <= 0) {
                EnterIdle();
                return;
            }
            Phase = EnemyAbilityPhase.Recovery;
            FramesRemainingInPhase = recovery;
        }

        private void EnterIdle() {
            Phase = EnemyAbilityPhase.Idle;
            ActiveAbility = null;
            FramesRemainingInPhase = 0;
            DashVelocity = Vector2.Zero;
        }

        // === Archetype execution ===

        private void ExecuteArchetype() {
            EnemyAbilityData ability = ActiveAbility;
            if (ability == null) return;
            switch (ability.Archetype) {
                case EnemyAbilityArchetype.MeleeStrike:
                    ActivateHitbox(ability, ability.HitboxSize, ability.HitboxOffset, mirrored: true);
                    break;
                case EnemyAbilityArchetype.ChargeDash:
                    DashVelocity = new Vector2((_facingRight ? 1f : -1f) * Mathf.Abs(ability.DashSpeed), 0f);
                    ActivateHitbox(ability, ability.HitboxSize, ability.HitboxOffset, mirrored: true);
                    break;
                case EnemyAbilityArchetype.AreaPulse:
                    float diameter = Mathf.Max(8f, ability.PulseRadius * 2f);
                    ActivateHitbox(ability, new Vector2(diameter, diameter),
                        new Vector2(0f, ability.HitboxOffset.Y), mirrored: false);
                    break;
                case EnemyAbilityArchetype.Projectile:
                    SpawnProjectiles(ability, lockVertical: false);
                    break;
                case EnemyAbilityArchetype.Shockwave:
                    SpawnProjectiles(ability, lockVertical: true);
                    break;
                case EnemyAbilityArchetype.ShieldBubble:
                    ShieldDamageReduction = Mathf.Clamp(ability.ShieldDamageReduction, 0f, 1f);
                    ShieldSecondsRemaining = Mathf.Max(0f, ability.ShieldDuration);
                    break;
                case EnemyAbilityArchetype.SummonMinions:
                    SummonMinions(ability);
                    break;
                case EnemyAbilityArchetype.Teleport:
                    Teleport(ability);
                    break;
            }
        }

        private void ActivateHitbox(EnemyAbilityData ability, Vector2 size, Vector2 offset, bool mirrored) {
            if (_hitbox == null) return;
            float direction = _facingRight ? 1f : -1f;
            float knockbackSign = ability.IsPullKnockback ? -direction : direction;

            _hitbox.AttackID = ability.AbilityID;
            _hitbox.HitboxID = "primary";
            _hitbox.AttackClass = ability.Archetype == EnemyAbilityArchetype.MeleeStrike
                ? FTT.Combat.AttackClass.Basic
                : FTT.Combat.AttackClass.Special;
            _hitbox.Damage = Mathf.Max(0f, ability.Damage) * Mathf.Max(0f, DamageMultiplier);
            _hitbox.KnockbackForce = new Vector2(
                knockbackSign * Mathf.Abs(ability.KnockbackForce.X),
                ability.KnockbackForce.Y);
            _hitbox.HitstunDuration = Mathf.Max(0f, ability.HitstunDuration);
            _hitbox.AppliedStatus = ability.AppliedStatus;
            _hitbox.StatusDuration = Mathf.Max(0f, ability.StatusDuration);
            _hitbox.StatusIntensity = ability.StatusIntensity <= 0f ? 1f : ability.StatusIntensity;
            _hitbox.OwnerPlayerIndex = -1;
            _hitbox.CollisionLayer = CollisionLayers.EnemyHitbox;
            _hitbox.CollisionMask = CollisionLayers.EnemyHitboxMask;
            _hitbox.Monitorable = true;
            _hitbox.Position = new Vector2(mirrored ? offset.X * direction : offset.X, offset.Y);
            if (_hitboxShape?.Shape is RectangleShape2D rect && size.X > 0f && size.Y > 0f) rect.Size = size;
            _hitbox.Activate();
        }

        private void SpawnProjectiles(EnemyAbilityData ability, bool lockVertical) {
            if (!EnsureProjectilePool()) return;
            Vector2 origin = _abilityOrigin != null
                ? _abilityOrigin.GlobalPosition
                : _owner.GlobalPosition + new Vector2((_facingRight ? 1f : -1f) * 24f, -40f);

            Vector2 toTarget = _targetPosition - origin;
            Vector2 baseDirection = toTarget.LengthSquared() > 1f
                ? toTarget.Normalized()
                : new Vector2(_facingRight ? 1f : -1f, 0f);
            if (lockVertical) {
                baseDirection = new Vector2(baseDirection.X >= 0f ? 1f : -1f, 0f);
            }

            int count = Math.Max(1, ability.ProjectileCount);
            float spread = Mathf.DegToRad(Mathf.Max(0f, ability.ProjectileSpreadDegrees));
            float damage = Mathf.Max(0f, ability.Damage) * Mathf.Max(0f, DamageMultiplier);
            Node parent = _owner.GetParent();

            for (int index = 0; index < count; index++) {
                float fraction = count == 1 ? 0f : index / (count - 1f) - 0.5f;
                Vector2 direction = spread > 0f ? baseDirection.Rotated(spread * fraction) : baseDirection;
                var projectile = PoolManager.Instance.Spawn(ProjectilePoolID, origin, parent) as EnemyProjectile;
                projectile?.Setup(ability, direction * Mathf.Max(1f, ability.ProjectileSpeed), damage, SourceID, lockVertical);
            }
        }

        private void SummonMinions(EnemyAbilityData ability) {
            if (string.IsNullOrWhiteSpace(ability.SummonEnemyID)) return;
            Node parent = _owner.GetParent();
            if (parent == null) return;
            int count = Math.Max(1, ability.SummonCount);
            for (int index = 0; index < count; index++) {
                float offsetX = (index % 2 == 0 ? -1f : 1f) * (96f + 48f * (index / 2));
                EnemyFactory.Spawn(ability.SummonEnemyID, parent, _owner.GlobalPosition + new Vector2(offsetX, 0f));
            }
        }

        private void Teleport(EnemyAbilityData ability) {
            float span = Mathf.Max(0f, ability.TeleportRangeMax - ability.TeleportRangeMin);
            float distance = ability.TeleportRangeMin + (float)Rng.NextDouble() * span;
            // Reappear on the far side of the target so the reposition reads clearly.
            float side = _owner.GlobalPosition.X <= _targetPosition.X ? 1f : -1f;
            _owner.GlobalPosition = new Vector2(
                _targetPosition.X + side * Mathf.Max(1f, distance),
                _owner.GlobalPosition.Y);
        }

        private static bool EnsureProjectilePool() {
            PoolManager pools = PoolManager.Instance;
            if (pools == null) return false;
            if (pools.IsRegistered(ProjectilePoolID)) return true;
            PackedScene scene = GD.Load<PackedScene>(ProjectileScenePath);
            if (scene == null) return false;
            pools.RegisterPool(ProjectilePoolID, scene, ProjectilePoolWarmUp, ProjectilePoolCapacity,
                PoolOverflowPolicy.RecycleOldest);
            return true;
        }

        // === Presentation ===

        private void ApplyTelegraphTint() {
            if (_sprite == null || ActiveAbility == null) return;
            _spriteBaseModulate = _tintApplied ? _spriteBaseModulate : _sprite.Modulate;
            _sprite.Modulate = ActiveAbility.TelegraphTint;
            _tintApplied = true;
        }

        private void RestoreTint() {
            if (!_tintApplied) return;
            if (_sprite != null) _sprite.Modulate = _spriteBaseModulate;
            _tintApplied = false;
        }

        private void RaisePresentation(EnemyPresentationPhase phase) {
            EventBus.Instance?.RaiseEnemyPresentation(new EnemyPresentationPayload {
                SourceID = SourceID ?? "",
                AbilityID = ActiveAbility?.AbilityID ?? "",
                PresentationEventID = ActiveAbility?.PresentationEventID ?? "",
                Phase = phase,
                Position = _owner.GlobalPosition
            });
        }
    }
}
