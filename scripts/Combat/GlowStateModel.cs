using System;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Priority layers for the <b>secondary effect</b> outline/glow arbiter,
    /// ordered lowest to highest. design-godot.md "Priority &amp; Overwrite":
    /// hyper-armor / spawn invincibility override status effects. When a higher
    /// layer clears, the next active layer takes over.
    ///
    /// <para>Package 11 A8 / F24: the Fighter player-slot edge is no longer one of
    /// these layers — see <see cref="SlotIndicator"/>.</para>
    /// </summary>
    public enum GlowLayer {
        /// <summary>
        /// <b>Retired by Package 11 A8 (F24 Option A).</b> The Fighter player-slot
        /// edge used to be the lowest arbitrated layer, so any status, armor or
        /// spawn source outranked and replaced it — precisely what F24 forbids
        /// ("damage, statuses, armor, invulnerability, ability decoys and an effect
        /// expiring cannot recolor, pulse, disable or replace it"). It is now an
        /// independent persistent shader channel owned by
        /// <c>GlowPresentationController.SetSlotIndicator</c>.
        ///
        /// <para>The member stays so the ordinals below do not shift and so a stale
        /// push degrades to an invisible low-priority state rather than failing to
        /// compile. Nothing in the repository pushes it.</para>
        /// </summary>
        SlotIndicator = 0,
        /// <summary>Active status effect.</summary>
        Status = 1,
        /// <summary>Respawn / post-rewind invulnerability aura.</summary>
        SpawnInvulnerability = 2,
        /// <summary>Hyper-armor (Chronal Armoring) shell; highest priority.</summary>
        HyperArmor = 3
    }

    /// <summary>
    /// One authored glow configuration. Maps 1:1 onto the shader uniforms in
    /// <c>assets/shaders/outline_glow.gdshader</c>.
    /// </summary>
    public readonly struct GlowState : IEquatable<GlowState> {
        public GlowState(GlowLayer layer, Color outlineColor, float thickness, float intensity, float pulseSpeed) {
            Layer = layer;
            OutlineColor = outlineColor;
            Thickness = Mathf.Clamp(thickness, 0f, 5f);
            Intensity = Mathf.Clamp(intensity, 0.5f, 3f);
            PulseSpeed = Mathf.Max(0f, pulseSpeed);
        }

        public GlowLayer Layer { get; }
        public Color OutlineColor { get; }
        public float Thickness { get; }
        public float Intensity { get; }
        public float PulseSpeed { get; }

        /// <summary>A state with no visible outline still counts as "not pushed".</summary>
        public bool IsVisible => OutlineColor.A > 0f && Thickness > 0f;

        public bool Equals(GlowState other) =>
            Layer == other.Layer
            && OutlineColor == other.OutlineColor
            && Mathf.IsEqualApprox(Thickness, other.Thickness)
            && Mathf.IsEqualApprox(Intensity, other.Intensity)
            && Mathf.IsEqualApprox(PulseSpeed, other.PulseSpeed);

        public override bool Equals(object obj) => obj is GlowState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Layer, OutlineColor, Thickness, Intensity, PulseSpeed);
        public static bool operator ==(GlowState left, GlowState right) => left.Equals(right);
        public static bool operator !=(GlowState left, GlowState right) => !left.Equals(right);
    }

    /// <summary>
    /// The priority stack behind <see cref="GlowPresentationController"/>. Kept free
    /// of Godot nodes so the arbitration rules are testable without the engine.
    /// One slot per layer: pushing the same layer twice replaces it rather than
    /// stacking, matching "the newest status completely replaces the previous one".
    /// </summary>
    public sealed class GlowStateStack {
        private const int LayerCount = 4;
        private readonly GlowState[] _states = new GlowState[LayerCount];
        private readonly bool[] _active = new bool[LayerCount];

        public int ActiveLayerCount {
            get {
                int count = 0;
                for (int index = 0; index < LayerCount; index++) {
                    if (_active[index]) count++;
                }
                return count;
            }
        }

        public void Push(GlowState state) {
            int index = (int)state.Layer;
            if (index < 0 || index >= LayerCount) return;
            _states[index] = state;
            _active[index] = true;
        }

        public void Clear(GlowLayer layer) {
            int index = (int)layer;
            if (index < 0 || index >= LayerCount) return;
            _active[index] = false;
            _states[index] = default;
        }

        public void ClearAll() {
            for (int index = 0; index < LayerCount; index++) {
                _active[index] = false;
                _states[index] = default;
            }
        }

        public bool IsActive(GlowLayer layer) {
            int index = (int)layer;
            return index >= 0 && index < LayerCount && _active[index];
        }

        /// <summary>Highest-priority active layer, or false when nothing is pushed.</summary>
        public bool TryResolve(out GlowState resolved) {
            for (int index = LayerCount - 1; index >= 0; index--) {
                if (!_active[index]) continue;
                resolved = _states[index];
                return true;
            }
            resolved = default;
            return false;
        }
    }

    /// <summary>
    /// The authored glow palette from design-godot.md's gameplay-integration table.
    /// Static so both the Story arbiter and the Fighter driver read one source.
    /// </summary>
    public static class GlowPalette {
        public static readonly Color HyperArmorColor = new(0.831f, 0.686f, 0.216f);   // #d4af37
        public static readonly Color SpawnInvulnerabilityColor = new(0f, 0.941f, 1f); // #00f0ff
        public static readonly Color TimeDilationColor = new(0.2f, 0.4f, 1f);         // #3366ff
        public static readonly Color StaticChargeColor = new(1f, 0.843f, 0f);         // #ffd700
        public static readonly Color RadiantBurnColor = new(1f, 0.271f, 0f);          // #ff4500
        public static readonly Color VenomColor = new(0.6f, 0f, 1f);                  // #9900ff
        /// <summary>Venom lerps between its two authored colours over the status duration.</summary>
        public static readonly Color VenomSecondaryColor = new(0f, 1f, 0.4f);         // #00ff66
        public static readonly Color RootColor = new(0.545f, 0.271f, 0.075f);         // #8B4513
        /// <summary>
        /// V7.6 Suppression (Package 11 A1): a cold desaturated grey. The design
        /// calls for the persistent gold resonance aura to DESATURATE and the
        /// outline to drop to base priority rather than for a loud new outline —
        /// so this state is deliberately thin, dim and non-pulsing.
        /// </summary>
        public static readonly Color SuppressionColor = new(0.45f, 0.48f, 0.53f);     // #737a87

        private static readonly Color[] SlotColors = {
            new(0f, 0.941f, 1f),      // P1 #00f0ff
            new(1f, 0.2f, 0.4f),      // P2 #ff3366
            new(1f, 0.843f, 0f),      // P3 #ffd700
            new(0f, 1f, 0.533f)       // P4 #00ff88
        };

        public static Color SlotColor(int playerIndex) =>
            playerIndex >= 0 && playerIndex < SlotColors.Length ? SlotColors[playerIndex] : SlotColors[0];

        public static GlowState HyperArmor() =>
            new(GlowLayer.HyperArmor, HyperArmorColor, 3f, 2f, 1.5f);

        public static GlowState SpawnInvulnerability() =>
            new(GlowLayer.SpawnInvulnerability, SpawnInvulnerabilityColor, 2.5f, 1.8f, 2f);

        /// <summary>
        /// F24's ownership edge: the slot colour at the 1 px reference thickness,
        /// static intensity, no pulse. Returned as a <see cref="GlowState"/> for
        /// convenience, but it is <b>not</b> pushed onto the effect stack — the
        /// arbiter writes it to the independent owner channel.
        /// </summary>
        public static GlowState SlotIndicator(int playerIndex) =>
            new(GlowLayer.SlotIndicator, SlotColor(playerIndex), 1f, 1f, 0f);

        /// <summary>Authored per-status outline; <c>None</c> yields an invisible state.</summary>
        public static GlowState Status(FTT.Core.StatusType type) => type switch {
            FTT.Core.StatusType.TimeDilation => new GlowState(GlowLayer.Status, TimeDilationColor, 2f, 1.5f, 0f),
            FTT.Core.StatusType.StaticCharge => new GlowState(GlowLayer.Status, StaticChargeColor, 2.5f, 2f, 3f),
            FTT.Core.StatusType.RadiantBurn => new GlowState(GlowLayer.Status, RadiantBurnColor, 2f, 2.5f, 1f),
            FTT.Core.StatusType.Venom => new GlowState(GlowLayer.Status, VenomColor, 2f, 1.5f, 0.5f),
            FTT.Core.StatusType.Root => new GlowState(GlowLayer.Status, RootColor, 1.5f, 1.2f, 0f),
            FTT.Core.StatusType.Suppression => new GlowState(GlowLayer.Status, SuppressionColor, 1f, 1f, 0f),
            _ => new GlowState(GlowLayer.Status, new Color(0f, 0f, 0f, 0f), 0f, 1f, 0f)
        };
    }
}
