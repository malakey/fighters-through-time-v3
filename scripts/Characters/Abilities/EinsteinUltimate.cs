using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Ultimate — The Cosmological Constant. Structured multi-hit sketch: damage
    /// per hit and hit count come from the authored AbilityData; the cinematic
    /// black-hole presentation remains placeholder until the ultimate pass.
    /// </summary>
    public partial class EinsteinUltimate : BaseSpecial {

        private const float CinematicDuration = 2.0f;
        private float _hitTimer;
        private int _hitsDone;

        private float DamagePerHit => Data?.BaseDamage ?? 15f;
        private int HitCount => Data?.HitCount > 0 ? Data.HitCount : 5;

        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            PhaseTimer = 0.5f;
            _hitsDone = 0;
            _hitTimer = 0;

            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            PhaseTimer = CinematicDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = 0.3f;
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                _hitTimer += dt;
                float hitInterval = CinematicDuration / HitCount;
                while (_hitTimer >= hitInterval && _hitsDone < HitCount) {
                    _hitTimer -= hitInterval;
                    _hitsDone++;
                    DealUltimateDamage();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void DealUltimateDamage() {
            var hitbox = GetOrCreateChildHitbox("UltHitbox");
            if (hitbox != null) {
                hitbox.Damage = DamagePerHit;
                float facing = Owner?.IsFacingRight == true ? 1f : -1f;
                if (hitbox.GetChildCount() > 0 && hitbox.GetChild(0) is CollisionShape2D shape)
                    shape.Position = new Vector2(50 * facing, -32);
                hitbox.Activate();
                GetTree().CreateTimer(0.2f).Timeout += () => hitbox.Deactivate();
            }
        }
    }
}
