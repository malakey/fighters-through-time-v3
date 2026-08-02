using Godot;

namespace FTT.Combat {

    public partial class BlockSystem : Node {
        [Export] public int MaxCharges = 3;
        public int CurrentCharges { get; private set; }

        private float _regenTimer;
        private const float RegenInterval = 3.0f;
        private bool _isBlocking;

        public override void _Ready() {
            CurrentCharges = MaxCharges;
        }

        public bool IsBlocking => _isBlocking;

        public void StartBlock() {
            _isBlocking = true;
            _regenTimer = 0f;
        }

        public void EndBlock() {
            _isBlocking = false;
        }

        public bool AbsorbHit() {
            if (!_isBlocking || CurrentCharges <= 0) return false;
            CurrentCharges--;
            _regenTimer = 0f;

            if (CurrentCharges <= 0) {
                _isBlocking = false;
                var owner = GetParent<FTT.Characters.PlayerController>();
                owner?.TransitionTo(FTT.Characters.CharacterState.Dazed);

                var knockback = new Vector2(2.0f, 1.0f);
                if (owner != null) {
                    var vel = owner.Velocity;
                    vel += owner.IsFacingRight ? new Vector2(-knockback.X * 60f, -knockback.Y * 60f)
                                               : new Vector2(knockback.X * 60f, -knockback.Y * 60f);
                    owner.Velocity = vel;
                }

                FTT.Core.EventBus.Instance?.RaiseBlockBroken(owner?.PlayerIndex ?? 0);
                return false;
            }
            return true;
        }

        public override void _PhysicsProcess(double delta) {
            if (_isBlocking || CurrentCharges >= MaxCharges) return;
            _regenTimer += (float)delta;
            if (_regenTimer >= RegenInterval) {
                _regenTimer = 0f;
                CurrentCharges = Mathf.Min(CurrentCharges + 1, MaxCharges);
            }
        }
    }
}
