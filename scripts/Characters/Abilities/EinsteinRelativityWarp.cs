using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Movement — Relativity Warp: folds spacetime to warp a short distance in the
    /// input direction (horizontal, vertical, or diagonal), usable in the air.
    /// The warp travel can cancel into a brief float glide when jump is held as it
    /// ends. Distance, duration, and cooldown come from the authored
    /// MovementAbilityData resource.
    /// </summary>
    public partial class EinsteinRelativityWarp : BaseSpecial {

        private const float FloatDuration = 1.0f;

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2): the warp's
        /// reduced-gravity float window lasts 20 frames longer.
        /// </summary>
        public const string ExtendedFloatPerkKey = "extended_float";
        /// <summary>V7.6 Extended Float: additional float frames at 60 Hz.</summary>
        public const int ExtendedFloatBonusFrames = 20;

        private Vector2 _warpDirection;
        private Vector2 _startPosition;
        private float _warpDuration = 0.2f;
        private float _warpDistance = 150f;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            EmitCharacterVfx(
                "res://scenes/vfx/einstein/EinsteinRelativityWarpVfx.tscn",
                Owner.GlobalPosition + new Vector2(0f, -28f));
            _warpDuration = MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 0.2f;
            _warpDistance = MovementData?.DistanceMoved > 0f ? MovementData.DistanceMoved : 150f;

            float hInput = Owner.CurrentInputFrame.Horizontal;
            // Godot 2D Y is down and the vertical axis is Down minus Up, so a
            // negative value warps upward. Since §2.7 that comes from the real
            // Up input (W / stick up / dpad-up), not from a held Jump.
            float vInput = Owner.CurrentInputFrame.Vertical;
            _warpDirection = new Vector2(hInput, vInput);
            if (_warpDirection == Vector2.Zero) {
                _warpDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            }
            _warpDirection = _warpDirection.Normalized();
            _startPosition = Owner.GlobalPosition;
        }

        protected override void OnActive() {
            PhaseTimer = _warpDuration;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();

            // The design allows canceling the warp into a brief float glide; hold
            // jump as the warp ends to trigger it in the air.
            if (!Owner.IsOnFloor() && Owner.CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Jump)) {
                // Extended Float (traversal node): +20 frames on the window.
                Owner.StoryFloatTimer = FloatDuration
                    + (Owner.HasStoryPerk(ExtendedFloatPerkKey)
                        ? ExtendedFloatBonusFrames / 60f
                        : 0f);
            }

            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Relativity Warp",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float speed = _warpDistance / _warpDuration;
                Owner.Velocity = _warpDirection * speed;
            }
            base._PhysicsProcess(delta);
        }
    }
}
