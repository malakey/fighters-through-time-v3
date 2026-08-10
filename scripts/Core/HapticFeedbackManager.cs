using System.Collections.Generic;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// Controller vibration per the design's haptic trigger table
    /// (design-godot.md:1731-1741). Every pattern lives here as a named event
    /// method with public constants so the table exists in exactly one place and
    /// the tests can pin it; call sites ask for the event, not for magnitudes.
    ///
    /// <para>All routing goes through <see cref="ResolveDevice"/>: a player index
    /// is not a device id, and local 1v1 must buzz the pad the slot actually owns.
    /// The global enable toggle and intensity slider are applied inside
    /// <see cref="Vibrate"/>, so no event can bypass accessibility settings.</para>
    /// </summary>
    public partial class HapticFeedbackManager : Node {
        public static HapticFeedbackManager Instance { get; private set; }

        // === The design table (audit M-31). Low-freq motor maps to Godot's weak
        // magnitude, high-freq to strong, matching the pre-existing convention. ===
        public const float TakingDamageWeak = 0.4f;
        public const float TakingDamageStrong = 0.3f;
        public const float TakingDamageSeconds = 0.1f;

        public const float GuardImpactWeak = 0.2f;
        public const float GuardImpactStrong = 0.4f;
        public const float GuardImpactSeconds = 0.06f;

        public const float GuardBreakWeak = 0.8f;
        public const float GuardBreakStrong = 0.9f;
        public const float GuardBreakSeconds = 0.2f;

        public const float HeavyLandingWeak = 0.5f;
        public const float HeavyLandingStrong = 0.2f;
        public const float HeavyLandingSeconds = 0.1f;

        public const float StageHazardWeak = 0.3f;
        public const float StageHazardStrong = 0.6f;
        public const float StageHazardSeconds = 0.15f;

        public const float KnockoutWeak = 1.0f;
        public const float KnockoutStrong = 1.0f;
        public const float KnockoutSeconds = 0.3f;

        /// <summary>Fall height, in Fighter world units, above which a landing is
        /// "heavy" (design: "fall &gt; 3 units").</summary>
        public const float HeavyLandingFallUnits = 3.0f;

        // Ultimate Activation ramps 0.4 -> 1.0 across its 500 ms window. Godot's
        // Input.StartJoyVibration is a constant pulse, so the ramp is staged: the
        // manager re-issues a short overlapping pulse every process frame at the
        // interpolated magnitude until the window closes.
        public const float UltimateRampStart = 0.4f;
        public const float UltimateRampEnd = 1.0f;
        public const float UltimateRampSeconds = 0.5f;
        private const float RampSliceSeconds = 0.1f;

        private float _intensityMultiplier = 0.7f;
        private bool _enabled = true;

        private struct UltimateRamp {
            public int Device;
            public float Elapsed;
        }

        private readonly List<UltimateRamp> _ultimateRamps = new();

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

        /// <summary>Advances any in-flight ultimate ramps with staged pulses.</summary>
        public override void _Process(double delta) {
            if (_ultimateRamps.Count == 0) return;
            for (int index = _ultimateRamps.Count - 1; index >= 0; index--) {
                UltimateRamp ramp = _ultimateRamps[index];
                ramp.Elapsed += (float)delta;
                float magnitude = UltimateRampMagnitude(ramp.Elapsed);
                Vibrate(ramp.Device, magnitude, magnitude, RampSliceSeconds);
                if (ramp.Elapsed >= UltimateRampSeconds) {
                    _ultimateRamps.RemoveAt(index);
                } else {
                    _ultimateRamps[index] = ramp;
                }
            }
        }

        /// <summary>The ramp curve, isolated so tests can pin its endpoints.</summary>
        public static float UltimateRampMagnitude(float elapsedSeconds) =>
            Mathf.Lerp(
                UltimateRampStart,
                UltimateRampEnd,
                Mathf.Clamp(elapsedSeconds / UltimateRampSeconds, 0f, 1f));

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

        // === Named design-table events ===

        /// <summary>Taking Damage: 0.4 / 0.3 / 100 ms.</summary>
        public void OnTakingDamage(int playerIndex) =>
            VibrateForPlayer(playerIndex, TakingDamageWeak, TakingDamageStrong, TakingDamageSeconds);

        /// <summary>Guard Impact: a successful block absorb, 0.2 / 0.4 / 60 ms.</summary>
        public void OnGuardImpact(int playerIndex) =>
            VibrateForPlayer(playerIndex, GuardImpactWeak, GuardImpactStrong, GuardImpactSeconds);

        /// <summary>Heavy Landing (fall &gt; 3 units): 0.5 / 0.2 / 100 ms.</summary>
        public void OnHeavyLanding(int playerIndex) =>
            VibrateForPlayer(playerIndex, HeavyLandingWeak, HeavyLandingStrong, HeavyLandingSeconds);

        /// <summary>Stage Hazard Hit: 0.3 / 0.6 / 150 ms — deliberately distinct from
        /// the player-attack damage buzz.</summary>
        public void OnStageHazardHit(int playerIndex) =>
            VibrateForPlayer(playerIndex, StageHazardWeak, StageHazardStrong, StageHazardSeconds);

        /// <summary>KO / Death: full intensity, 300 ms.</summary>
        public void OnKnockout(int playerIndex) =>
            VibrateForPlayer(playerIndex, KnockoutWeak, KnockoutStrong, KnockoutSeconds);

        private void OnDamageDealt(PlayerHPPayload payload) {
            if (payload.DamageAmount <= 0f) return;
            OnTakingDamage(payload.PlayerIndex);
        }

        private void OnGuardBroken(int playerIndex) {
            VibrateForPlayer(playerIndex, GuardBreakWeak, GuardBreakStrong, GuardBreakSeconds);
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

        /// <summary>
        /// Ultimate Activation: 0.4 -&gt; 1.0 ramp over 500 ms, staged as per-frame
        /// pulses. Re-activation on the same device restarts its ramp.
        /// </summary>
        public void OnUltimateActivation(int device) {
            if (!_enabled || device < 0) return;
            for (int index = 0; index < _ultimateRamps.Count; index++) {
                if (_ultimateRamps[index].Device != device) continue;
                _ultimateRamps[index] = new UltimateRamp { Device = device, Elapsed = 0f };
                Vibrate(device, UltimateRampStart, UltimateRampStart, RampSliceSeconds);
                return;
            }
            _ultimateRamps.Add(new UltimateRamp { Device = device, Elapsed = 0f });
            Vibrate(device, UltimateRampStart, UltimateRampStart, RampSliceSeconds);
        }

        /// <summary>Ramps currently in flight, for tests.</summary>
        public int ActiveUltimateRampCount => _ultimateRamps.Count;
    }
}
