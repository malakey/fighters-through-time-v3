using Godot;

namespace FTT.Core {

    public partial class HapticFeedbackManager : Node {
        public static HapticFeedbackManager Instance { get; private set; }

        private float _intensityMultiplier = 0.7f;
        private bool _enabled = true;

        public override void _Ready() {
            Instance = this;
            if (SaveManager.Instance?.GlobalData != null) {
                _enabled = SaveManager.Instance.GlobalData.HapticsEnabled;
                _intensityMultiplier = SaveManager.Instance.GlobalData.HapticIntensity;
            }
            EventBus.Instance.OnPlayerHPChanged += OnDamageDealt;
            EventBus.Instance.OnBlockBroken += OnGuardBroken;
            EventBus.Instance.OnHitConfirm += OnHitConfirmed;
            EventBus.Instance.OnUltimateActivation += OnUltimateActivated;
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerHPChanged -= OnDamageDealt;
                EventBus.Instance.OnBlockBroken -= OnGuardBroken;
                EventBus.Instance.OnHitConfirm -= OnHitConfirmed;
                EventBus.Instance.OnUltimateActivation -= OnUltimateActivated;
            }
        }

        /// <summary>
        /// Resolves the joypad a player slot actually owns. Local 1v1 must not
        /// buzz device 0 for both fighters, and a player index is not a device id:
        /// <c>InputManager</c> owns that mapping.
        /// </summary>
        public static int ResolveDevice(int playerIndex) {
            if (playerIndex < 0) return InputManager.UnassignedDevice;
            return InputManager.Instance?.GetDeviceForPlayer(playerIndex) ?? InputManager.UnassignedDevice;
        }

        public void SetIntensity(float intensity) {
            _intensityMultiplier = Mathf.Clamp(intensity, 0f, 1f);
        }

        public void SetEnabled(bool enabled) { _enabled = enabled; }

        private void Vibrate(int device, float weakMotor, float strongMotor, float duration) {
            if (!_enabled || device < 0) return;
            float w = Mathf.Clamp(weakMotor * _intensityMultiplier, 0f, 1f);
            float s = Mathf.Clamp(strongMotor * _intensityMultiplier, 0f, 1f);
            Input.StartJoyVibration(device, w, s, duration);
        }

        private void OnDamageDealt(PlayerHPPayload payload) {
            if (payload.DamageAmount <= 0f) return;
            VibrateForPlayer(payload.PlayerIndex, 0.3f, 0.5f, 0.08f);
        }

        private void OnGuardBroken(int playerIndex) {
            VibrateForPlayer(playerIndex, 0.8f, 0.9f, 0.2f);
        }

        private void OnHitConfirmed(HitConfirmPayload payload) {
            OnHitConfirm(ResolveDevice(payload.PlayerIndex), payload.IsHeavy);
        }

        private void OnUltimateActivated(UltimateActivationPayload payload) {
            OnUltimateActivation(ResolveDevice(payload.PlayerIndex));
        }

        /// <summary>Buzzes the device that player slot owns, if it owns one.</summary>
        public void VibrateForPlayer(int playerIndex, float weakMotor, float strongMotor, float duration) {
            Vibrate(ResolveDevice(playerIndex), weakMotor, strongMotor, duration);
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
