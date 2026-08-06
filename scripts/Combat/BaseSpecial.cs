using Godot;
using FTT.Characters;

namespace FTT.Combat {

    public enum AbilityPhase {
        Inactive,
        Startup,
        Active,
        Recovery,
        Cleanup
    }

    public abstract partial class BaseSpecial : Node {
        private const string PlaceholderProjectilePoolID = "story_projectile";
        private const string PlaceholderProjectileScenePath = "res://scenes/templates/StoryProjectileTemplate.tscn";
        private const string PlaceholderZonePoolID = "story_zone";
        private const string PlaceholderZoneScenePath = "res://scenes/templates/StoryZoneTemplate.tscn";
        [Export] public AbilityData Data;

        public AbilityPhase CurrentPhase { get; protected set; } = AbilityPhase.Inactive;
        public bool IsExecuting => CurrentPhase != AbilityPhase.Inactive;

        protected new PlayerController Owner;
        private int _phaseFramesRemaining;
        protected float PhaseTimer {
            get => _phaseFramesRemaining / 60.0f;
            set => _phaseFramesRemaining = Mathf.Max(0, Mathf.RoundToInt(value * 60.0f));
        }

        public override void _Ready() {
            Owner = GetParent<PlayerController>();
        }

        public bool TryExecute() {
            if (IsExecuting) return false;
            if (!Validate()) return false;

            CurrentPhase = AbilityPhase.Startup;
            OnStartup();
            return true;
        }

        protected virtual bool Validate() {
            return Owner != null && Data != null;
        }

        protected abstract void OnStartup();
        protected abstract void OnActive();
        protected abstract void OnRecovery();

        protected virtual void OnCleanup() {
            CurrentPhase = AbilityPhase.Inactive;
            if (Owner.IsOnFloor()) {
                Owner.TransitionTo(CharacterState.Idle);
            } else {
                Owner.TransitionTo(CharacterState.Airborne);
            }
        }

        public void AdvanceToActive() {
            CurrentPhase = AbilityPhase.Active;
            OnActive();
        }

        public void AdvanceToRecovery() {
            CurrentPhase = AbilityPhase.Recovery;
            OnRecovery();
        }

        public void AdvanceToCleanup() {
            CurrentPhase = AbilityPhase.Cleanup;
            OnCleanup();
        }

        public override void _PhysicsProcess(double delta) {
            if (!IsExecuting) return;
            _phaseFramesRemaining--;
            if (_phaseFramesRemaining <= 0) {
                switch (CurrentPhase) {
                    case AbilityPhase.Startup:
                        AdvanceToActive();
                        break;
                    case AbilityPhase.Active:
                        AdvanceToRecovery();
                        break;
                    case AbilityPhase.Recovery:
                        AdvanceToCleanup();
                        break;
                }
            }
        }

        protected PlaceholderProjectile SpawnPlaceholderProjectile(Vector2 position, float speed,
            bool movingRight, Color color, Vector2 size = default, float lifetime = 3f) {
            var proj = SpawnPooledPlaceholder<PlaceholderProjectile>(
                PlaceholderProjectilePoolID,
                PlaceholderProjectileScenePath,
                position,
                50);
            if (proj == null) return null;
            proj.Setup(Data?.BaseDamage ?? 10f, Data?.KnockbackForce ?? new Vector2(3, -2),
                speed, movingRight, Owner?.PlayerIndex ?? 0, color, size, lifetime, Owner, Data);
            return proj;
        }

        protected PlaceholderZone SpawnPlaceholderZone(Vector2 position, float damage,
            float lifetime, float tickInterval, Color color, float radius = 60f) {
            var zone = SpawnPooledPlaceholder<PlaceholderZone>(
                PlaceholderZonePoolID,
                PlaceholderZoneScenePath,
                position,
                24);
            if (zone == null) return null;
            zone.Setup(damage, lifetime, tickInterval, Owner?.PlayerIndex ?? 0, color, radius);
            return zone;
        }

        private T SpawnPooledPlaceholder<T>(string poolID, string scenePath, Vector2 position, int capacity)
            where T : FTT.Core.PooledNode {
            FTT.Core.PoolManager pools = FTT.Core.PoolManager.Instance;
            Node parent = Owner?.GetParent() ?? GetTree().CurrentScene;
            if (pools == null || parent == null) return null;
            if (!pools.IsRegistered(poolID)) {
                PackedScene scene = GD.Load<PackedScene>(scenePath);
                if (scene == null) {
                    GD.PushError($"Missing pooled placeholder scene: {scenePath}");
                    return null;
                }
                pools.RegisterPool(poolID, scene, 1, capacity, FTT.Core.PoolOverflowPolicy.Grow);
            }
            return pools.Spawn(poolID, position, parent) as T;
        }

        protected Hitbox GetOrCreateChildHitbox(string name) {
            var existing = GetNodeOrNull<Hitbox>(name);
            if (existing != null) return existing;
            var hb = new Hitbox();
            hb.Name = name;
            hb.AttackID = Data?.AbilityID ?? "";
            hb.HitboxID = name;
            hb.AttackClass = Data?.Slot == FTT.Core.AbilitySlot.Ultimate
                ? AttackClass.Ultimate
                : AttackClass.Special;
            hb.Damage = Data?.BaseDamage ?? 10f;
            hb.KnockbackForce = Data?.KnockbackForce ?? new Vector2(3, -2);
            hb.HitstunDuration = Data?.HitstunDuration ?? 0.2f;
            hb.AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None;
            hb.StatusDuration = Data?.StatusDuration ?? 0f;
            hb.StatusIntensity = Data?.StatusIntensity ?? 1f;
            hb.ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f;
            hb.ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.1f;
            hb.OwnerPlayerIndex = Owner?.PlayerIndex ?? 0;
            hb.SourcePlayer = Owner;
            hb.CollisionLayer = FTT.Core.CollisionLayers.HitboxLayerForFighterSlot(Owner?.PlayerIndex ?? 0);
            hb.CollisionMask = FTT.Core.CollisionLayers.HitboxMaskForFighterSlot(Owner?.PlayerIndex ?? 0);
            hb.Monitorable = true;
            var shape = new CollisionShape2D();
            var rect = new RectangleShape2D();
            rect.Size = Data?.HitboxSize ?? new Vector2(50, 50);
            shape.Shape = rect;
            hb.AddChild(shape);
            AddChild(hb);
            return hb;
        }
    }
}
