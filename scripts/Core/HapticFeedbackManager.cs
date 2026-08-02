using Godot;

namespace FTT.Core {

    public partial class HapticFeedbackManager : Node {
        public static HapticFeedbackManager Instance { get; private set; }

        private float _intensityMultiplier = 0.7f;
        private bool _enabled = true;

        public override void _Ready() {
            Instance = this;
            EventBus.Instance.OnPlayerHPChanged += OnDamageDealt;
            EventBus.Instance.OnBlockBroken += OnGuardBroken;
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerHPChanged -= OnDamageDealt;
                EventBus.Instance.OnBlockBroken -= OnGuardBroken;
            }
        }

        public void SetIntensity(float intensity) {
            _intensityMultiplier = Mathf.Clamp(intensity, 0f, 1f);
        }

        public void SetEnabled(bool enabled) { _enabled = enabled; }

        private void Vibrate(int device, float weakMotor, float strongMotor, float duration) {
            if (!_enabled) return;
            float w = Mathf.Clamp(weakMotor * _intensityMultiplier, 0f, 1f);
            float s = Mathf.Clamp(strongMotor * _intensityMultiplier, 0f, 1f);
            Input.StartJoyVibration(device, w, s, duration);
        }

        private void OnDamageDealt(PlayerHPPayload payload) {
            Vibrate(0, 0.3f, 0.5f, 0.08f);
        }

        private void OnGuardBroken(int playerIndex) {
            Vibrate(playerIndex, 0.8f, 0.9f, 0.2f);
        }

        public void OnHitConfirm(int device, bool isHeavy) {
            if (isHeavy) Vibrate(device, 0.6f, 0.7f, 0.12f);
            else Vibrate(device, 0.3f, 0.5f, 0.08f);
        }

        public void OnUltimateActivation(int device) {
            Vibrate(device, 0.4f, 1.0f, 0.5f);
        }
    }
}
