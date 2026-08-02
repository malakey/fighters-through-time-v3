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
        [Export] public AbilityData Data;

        public AbilityPhase CurrentPhase { get; protected set; } = AbilityPhase.Inactive;
        public bool IsExecuting => CurrentPhase != AbilityPhase.Inactive;

        protected new PlayerController Owner;
        protected float PhaseTimer;

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
            PhaseTimer -= (float)delta;
            if (PhaseTimer <= 0) {
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
            var proj = new PlaceholderProjectile();
            proj.Setup(Data?.BaseDamage ?? 10f, Data?.KnockbackForce ?? new Vector2(3, -2),
                speed, movingRight, Owner?.PlayerIndex ?? 0, color, size, lifetime);
            proj.GlobalPosition = position;
            Owner?.GetParent()?.AddChild(proj);
            return proj;
        }

        protected PlaceholderZone SpawnPlaceholderZone(Vector2 position, float damage,
            float lifetime, float tickInterval, Color color, float radius = 60f) {
            var zone = new PlaceholderZone();
            zone.Setup(damage, lifetime, tickInterval, Owner?.PlayerIndex ?? 0, color, radius);
            zone.GlobalPosition = position;
            Owner?.GetParent()?.AddChild(zone);
            return zone;
        }

        protected Hitbox GetOrCreateChildHitbox(string name) {
            var existing = GetNodeOrNull<Hitbox>(name);
            if (existing != null) return existing;
            var hb = new Hitbox();
            hb.Name = name;
            hb.Damage = Data?.BaseDamage ?? 10f;
            hb.KnockbackForce = Data?.KnockbackForce ?? new Vector2(3, -2);
            hb.HitstunDuration = 0.2f;
            hb.OwnerPlayerIndex = Owner?.PlayerIndex ?? 0;
            hb.CollisionLayer = 4;
            hb.CollisionMask = 8;
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
