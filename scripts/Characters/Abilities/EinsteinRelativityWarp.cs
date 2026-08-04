using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    public partial class EinsteinRelativityWarp : BaseSpecial {

        private const float WarpDuration = 0.2f;
        private const float WarpDistance = 150f;
        private const float CooldownTime = 5.0f;

        private Vector2 _warpDirection;
        private Vector2 _startPosition;
        private float _warpTimer;

        protected override void OnStartup() {
            PhaseTimer = 0.05f;

            float hInput = Owner.CurrentInputFrame.Horizontal;
            float vInput = Owner.CurrentInputFrame.Vertical;

            _warpDirection = new Vector2(hInput, vInput);
            if (_warpDirection == Vector2.Zero) {
                _warpDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            }
            _warpDirection = _warpDirection.Normalized();
            _startPosition = Owner.GlobalPosition;
        }

        protected override void OnActive() {
            PhaseTimer = WarpDuration;
            _warpTimer = WarpDuration;
        }

        protected override void OnRecovery() {
            PhaseTimer = 0.1f;
            Owner.MovementAbilityCooldownTimer = CooldownTime;

            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = "Relativity Warp",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = FTT.Core.AbilitySlot.MovementAbility,
                Duration = CooldownTime
            });
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float dt = (float)delta;
                _warpTimer -= dt;
                float speed = WarpDistance / WarpDuration * 60f;
                Owner.Velocity = _warpDirection * speed;
            }
            base._PhysicsProcess(delta);
        }
    }
}
