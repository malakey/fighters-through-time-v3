using System;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B1, extended by Package 11 A8 (F24). The readiness state the Story
    /// HUD shows for the player's four ability slots, their V7.5 lock states, and
    /// <b>both</b> active status effects.
    ///
    /// Two different sources feed slot readiness and they are not interchangeable.
    /// Special 1, Special 2 and the movement ability are cooldown-gated: every kit
    /// raises <c>EventBus.OnCooldownStarted</c> with the authored duration, so the
    /// model counts that down. The ultimate has no cooldown at all — it is gated
    /// purely by the Influence meter reaching 100 — so its readiness is derived from
    /// <c>OnUltimateMeterChanged</c> instead. Treating them the same way was the
    /// obvious first design and it produces an ultimate icon that never lights up.
    ///
    /// <para><b>Two status slots (F24).</b> Both status systems have carried two
    /// slots since V7.3 — <c>StatusController</c> has <c>_control</c> and
    /// <c>_damage</c>, and the sim's runtime component bit-packs both — but every
    /// HUD collapsed them into one pip. HUD_CONTRACT forbids that: "Show both
    /// occupied slots simultaneously; never choose a single winning HUD icon based
    /// on outline priority." The slot for a given type is derived here from the
    /// same damage/control split <c>StatusController.IsDamageStatus</c> uses rather
    /// than from a new field on the payload, which is what keeps every event raiser
    /// untouched.</para>
    ///
    /// <para><b>The clock is authoritative.</b> F24: "Radials follow authoritative
    /// simulation clocks, including pauses and Time Freeze; the HUD must not
    /// independently count down a frozen status." <see cref="Tick"/>'s local
    /// countdown is therefore a fallback for a status whose clear event never
    /// arrives (its owner left the tree), and it is suppressed outright while
    /// <see cref="StatusClockSuspended"/> is set. When a live
    /// <c>StatusController</c> is reachable the HUD calls
    /// <see cref="SyncStatusFromAuthority"/> every frame and the fallback never
    /// matters.</para>
    ///
    /// Pure C# so the whole indicator surface is provable without a scene tree.
    /// </summary>
    public sealed class HudAbilityIndicatorModel {

        /// <summary>Meter value at which the ultimate becomes usable (0-100).</summary>
        public const float UltimateReadyMeter = 100f;

        private const int SlotCount = 4;

        private readonly float[] _remaining = new float[SlotCount];
        private readonly float[] _durations = new float[SlotCount];
        private readonly AbilitySlotLockState[] _locks = new AbilitySlotLockState[SlotCount];

        /// <summary>Current Influence meter, 0-100.</summary>
        public float UltimateMeter { get; private set; }

        /// <summary>The control-category status (Time Dilation, Static Charge, Root, Suppression), or <c>None</c>.</summary>
        public StatusType ControlStatus { get; private set; } = StatusType.None;

        /// <summary>Seconds left on <see cref="ControlStatus"/>, as last reported.</summary>
        public float ControlStatusRemaining { get; private set; }

        /// <summary>The damage-category status (Venom, Radiant Burn), or <c>None</c>.</summary>
        public StatusType DamageStatus { get; private set; } = StatusType.None;

        /// <summary>Seconds left on <see cref="DamageStatus"/>, as last reported.</summary>
        public float DamageStatusRemaining { get; private set; }

        /// <summary>
        /// Compatibility view for surfaces that still read one status: the control
        /// slot leads, matching <c>StatusController.ActiveType</c>. The HUD itself
        /// does not use this — it renders both slots.
        /// </summary>
        public StatusType ActiveStatus => ControlStatus != StatusType.None ? ControlStatus : DamageStatus;

        /// <summary>Seconds left on <see cref="ActiveStatus"/>.</summary>
        public float StatusRemaining =>
            ControlStatus != StatusType.None ? ControlStatusRemaining : DamageStatusRemaining;

        /// <summary>
        /// While true, <see cref="Tick"/> does not advance either status radial.
        /// The HUD sets it from the authoritative Time Freeze state: a frozen
        /// status must not tick down on screen while its real clock is stopped.
        /// </summary>
        public bool StatusClockSuspended { get; set; }

        /// <summary>Clears every slot, the meter, both statuses and every lock.</summary>
        public void Reset() {
            for (int index = 0; index < SlotCount; index++) {
                _remaining[index] = 0f;
                _durations[index] = 0f;
                _locks[index] = AbilitySlotLockState.Clear;
            }
            UltimateMeter = 0f;
            StatusClockSuspended = false;
            ClearStatus();
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

        // ---- Status slots ---------------------------------------------------

        /// <summary>
        /// Applies a status into <b>its own</b> slot, replacing whatever that slot
        /// held. <c>None</c> or a non-positive duration clears that slot instead,
        /// matching <c>StatusController.ApplyStatus</c>'s own guard.
        ///
        /// <para>Crucially it does not touch the other slot: under the old
        /// single-status model a Venom application wiped an active Root off the HUD
        /// even though the player was still rooted.</para>
        /// </summary>
        public void ApplyStatus(StatusType type, float durationSeconds) {
            if (type == StatusType.None) return;
            bool damage = IsDamageStatus(type);
            if (durationSeconds <= 0f) {
                ClearSlot(damage);
                return;
            }
            if (damage) {
                DamageStatus = type;
                DamageStatusRemaining = durationSeconds;
            } else {
                ControlStatus = type;
                ControlStatusRemaining = durationSeconds;
            }
        }

        /// <summary>
        /// Falling edge for a legacy (un-scoped) clear. <c>None</c> means "no status
        /// remains at all", so it clears both. A specific type clears only that
        /// type's slot. <c>StatusController</c> now publishes slot-scoped clears
        /// (Package 12 W10), which go through <see cref="ClearStatusSlot"/>.
        /// </summary>
        public void ClearStatus(StatusType type) {
            if (type == StatusType.None) {
                ClearStatus();
                return;
            }
            bool damage = IsDamageStatus(type);
            if (damage && DamageStatus != type) return;
            if (!damage && ControlStatus != type) return;
            ClearSlot(damage);
        }

        /// <summary>
        /// Package 12 W10: a slot-scoped falling edge. Empties exactly one slot and
        /// leaves the other alone — no survivor re-announce is needed.
        /// </summary>
        public void ClearStatusSlot(bool damageSlot) => ClearSlot(damageSlot);

        /// <summary>Clears both slots.</summary>
        public void ClearStatus() {
            ControlStatus = StatusType.None;
            ControlStatusRemaining = 0f;
            DamageStatus = StatusType.None;
            DamageStatusRemaining = 0f;
        }

        /// <summary>
        /// Overwrites both slots from the authoritative status owner. This is the
        /// F24 path: the radials read a real simulation clock, so a Time Freeze or
        /// a pause stops them for free, and an expiry the event layer swallowed
        /// still disappears from the HUD.
        /// </summary>
        public void SyncStatusFromAuthority(
            StatusType control, float controlRemaining, StatusType damage, float damageRemaining) {
            ControlStatus = controlRemaining > 0f ? control : StatusType.None;
            ControlStatusRemaining = ControlStatus == StatusType.None ? 0f : controlRemaining;
            DamageStatus = damageRemaining > 0f ? damage : StatusType.None;
            DamageStatusRemaining = DamageStatus == StatusType.None ? 0f : damageRemaining;
        }

        /// <summary>The status occupying a slot, or <c>None</c>.</summary>
        public StatusType StatusIn(bool damageSlot) => damageSlot ? DamageStatus : ControlStatus;

        /// <summary>Seconds left in a slot.</summary>
        public float RemainingIn(bool damageSlot) => damageSlot ? DamageStatusRemaining : ControlStatusRemaining;

        /// <summary>
        /// Which slot a status type belongs to. Mirrors
        /// <c>StatusController.IsDamageStatus</c> without making a pure model take a
        /// dependency on <c>FTT.Combat</c> — the two are pinned together by test
        /// rather than by a reference.
        /// </summary>
        public static bool IsDamageStatus(StatusType type) =>
            type is StatusType.Venom or StatusType.RadiantBurn;

        private void ClearSlot(bool damageSlot) {
            if (damageSlot) {
                DamageStatus = StatusType.None;
                DamageStatusRemaining = 0f;
            } else {
                ControlStatus = StatusType.None;
                ControlStatusRemaining = 0f;
            }
        }

        // ---- V7.5 ability slot locks ----------------------------------------

        /// <summary>
        /// Records a slot's lock state. A locked slot is still <b>shown</b> — the
        /// Legacy Unlock Schedule wants the player to see the dormant power they
        /// will get back, and Suppression wants them to see what was taken.
        /// </summary>
        public void SetSlotLock(AbilitySlot slot, AbilitySlotLockState state) {
            int index = IndexOf(slot);
            if (index < 0) return;
            _locks[index] = state;
        }

        public AbilitySlotLockState LockState(AbilitySlot slot) {
            int index = IndexOf(slot);
            return index < 0 ? AbilitySlotLockState.Clear : _locks[index];
        }

        /// <summary>True when the slot is Dormant or Suppressed.</summary>
        public bool IsLocked(AbilitySlot slot) => LockState(slot) != AbilitySlotLockState.Clear;

        // ---- Ticking ---------------------------------------------------------

        /// <summary>
        /// Advances cooldowns and the status fallback timers. Cooldowns always
        /// advance; the status radials do not while
        /// <see cref="StatusClockSuspended"/> is set (F24's frozen-clock rule).
        /// </summary>
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
            if (StatusClockSuspended) return;
            if (ControlStatus != StatusType.None) {
                ControlStatusRemaining -= deltaSeconds;
                if (ControlStatusRemaining <= 0f) ClearSlot(damageSlot: false);
            }
            if (DamageStatus == StatusType.None) return;
            DamageStatusRemaining -= deltaSeconds;
            if (DamageStatusRemaining <= 0f) ClearSlot(damageSlot: true);
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

        /// <summary>
        /// True when the player can actually use the slot right now. A Dormant or
        /// Suppressed slot is never ready however full its cooldown or meter is —
        /// the lock overlay and the readiness tint must agree.
        /// </summary>
        public bool IsReady(AbilitySlot slot) {
            if (IsLocked(slot)) return false;
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

        /// <summary>
        /// Distinct glyph per status. HUD_CONTRACT: "Use distinct status glyphs
        /// plus localized names… color alone must not identify an effect." These
        /// stand in for art the way every other placeholder in the HUD does, but
        /// the <em>shape</em> distinction is a contract requirement rather than a
        /// placeholder detail, so it survives the art pass.
        /// </summary>
        public static string StatusGlyph(StatusType type) => type switch {
            StatusType.TimeDilation => "◑",
            StatusType.Venom => "☠",
            StatusType.StaticCharge => "⚡",
            StatusType.RadiantBurn => "✸",
            StatusType.Root => "⟊",
            _ => ""
        };

        /// <summary>Translation key for a slot lock state's label.</summary>
        public static string LockLabelKey(AbilitySlotLockState state) => state switch {
            AbilitySlotLockState.Dormant => "hud_slot_lock_dormant",
            AbilitySlotLockState.Suppressed => "hud_slot_lock_suppressed",
            _ => "hud_slot_lock_clear"
        };

        /// <summary>
        /// Glyph for a slot lock state. Dormant is a dim star that ignites on the
        /// Resonance Restored beat; Suppressed is a cold cross-out.
        /// </summary>
        public static string LockGlyph(AbilitySlotLockState state) => state switch {
            AbilitySlotLockState.Dormant => "✧",
            AbilitySlotLockState.Suppressed => "✕",
            _ => ""
        };
    }
}
