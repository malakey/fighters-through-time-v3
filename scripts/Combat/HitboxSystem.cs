using Godot;
using System;

namespace FTT.Combat {

    public partial class Hitbox : Area2D {
        [Export] public float Damage = 10f;
        [Export] public Vector2 KnockbackForce = new(3f, -2f);
        [Export] public float HitstunDuration = 0.2f;
        [Export] public bool IsActive;
        [Export] public int OwnerPlayerIndex = -1;

        public void Activate() {
            IsActive = true;
            Monitoring = true;
        }

        public void Deactivate() {
            IsActive = false;
            Monitoring = false;
        }

        public override void _Ready() {
            Deactivate();
            AreaEntered += OnAreaEntered;
        }

        private void OnAreaEntered(Area2D area) {
            if (!IsActive) return;
            if (area is Hurtbox hurtbox) {
                if (hurtbox.OwnerPlayerIndex == OwnerPlayerIndex) return;
                hurtbox.TakeHit(Damage, KnockbackForce, HitstunDuration, GlobalPosition);
            }
        }
    }

    public partial class Hurtbox : Area2D {
        [Export] public int OwnerPlayerIndex = -1;
        public event Action<float, Vector2, float, Vector2> OnHit;

        public void TakeHit(float damage, Vector2 knockback, float hitstun, Vector2 hitPosition) {
            OnHit?.Invoke(damage, knockback, hitstun, hitPosition);
        }
    }
}
