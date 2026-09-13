namespace FTT.Combat {

    /// <summary>
    /// The two status slots every combatant carries (V7.6). A damaging status and
    /// a control status coexist; a new application only ever competes with the
    /// occupant of its own slot.
    /// </summary>
    public enum StatusSlot {
        Damage = 0,
        Control = 1
    }

    /// <summary>One slot's occupant: type, remaining duration in seconds, potency.</summary>
    public struct StatusEffectData {
        public FTT.Core.StatusType Type;
        public float RemainingSeconds;
        public float Intensity;

        public readonly bool IsActive => Type != FTT.Core.StatusType.None && RemainingSeconds > 0f;

        public static StatusEffectData Empty => new() {
            Type = FTT.Core.StatusType.None,
            RemainingSeconds = 0f,
            Intensity = 0f
        };
    }

    /// <summary>
    /// The contracted two-slot container (V7.6 <c>ActiveStatuses : StatusSlots</c>),
    /// carried by <c>PlayerController</c>, <c>EnemyController</c> and
    /// <c>BossController</c>. The deterministic simulation stores the same six
    /// values as flat fields on <c>FighterRuntimeComponent</c>.
    /// </summary>
    public struct StatusSlots {
        public StatusEffectData Damage;
        public StatusEffectData Control;

        public readonly StatusEffectData this[StatusSlot slot] =>
            slot == StatusSlot.Damage ? Damage : Control;

        public readonly bool Has(FTT.Core.StatusType type) =>
            type != FTT.Core.StatusType.None && (Damage.Type == type || Control.Type == type);

        /// <summary>Control slot first, then the damage slot — the compat view for single-status readers.</summary>
        public readonly FTT.Core.StatusType Active =>
            Control.Type != FTT.Core.StatusType.None ? Control.Type : Damage.Type;

        public static StatusSlots Empty => new() {
            Damage = StatusEffectData.Empty,
            Control = StatusEffectData.Empty
        };
    }

    /// <summary>
    /// The single status-slot routing table and the V7.6 stronger-wins
    /// replacement rule. Every application site — <c>StatusController</c>,
    /// <c>EnemyController</c>, <c>BossController</c> and the deterministic
    /// <c>FighterEntitySystems.ApplyStatus</c> — routes through this class, so a
    /// wrong slot assignment is a compile-time table entry rather than a runtime
    /// bug, and the four sites cannot drift apart.
    ///
    /// Deliberately free of Godot types: the deterministic simulation calls
    /// <see cref="SlotOf"/> and <see cref="ShouldReplaceRaw"/> on its hot path.
    /// </summary>
    public static class StatusRouting {

        /// <summary>
        /// The compile-time slot table. Damage slot: Venom, RadiantBurn. Control
        /// slot: TimeDilation, StaticCharge, Root, Suppression.
        /// </summary>
        public static StatusSlot SlotOf(FTT.Core.StatusType type) => type switch {
            FTT.Core.StatusType.Venom => StatusSlot.Damage,
            FTT.Core.StatusType.RadiantBurn => StatusSlot.Damage,
            FTT.Core.StatusType.TimeDilation => StatusSlot.Control,
            FTT.Core.StatusType.StaticCharge => StatusSlot.Control,
            FTT.Core.StatusType.Root => StatusSlot.Control,
            FTT.Core.StatusType.Suppression => StatusSlot.Control,
            _ => StatusSlot.Control
        };

        /// <summary>Integer form for the simulation, which stores the type as an int.</summary>
        public static StatusSlot SlotOf(int statusType) => SlotOf((FTT.Core.StatusType)statusType);

        public static bool IsDamageSlot(FTT.Core.StatusType type) => SlotOf(type) == StatusSlot.Damage;

        /// <summary>
        /// V7.6 stronger-wins (replacing the V7 newest-always-wins rule). Within a
        /// slot: a different type always replaces; the same type replaces only when
        /// <c>newIntensity × newDuration &gt;= currentIntensity × currentRemaining</c>,
        /// bringing its own duration — nothing is ever added or refreshed. A weaker
        /// same-type application does nothing at all. The other slot is never
        /// touched (the caller owns that; this rule only judges one slot).
        /// </summary>
        public static bool ShouldReplace(
            FTT.Core.StatusType current, float currentIntensity, float currentRemaining,
            FTT.Core.StatusType incoming, float incomingIntensity, float incomingDuration) {
            if (incoming == FTT.Core.StatusType.None || incomingDuration <= 0f) return false;
            if (current == FTT.Core.StatusType.None || currentRemaining <= 0f) return true;
            if (current != incoming) return true;
            float currentStrength = Normalize(currentIntensity) * currentRemaining;
            float incomingStrength = Normalize(incomingIntensity) * incomingDuration;
            return incomingStrength >= currentStrength;
        }

        /// <summary>
        /// The fixed-point sibling for the deterministic simulation: the strength
        /// product is an exact <c>int64</c> of the FP64 raw intensity times the
        /// integer frame count, so it is bit-reproducible across a rollback and
        /// needs no FP64 division. Both raw intensities must already be
        /// normalized by the caller (the sim's ApplyStatus substitutes
        /// <c>FP64.One</c> for a non-positive authored intensity before storing).
        /// </summary>
        public static bool ShouldReplaceRaw(
            int currentType, long currentIntensityRaw, int currentFrames,
            int incomingType, long incomingIntensityRaw, int incomingFrames) {
            if (incomingType == (int)FTT.Core.StatusType.None || incomingFrames <= 0) return false;
            if (currentType == (int)FTT.Core.StatusType.None || currentFrames <= 0) return true;
            if (currentType != incomingType) return true;
            long currentStrength = currentIntensityRaw * currentFrames;
            long incomingStrength = incomingIntensityRaw * incomingFrames;
            return incomingStrength >= currentStrength;
        }

        /// <summary>A non-positive authored intensity means "standard potency" (1.0).</summary>
        private static float Normalize(float intensity) => intensity <= 0f ? 1f : intensity;
    }

    /// <summary>
    /// The common contract every status-bearing combatant implements (V7.6).
    /// Deliberately not derived from a damage interface: the repository has no
    /// <c>IDamageable</c> today (see the Package 11 §9 A1 note), and inventing
    /// one would be a cross-cutting hit-pipeline refactor outside this
    /// workstream.
    /// </summary>
    public interface IStatusEffectTarget {
        /// <summary>The two-slot snapshot. Setting it restores both slots wholesale.</summary>
        StatusSlots ActiveStatuses { get; set; }

        /// <summary>Routes by <see cref="StatusRouting.SlotOf"/> and applies stronger-wins.</summary>
        void ApplyStatusEffect(FTT.Core.StatusType status, float duration, float intensity);

        void ClearStatusEffect(StatusSlot slot);

        /// <summary>Death, respawn, Restart Level and Fighter stock loss clear both slots.</summary>
        void ClearAllStatusEffects();

        Godot.Node2D TargetNode { get; }
    }
}
