using System;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B1. The readiness state the Story HUD shows for the player's four
    /// ability slots plus the one active status effect.
    ///
    /// Two different sources feed it and they are not interchangeable. Special 1,
    /// Special 2 and the movement ability are cooldown-gated: every kit raises
    /// <c>EventBus.OnCooldownStarted</c> with the authored duration, so the model
    /// counts that down. The ultimate has no cooldown at all — it is gated purely
    /// by the Influence meter reaching 100 — so its readiness is derived from
    /// <c>OnUltimateMeterChanged</c> instead. Treating them the same way was the
    /// obvious first design and it produces an ultimate icon that never lights up.
    ///
    /// Status follows the project's one-status-at-a-time rule: the newest applied
    /// status completely replaces the previous one, and the falling edge
    /// (<c>OnStatusEffectCleared</c>, added by A3) clears it. The local timer is a
    /// presentation fallback only, for a status whose clear event never arrives
    /// because its owner left the tree.
    ///
    /// Pure C# so the whole indicator surface is provable without a scene tree.
    /// </summary>
    public sealed class HudAbilityIndicatorModel {

        /// <summary>Meter value at which the ultimate becomes usable (0-100).</summary>
        public const float UltimateReadyMeter = 100f;

        private const int SlotCount = 4;

        private readonly float[] _remaining = new float[SlotCount];
        private readonly float[] _durations = new float[SlotCount];

        /// <summary>Current Influence meter, 0-100.</summary>
        public float UltimateMeter { get; private set; }

        /// <summary>The single active status, or <c>None</c>.</summary>
        public StatusType ActiveStatus { get; private set; } = StatusType.None;

        /// <summary>Seconds left on the active status, as last reported.</summary>
        public float StatusRemaining { get; private set; }

        /// <summary>Clears every slot, the meter and the status.</summary>
        public void Reset() {
            for (int index = 0; index < SlotCount; index++) {
                _remaining[index] = 0f;
                _durations[index] = 0f;
            }
            UltimateMeter = 0f;
            ActiveStatus = StatusType.None;
            StatusRemaining = 0f;
        }

        /// <summary>
        /// Arms a cooldown for a slot. The ultimate slot is ignored: its readiness
        /// comes from the meter, and accepting a cooldown there would let a stray
        /// payload blank an icon the player can legitimately press.
        /// </summary>
        public void StartCooldown(AbilitySlot slot, float durationSeconds) {
            if (slot == AbilitySlot.Ultimate) return;
            int index = IndexOf(slot);
            if (index < 0) return;
            float duration = durationSeconds > 0f ? durationSeconds : 0f;
            _remaining[index] = duration;
            _durations[index] = duration;
        }

        /// <summary>Reports the meter; also what makes the ultimate slot ready.</summary>
        public void SetUltimateMeter(float value) {
            if (float.IsNaN(value)) return;
            UltimateMeter = Math.Clamp(value, 0f, UltimateReadyMeter);
        }

        /// <summary>
        /// Applies a status, replacing whatever was active. <c>None</c> or a
        /// non-positive duration clears instead, matching
        /// <c>StatusController.ApplyStatus</c>'s own guard.
        /// </summary>
        public void ApplyStatus(StatusType type, float durationSeconds) {
            if (type == StatusType.None || durationSeconds <= 0f) {
                ClearStatus();
                return;
            }
            ActiveStatus = type;
            StatusRemaining = durationSeconds;
        }

        public void ClearStatus() {
            ActiveStatus = StatusType.None;
            StatusRemaining = 0f;
        }

        /// <summary>Advances cooldowns and the status fallback timer.</summary>
        public void Tick(float deltaSeconds) {
            if (deltaSeconds <= 0f) return;
            for (int index = 0; index < SlotCount; index++) {
                if (_remaining[index] <= 0f) continue;
                _remaining[index] -= deltaSeconds;
                if (_remaining[index] <= 0f) {
                    _remaining[index] = 0f;
                    _durations[index] = 0f;
                }
            }
            if (ActiveStatus == StatusType.None) return;
            StatusRemaining -= deltaSeconds;
            if (StatusRemaining <= 0f) ClearStatus();
        }

        /// <summary>Seconds left on a slot's cooldown; the ultimate always reports 0.</summary>
        public float RemainingSeconds(AbilitySlot slot) {
            if (slot == AbilitySlot.Ultimate) return 0f;
            int index = IndexOf(slot);
            return index < 0 ? 0f : _remaining[index];
        }

        /// <summary>
        /// How full the slot's readiness indicator draws, 0 (just used) to 1
        /// (ready). The ultimate reports its meter fraction, which is exactly the
        /// same "how close am I" reading for a meter-gated slot.
        /// </summary>
        public float ReadinessFraction(AbilitySlot slot) {
            if (slot == AbilitySlot.Ultimate) return UltimateMeter / UltimateReadyMeter;
            int index = IndexOf(slot);
            if (index < 0) return 1f;
            if (_durations[index] <= 0f) return 1f;
            return 1f - Math.Clamp(_remaining[index] / _durations[index], 0f, 1f);
        }

        /// <summary>True when the player can actually use the slot right now.</summary>
        public bool IsReady(AbilitySlot slot) {
            if (slot == AbilitySlot.Ultimate) return UltimateMeter >= UltimateReadyMeter;
            return RemainingSeconds(slot) <= 0f;
        }

        private static int IndexOf(AbilitySlot slot) => slot switch {
            AbilitySlot.Special1 => 0,
            AbilitySlot.Special2 => 1,
            AbilitySlot.MovementAbility => 2,
            AbilitySlot.Ultimate => 3,
            _ => -1
        };

        /// <summary>Translation key for a slot's short HUD label.</summary>
        public static string SlotLabelKey(AbilitySlot slot) => slot switch {
            AbilitySlot.Special1 => "hud_slot_special1",
            AbilitySlot.Special2 => "hud_slot_special2",
            AbilitySlot.MovementAbility => "hud_slot_movement",
            _ => "hud_slot_ultimate"
        };

        /// <summary>
        /// Translation key for a status name. The existing <c>status_*</c> family
        /// is reused rather than a HUD-specific one, so the HUD and any other
        /// surface naming a status cannot drift apart.
        /// </summary>
        public static string StatusLabelKey(StatusType type) =>
            "status_" + type.ToString().ToLowerInvariant();
    }
}
