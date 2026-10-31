using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// MM-HUD (meter and Defy update, 2026-10-31; ledger <c>USER-DEFY-HUD-NOTCH</c>). The Defy notch on the Influence meter: one
    /// mark at <see cref="NotchFraction"/> (the 50 Influence Defy costs, half the 100 cap) on the Story Ultimate ring and the
    /// Fighter meter tube. It is lit (gold) only when the meter has the Defy cost <b>and</b> <c>gameplay_defy</c> is bound for
    /// the player's current device kind; otherwise it stays dim (<c>Ink3</c>), so a player who has unbound Defy is never
    /// shown a ready cue for a button they cannot press.
    /// </summary>
    public static class DefyNotchRules {

        /// <summary>The notch's share of the meter: <c>DefyInfluenceCost / MaxValue</c> = 0.5.</summary>
        public const float NotchFraction = BasicComboRules.DefyInfluenceCost / InfluenceMeterRules.MaxValue;

        /// <summary>The InputMap action name (<c>InputManager.Actions.Defy</c> once MM-INPUT lands).</summary>
        public const string DefyAction = "gameplay_defy";

        /// <summary>Lit when the meter holds the Defy cost and the action is bound for the device in use.</summary>
        public static bool IsLit(float meter, bool bound) => bound && meter >= BasicComboRules.DefyInfluenceCost;

        /// <summary>
        /// True when <c>gameplay_defy</c> has at least one event of the player's current device kind (a pad button for a pad,
        /// a key or mouse button for the keyboard). False when the action does not exist.
        /// </summary>
        public static bool IsBound(bool joypad) {
            if (!InputMap.HasAction(DefyAction)) return false;
            foreach (InputEvent inputEvent in InputMap.ActionGetEvents(DefyAction)) {
                bool pad = inputEvent is InputEventJoypadButton or InputEventJoypadMotion;
                if (pad == joypad) return true;
            }
            return false;
        }

        /// <summary>True when the player's slot is driven by a pad (a connected pad assigned, else the keyboard).</summary>
        public static bool PlayerUsesJoypad(int playerIndex) {
            InputManager input = InputManager.Instance;
            return input != null && input.IsJoypadConnected(playerIndex);
        }
    }

    /// <summary>
    /// Caches <see cref="DefyNotchRules.IsBound"/> and re-reads it only when the binding service's generation or the device
    /// kind moves, so a HUD asking every frame does not walk the InputMap every frame.
    /// </summary>
    public sealed class DefyBindingProbe {
        private int _generation = -1;
        private int _kind = -1;
        private bool _bound;

        /// <summary>Whether <c>gameplay_defy</c> is bound for <paramref name="playerIndex"/>'s current device kind.</summary>
        public bool IsBound(int playerIndex) {
            bool joypad = DefyNotchRules.PlayerUsesJoypad(playerIndex);
            int kind = joypad ? 1 : 0;
            if (_generation != InputBindingService.Generation || _kind != kind) {
                _generation = InputBindingService.Generation;
                _kind = kind;
                _bound = DefyNotchRules.IsBound(joypad);
            }
            return _bound;
        }

        /// <summary>Forgets the cache (a test that edits the InputMap without going through the binding service).</summary>
        public void Invalidate() => _generation = -1;
    }
}
