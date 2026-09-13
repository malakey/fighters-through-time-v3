using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Combat {

    public interface IStatusStrategy {
        void OnApply(FTT.Characters.PlayerController target, float duration, float intensity);
        void OnTick(FTT.Characters.PlayerController target, float delta);
        void OnRemove(FTT.Characters.PlayerController target);
        float CaptureRuntimeState() => 0f;
        void RestoreRuntimeState(float value) { }
    }

    public struct StatusSlotSnapshot {
        public FTT.Core.StatusType Type;
        public float RemainingDuration;
        public float Intensity;
        public float StrategyRuntimeState;
    }

    public struct StatusSnapshot {
        public StatusSlotSnapshot Control;
        public StatusSlotSnapshot Damage;
    }

    public sealed class TimeDilationStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            float potency = intensity <= 0 ? 1.0f : intensity;
            target.StatusMovementMultiplier = MathF.Max(0.1f, 1.0f - (0.5f * potency));
            target.StatusJumpMultiplier = target.StatusMovementMultiplier;
            target.StatusAnimationMultiplier = 0.5f;
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) {
            target.StatusMovementMultiplier = 1.0f;
            target.StatusJumpMultiplier = 1.0f;
            target.StatusAnimationMultiplier = 1.0f;
        }
    }

    public sealed class VenomStrategy : IStatusStrategy {
        private const float TickInterval = 1.0f;
        private float _damagePerTick;
        private float _tickTimer;

        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            _damagePerTick = 2.0f * (intensity <= 0 ? 1.0f : intensity);
            _tickTimer = TickInterval;
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) {
            _tickTimer -= delta;
            if (_tickTimer <= 0.0f) {
                _tickTimer += TickInterval;
                target.ApplyPersistentDamage(Math.Max(1, (int)MathF.Round(_damagePerTick)));
            }
        }
        public void OnRemove(FTT.Characters.PlayerController target) { }
        public float CaptureRuntimeState() => _tickTimer;
        public void RestoreRuntimeState(float value) => _tickTimer = value;
    }

    public sealed class StaticChargeStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            target.ApplyStun(duration);
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) { }
    }

    public sealed class RadiantBurnStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            float potency = intensity <= 0 ? 1.0f : intensity;
            target.StatusDamageTakenMultiplier = 1.0f + (0.25f * potency);
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) {
            target.StatusDamageTakenMultiplier = 1.0f;
        }
    }

    public sealed class RootStrategy : IStatusStrategy {
        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            target.IsMovementRooted = true;
            var velocity = target.Velocity;
            velocity.X = 0.0f;
            target.Velocity = velocity;
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) {
            target.IsMovementRooted = false;
        }
    }

    /// <summary>
    /// V7.6 Suppression (the Eraser line's Null Lance) — control slot, Story-only
    /// source. The whole effect is the ability-cast lock: Special 1, Special 2,
    /// the Movement Ability and the Ultimate cast are refused (cooldowns keep
    /// ticking, no meter is spent) and the persistent resonance aura desaturates
    /// to cold grey. Basics, block, grab, Rally, DI, landing tech, Defy History,
    /// death rewinds and Time Freeze are deliberately untouched — Time Freeze is
    /// the universal escape tool and is explicitly carved out.
    /// </summary>
    public sealed class SuppressionStrategy : IStatusStrategy {
        /// <summary>Authored standard duration (design: 2.0 s at standard intensity).</summary>
        public const float StandardDurationSeconds = 2.0f;

        public void OnApply(FTT.Characters.PlayerController target, float duration, float intensity) {
            target.SetAbilityCastSuppressed(true);
        }
        public void OnTick(FTT.Characters.PlayerController target, float delta) { }
        public void OnRemove(FTT.Characters.PlayerController target) {
            target.SetAbilityCastSuppressed(false);
        }
    }

    /// <summary>
    /// V7 two-slot status system. A damaging status (Venom, RadiantBurn) and a
    /// control status (TimeDilation, StaticCharge, Root, Suppression) can be
    /// active at the same time; a new application competes only with the
    /// occupant of its own slot. This keeps the trapper kits coherent —
    /// Cleopatra's vortex slow no longer deletes her nest's Venom. Each
    /// strategy's OnRemove resets only its own modifiers so one slot expiring
    /// cannot strip the other slot's effect.
    ///
    /// V7.6 (Package 11 A1): slot routing is <see cref="StatusRouting.SlotOf"/>
    /// — the one table shared with the enemy, boss and simulation sites — and
    /// within a slot the rule is <b>stronger-wins</b>
    /// (<see cref="StatusRouting.ShouldReplace"/>), not newest-wins: a weaker
    /// same-type reapplication does nothing at all.
    /// </summary>
    public partial class StatusController : Node {

        private sealed class Slot {
            public FTT.Core.StatusType Type = FTT.Core.StatusType.None;
            public IStatusStrategy Strategy;
            public float Remaining;
            public float Intensity;
        }

        private readonly Slot _control = new();
        private readonly Slot _damage = new();
        private FTT.Characters.PlayerController _owner;

        private static readonly Dictionary<FTT.Core.StatusType, Func<IStatusStrategy>> StrategyFactory = new() {
            { FTT.Core.StatusType.TimeDilation, () => new TimeDilationStrategy() },
            { FTT.Core.StatusType.Venom, () => new VenomStrategy() },
            { FTT.Core.StatusType.StaticCharge, () => new StaticChargeStrategy() },
            { FTT.Core.StatusType.RadiantBurn, () => new RadiantBurnStrategy() },
            { FTT.Core.StatusType.Root, () => new RootStrategy() },
            { FTT.Core.StatusType.Suppression, () => new SuppressionStrategy() }
        };

        /// <summary>
        /// Forwarder to the shared routing table (V7.6). Kept because the HUD and
        /// the simulation's hot path already call this signature; the table itself
        /// lives in <see cref="StatusRouting"/> and has exactly one implementation.
        /// </summary>
        public static bool IsDamageStatus(FTT.Core.StatusType type) =>
            StatusRouting.SlotOf(type) == StatusSlot.Damage;

        private Slot SlotFor(FTT.Core.StatusType type) =>
            StatusRouting.SlotOf(type) == StatusSlot.Damage ? _damage : _control;

        private Slot SlotFor(StatusSlot slot) => slot == StatusSlot.Damage ? _damage : _control;

        public override void _Ready() {
            _owner = GetParent<FTT.Characters.PlayerController>();
        }

        public void ApplyStatus(FTT.Core.StatusType type, float duration, float intensity = 1.0f) {
            if (type == FTT.Core.StatusType.None || duration <= 0.0f || _owner == null) return;
            Slot slot = SlotFor(type);
            // V7.6 stronger-wins: a weaker same-type application is a complete
            // no-op — no replace, no refresh, no event, and the occupant's own
            // remaining duration is left exactly as it was.
            if (!StatusRouting.ShouldReplace(
                    slot.Type, slot.Intensity, slot.Remaining, type, intensity, duration)) return;
            ClearSlot(slot, raiseEvents: false);

            slot.Type = type;
            slot.Remaining = duration;
            slot.Intensity = intensity <= 0.0f ? 1.0f : intensity;
            slot.Strategy = StrategyFactory.TryGetValue(type, out Func<IStatusStrategy> factory) ? factory() : null;
            slot.Strategy?.OnApply(_owner, duration, slot.Intensity);

            FTT.Core.EventBus.Instance?.RaiseStatusEffectApplied(new FTT.Core.StatusEffectPayload {
                TargetIndex = _owner.PlayerIndex,
                Type = type,
                Duration = duration,
                Intensity = slot.Intensity
            });
        }

        public StatusSnapshot CaptureState() => new() {
            Control = CaptureSlot(_control),
            Damage = CaptureSlot(_damage)
        };

        private static StatusSlotSnapshot CaptureSlot(Slot slot) => new() {
            Type = slot.Type,
            RemainingDuration = slot.Remaining,
            Intensity = slot.Intensity,
            StrategyRuntimeState = slot.Strategy?.CaptureRuntimeState() ?? 0f
        };

        public void RestoreState(StatusSnapshot snapshot) {
            ClearStatus();
            RestoreSlot(snapshot.Control);
            RestoreSlot(snapshot.Damage);
        }

        private void RestoreSlot(StatusSlotSnapshot snapshot) {
            if (snapshot.Type == FTT.Core.StatusType.None || snapshot.RemainingDuration <= 0f) return;
            ApplyStatus(snapshot.Type, snapshot.RemainingDuration, snapshot.Intensity);
            Slot slot = SlotFor(snapshot.Type);
            slot.Remaining = snapshot.RemainingDuration;
            slot.Strategy?.RestoreRuntimeState(snapshot.StrategyRuntimeState);
        }

        public void ClearStatus() {
            ClearSlot(_control, raiseEvents: true);
            ClearSlot(_damage, raiseEvents: true);
        }

        /// <summary>Clears only the slot currently holding <paramref name="type"/>; no-op otherwise.</summary>
        public void ClearStatus(FTT.Core.StatusType type) {
            Slot slot = SlotFor(type);
            if (slot.Type == type) ClearSlot(slot, raiseEvents: true);
        }

        private void ClearSlot(Slot slot, bool raiseEvents) {
            bool hadStatus = slot.Type != FTT.Core.StatusType.None;
            slot.Strategy?.OnRemove(_owner);
            slot.Strategy = null;
            slot.Type = FTT.Core.StatusType.None;
            slot.Remaining = 0.0f;
            slot.Intensity = 0.0f;
            if (!hadStatus || _owner == null || !raiseEvents) return;

            // Presentation layers (glow arbiter, HUD status pips) track a single
            // status. When one slot falls with the other still live, re-announce
            // the survivor so those surfaces fall back to it instead of clearing.
            Slot survivor = _control.Type != FTT.Core.StatusType.None ? _control
                : _damage.Type != FTT.Core.StatusType.None ? _damage
                : null;
            if (survivor != null) {
                FTT.Core.EventBus.Instance?.RaiseStatusEffectApplied(new FTT.Core.StatusEffectPayload {
                    TargetIndex = _owner.PlayerIndex,
                    Type = survivor.Type,
                    Duration = survivor.Remaining,
                    Intensity = survivor.Intensity
                });
                return;
            }
            FTT.Core.EventBus.Instance?.RaiseStatusEffectCleared(new FTT.Core.StatusEffectPayload {
                TargetIndex = _owner.PlayerIndex,
                Type = FTT.Core.StatusType.None,
                Duration = 0f,
                Intensity = 0f
            });
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            TickSlot(_control, dt);
            TickSlot(_damage, dt);
        }

        private void TickSlot(Slot slot, float dt) {
            if (slot.Type == FTT.Core.StatusType.None || slot.Strategy == null || _owner == null) return;
            slot.Remaining -= dt;
            slot.Strategy.OnTick(_owner, dt);
            if (slot.Remaining <= 0.0f) ClearSlot(slot, raiseEvents: true);
        }

        /// <summary>Control slot first, then the damage slot — the compat view for single-status readers.</summary>
        public FTT.Core.StatusType ActiveType =>
            _control.Type != FTT.Core.StatusType.None ? _control.Type : _damage.Type;
        public float RemainingDuration =>
            _control.Type != FTT.Core.StatusType.None ? _control.Remaining : _damage.Remaining;
        public float Intensity =>
            _control.Type != FTT.Core.StatusType.None ? _control.Intensity : _damage.Intensity;
        public FTT.Core.StatusType ControlStatusType => _control.Type;
        public FTT.Core.StatusType DamageStatusType => _damage.Type;
        public bool HasStatus(FTT.Core.StatusType type) => _control.Type == type || _damage.Type == type;

        // === V7.6 StatusSlots contract (Package 11 A1) ===
        // The controller is the player's slot store; PlayerController implements
        // IStatusEffectTarget over this surface.

        /// <summary>
        /// The contracted two-slot view. The setter restores both slots wholesale
        /// (strategies are rebuilt and re-applied), which is what death/respawn
        /// and rewind restore need.
        /// </summary>
        public StatusSlots ActiveStatuses {
            get => new() {
                Damage = ReadSlot(_damage),
                Control = ReadSlot(_control)
            };
            set {
                ClearStatus();
                ApplySlotData(value.Damage);
                ApplySlotData(value.Control);
            }
        }

        private static StatusEffectData ReadSlot(Slot slot) => new() {
            Type = slot.Type,
            RemainingSeconds = slot.Remaining,
            Intensity = slot.Intensity
        };

        private void ApplySlotData(StatusEffectData data) {
            if (data.Type == FTT.Core.StatusType.None || data.RemainingSeconds <= 0f) return;
            ApplyStatus(data.Type, data.RemainingSeconds, data.Intensity);
        }

        /// <summary>Clears one slot by identity, whatever occupies it.</summary>
        public void ClearStatusSlot(StatusSlot slot) => ClearSlot(SlotFor(slot), raiseEvents: true);

        /// <summary>Both slots. Death, respawn and Restart Level call this.</summary>
        public void ClearAllStatusEffects() => ClearStatus();
    }
}
