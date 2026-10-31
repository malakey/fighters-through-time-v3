using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B1. The Story HUD's ability-readiness and status model.
///
/// The trap this pins is the ultimate slot. Three of the four slots are cooldown
/// gated and every kit raises <c>OnCooldownStarted</c> for them; the ultimate has
/// no cooldown at all and is gated purely by the Influence meter, so a model that
/// treats all four the same produces an ultimate icon that is permanently "ready"
/// (nothing ever arms it) or permanently dark (something wrongly does).
/// </summary>
[TestSuite]
public class HudAbilityIndicatorModelTests {

    [TestCase]
    public void EveryCooldownSlotStartsReadyAndTheUltimateStartsUnready() {
        var model = new HudAbilityIndicatorModel();

        AssertThat(model.IsReady(AbilitySlot.Special1)).IsTrue();
        AssertThat(model.IsReady(AbilitySlot.Special2)).IsTrue();
        AssertThat(model.IsReady(AbilitySlot.MovementAbility)).IsTrue();
        // An empty meter cannot fire an ultimate.
        AssertThat(model.IsReady(AbilitySlot.Ultimate)).IsFalse();
        AssertFloat(model.ReadinessFraction(AbilitySlot.Ultimate)).IsEqual(0f);
    }

    [TestCase]
    public void ASpecialsReuseDelayIsTwoSecondsAndUnderOnePipItIsStarvedUnlessLocked() {
        var model = new HudAbilityIndicatorModel();
        // A fresh model holds the start fill of two pips.
        AssertFloat(model.MomentumPips).IsEqual(2f);
        AssertFloat(HudAbilityIndicatorModel.SpecialReuseDelaySeconds).IsEqualApprox(2f, 0.0001f);

        // The sweep reads remaining / 2 s whatever the authored cooldown was.
        model.StartCooldown(AbilitySlot.Special1, 10f);
        model.SyncReuseDelay(AbilitySlot.Special1, 0.5f);
        AssertFloat(model.RemainingSeconds(AbilitySlot.Special1)).IsEqualApprox(0.5f, 0.0001f);
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqualApprox(0.75f, 0.0001f);
        model.SyncReuseDelay(AbilitySlot.Special1, 0f);
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsTrue();
        // The movement ability is not a reuse-delay slot.
        model.SyncReuseDelay(AbilitySlot.MovementAbility, 1f);
        AssertFloat(model.RemainingSeconds(AbilitySlot.MovementAbility)).IsEqual(0f);

        // Under one pip a Special is starved and not ready; the movement ability is unaffected; a locked slot is not starved.
        model.SyncMomentum(0.99f);
        AssertThat(model.IsStarved(AbilitySlot.Special2)).IsTrue();
        AssertThat(model.IsReady(AbilitySlot.Special2)).IsFalse();
        AssertThat(model.IsStarved(AbilitySlot.MovementAbility)).IsFalse();
        AssertThat(model.IsReady(AbilitySlot.MovementAbility)).IsTrue();
        model.SetSlotLock(AbilitySlot.Special2, AbilitySlotLockState.Dormant);
        AssertThat(model.IsStarved(AbilitySlot.Special2)).IsFalse();
        model.SyncMomentum(1f);
        AssertThat(model.IsStarved(AbilitySlot.Special1)).IsFalse();
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsTrue();
        model.SyncMomentum(99f);
        AssertFloat(model.MomentumPips).IsEqual(4f);
    }

    [TestCase]
    public void ACooldownCountsDownAndReleasesTheSlotExactlyOnce() {
        var model = new HudAbilityIndicatorModel();

        model.StartCooldown(AbilitySlot.Special1, 10f);
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsFalse();
        AssertFloat(model.RemainingSeconds(AbilitySlot.Special1)).IsEqual(10f);
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqual(0f);

        model.Tick(5f);
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqualApprox(0.5f, 0.0001f);
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsFalse();

        model.Tick(5.5f);
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsTrue();
        AssertFloat(model.RemainingSeconds(AbilitySlot.Special1)).IsEqual(0f);
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqual(1f);

        // Other slots are untouched by one slot's cooldown.
        AssertThat(model.IsReady(AbilitySlot.Special2)).IsTrue();
        AssertThat(model.IsReady(AbilitySlot.MovementAbility)).IsTrue();
    }

    /// <summary>
    /// 2026-10-11 (PROVISIONAL-HIT-CONFIRM-RULES (9)): the Story HUD now writes
    /// each cooldown's remaining time from the hero's own timer every frame, so
    /// a per-hit Momentum refund, a Time Freeze pause or the Story cooldown
    /// multiplier reach the radial. The denominator the cast armed is kept and
    /// only ever raised.
    /// </summary>
    [TestCase]
    public void ASyncedCooldownKeepsItsDenominatorAndFollowsARefund() {
        var model = new HudAbilityIndicatorModel();

        model.StartCooldown(AbilitySlot.Special1, 10f);
        model.SyncCooldownRemaining(AbilitySlot.Special1, 6f);
        AssertFloat(model.RemainingSeconds(AbilitySlot.Special1)).IsEqual(6f);
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqualApprox(0.4f, 0.0001f);
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsFalse();

        // A refund that empties the timer readies the slot at once.
        model.SyncCooldownRemaining(AbilitySlot.Special1, 0f);
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsTrue();
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqual(1f);

        // An authority above the denominator (a cooldown restored on load, which
        // raises no start event) becomes the denominator.
        model.SyncCooldownRemaining(AbilitySlot.Special1, 12f);
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqual(0f);
        model.SyncCooldownRemaining(AbilitySlot.Special1, 3f);
        AssertFloat(model.ReadinessFraction(AbilitySlot.Special1)).IsEqualApprox(0.75f, 0.0001f);

        // The other slots are untouched, and the Ultimate never takes a cooldown.
        AssertThat(model.IsReady(AbilitySlot.Special2)).IsTrue();
        model.SetUltimateMeter(HudAbilityIndicatorModel.UltimateReadyMeter);
        model.SyncCooldownRemaining(AbilitySlot.Ultimate, 5f);
        AssertThat(model.IsReady(AbilitySlot.Ultimate)).IsTrue();
        AssertFloat(model.RemainingSeconds(AbilitySlot.Ultimate)).IsEqual(0f);
    }

    [TestCase]
    public void TheUltimateIsGatedByTheMeterAndNeverByACooldownPayload() {
        var model = new HudAbilityIndicatorModel();

        // A stray cooldown payload on the ultimate slot must not blank an icon the
        // player can legitimately press.
        model.StartCooldown(AbilitySlot.Ultimate, 30f);
        AssertFloat(model.RemainingSeconds(AbilitySlot.Ultimate)).IsEqual(0f);

        model.SetUltimateMeter(50f);
        AssertThat(model.IsReady(AbilitySlot.Ultimate)).IsFalse();
        AssertFloat(model.ReadinessFraction(AbilitySlot.Ultimate)).IsEqualApprox(0.5f, 0.0001f);

        model.SetUltimateMeter(HudAbilityIndicatorModel.UltimateReadyMeter);
        AssertThat(model.IsReady(AbilitySlot.Ultimate)).IsTrue();
        AssertFloat(model.ReadinessFraction(AbilitySlot.Ultimate)).IsEqual(1f);

        // Using the ultimate resets the meter, which must darken the icon again.
        model.SetUltimateMeter(0f);
        AssertThat(model.IsReady(AbilitySlot.Ultimate)).IsFalse();

        // Out-of-range and NaN payloads cannot push the meter past full.
        model.SetUltimateMeter(400f);
        AssertFloat(model.UltimateMeter).IsEqual(HudAbilityIndicatorModel.UltimateReadyMeter);
        model.SetUltimateMeter(float.NaN);
        AssertFloat(model.UltimateMeter).IsEqual(HudAbilityIndicatorModel.UltimateReadyMeter);
    }

    /// <summary>
    /// F24: newest-wins is a per-SLOT rule, not a per-model one. Venom replacing
    /// Venom is still an outright replacement; Venom arriving beside Root is not.
    /// </summary>
    [TestCase]
    public void TheNewestStatusReplacesThePreviousOneInItsOwnSlotOnly() {
        var model = new HudAbilityIndicatorModel();

        model.ApplyStatus(StatusType.Venom, 5f);
        AssertThat(model.DamageStatus).IsEqual(StatusType.Venom);
        AssertFloat(model.DamageStatusRemaining).IsEqual(5f);
        AssertThat(model.ControlStatus).IsEqual(StatusType.None);

        // Same slot: newest wins outright.
        model.ApplyStatus(StatusType.RadiantBurn, 2f);
        AssertThat(model.DamageStatus).IsEqual(StatusType.RadiantBurn);
        AssertFloat(model.DamageStatusRemaining).IsEqual(2f);

        // Other slot: coexists. The compat single-status view still leads with
        // control, matching StatusController.ActiveType.
        model.ApplyStatus(StatusType.Root, 3f);
        AssertThat(model.ControlStatus).IsEqual(StatusType.Root);
        AssertThat(model.DamageStatus).IsEqual(StatusType.RadiantBurn);
        AssertThat(model.ActiveStatus).IsEqual(StatusType.Root);

        model.ClearStatus(StatusType.Root);
        AssertThat(model.ControlStatus).IsEqual(StatusType.None);
        AssertThat(model.DamageStatus).IsEqual(StatusType.RadiantBurn);

        model.ClearStatus();
        AssertThat(model.DamageStatus).IsEqual(StatusType.None);
        AssertFloat(model.StatusRemaining).IsEqual(0f);
    }

    [TestCase]
    public void AStatusWhoseClearEventNeverArrivesStillExpiresLocally() {
        var model = new HudAbilityIndicatorModel();
        model.ApplyStatus(StatusType.RadiantBurn, 1f);

        model.Tick(0.5f);
        AssertThat(model.DamageStatus).IsEqual(StatusType.RadiantBurn);

        // The bus falling edge is the primary source; this timer is the fallback
        // for a status whose owner left the tree mid-effect.
        model.Tick(0.6f);
        AssertThat(model.DamageStatus).IsEqual(StatusType.None);
    }

    /// <summary>
    /// F24: "Radials follow authoritative simulation clocks, including pauses and
    /// Time Freeze; the HUD must not independently count down a frozen status."
    /// Cooldowns are unaffected — they are not what Time Freeze stops.
    /// </summary>
    [TestCase]
    public void ASuspendedStatusClockDoesNotTickDownWhileCooldownsStillDo() {
        var model = new HudAbilityIndicatorModel();
        model.ApplyStatus(StatusType.Venom, 5f);
        model.StartCooldown(AbilitySlot.Special1, 4f);

        model.StatusClockSuspended = true;
        model.Tick(2f);
        AssertFloat(model.DamageStatusRemaining).IsEqual(5f);
        AssertFloat(model.RemainingSeconds(AbilitySlot.Special1)).IsEqual(2f);

        model.StatusClockSuspended = false;
        model.Tick(2f);
        AssertFloat(model.DamageStatusRemaining).IsEqual(3f);
    }

    /// <summary>
    /// The authoritative sync is what the Story HUD actually renders from: it
    /// overwrites both slots wholesale, so an expiry the event layer missed still
    /// disappears from the HUD. (Package 12 W10 made the events slot-scoped, so
    /// the pre-W10 survivor re-announce no longer swallows one.)
    /// </summary>
    [TestCase]
    public void SyncingFromTheAuthorityOverwritesBothSlots() {
        var model = new HudAbilityIndicatorModel();
        model.ApplyStatus(StatusType.Venom, 5f);
        model.ApplyStatus(StatusType.Root, 5f);

        model.SyncStatusFromAuthority(StatusType.Root, 1.5f, StatusType.None, 0f);
        AssertThat(model.ControlStatus).IsEqual(StatusType.Root);
        AssertFloat(model.ControlStatusRemaining).IsEqual(1.5f);
        AssertThat(model.DamageStatus).IsEqual(StatusType.None);

        // A zero remaining is an empty slot however the type reads.
        model.SyncStatusFromAuthority(StatusType.Root, 0f, StatusType.Venom, 2f);
        AssertThat(model.ControlStatus).IsEqual(StatusType.None);
        AssertThat(model.DamageStatus).IsEqual(StatusType.Venom);
    }

    /// <summary>
    /// V7.5 slot locks: a locked slot is never "ready", however full its cooldown
    /// or meter is, so the overlay and the readiness tint cannot disagree.
    /// </summary>
    [TestCase]
    public void ALockedSlotIsNeverReadyAndClearingTheLockRestoresIt() {
        var model = new HudAbilityIndicatorModel();
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsTrue();

        model.SetSlotLock(AbilitySlot.Special1, AbilitySlotLockState.Dormant);
        AssertThat(model.LockState(AbilitySlot.Special1)).IsEqual(AbilitySlotLockState.Dormant);
        AssertThat(model.IsLocked(AbilitySlot.Special1)).IsTrue();
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsFalse();

        model.SetUltimateMeter(100f);
        model.SetSlotLock(AbilitySlot.Ultimate, AbilitySlotLockState.Suppressed);
        AssertThat(model.IsReady(AbilitySlot.Ultimate)).IsFalse();

        model.SetSlotLock(AbilitySlot.Special1, AbilitySlotLockState.Clear);
        AssertThat(model.IsReady(AbilitySlot.Special1)).IsTrue();
    }

    [TestCase]
    public void ApplyingNoneOrANonPositiveDurationClearsInsteadOfLatching() {
        var model = new HudAbilityIndicatorModel();
        model.ApplyStatus(StatusType.StaticCharge, 3f);

        model.ApplyStatus(StatusType.None, 3f);
        // None names no slot, so it latches nothing — and must not silently wipe
        // the StaticCharge that IS active.
        AssertThat(model.ControlStatus).IsEqual(StatusType.StaticCharge);

        model.ApplyStatus(StatusType.Venom, 4f);
        model.ApplyStatus(StatusType.Venom, 0f);
        AssertThat(model.DamageStatus).IsEqual(StatusType.None);
    }

    [TestCase]
    public void ResetReturnsEverySlotTheMeterAndTheStatusToTheirStartState() {
        var model = new HudAbilityIndicatorModel();
        model.StartCooldown(AbilitySlot.Special2, 8f);
        model.SetUltimateMeter(100f);
        model.ApplyStatus(StatusType.TimeDilation, 6f);

        model.Reset();

        AssertThat(model.IsReady(AbilitySlot.Special2)).IsTrue();
        AssertThat(model.IsReady(AbilitySlot.Ultimate)).IsFalse();
        AssertThat(model.ActiveStatus).IsEqual(StatusType.None);
        AssertThat(model.LockState(AbilitySlot.Special2)).IsEqual(AbilitySlotLockState.Clear);
        AssertFloat(model.UltimateMeter).IsEqual(0f);
    }

    [TestCase]
    public void EachSlotAndStatusNamesAnExistingTranslationKeyFamily() {
        // The status pip reuses the shared status_* family rather than a HUD-local
        // one, so the HUD and any other surface naming a status cannot drift.
        AssertString(HudAbilityIndicatorModel.SlotLabelKey(AbilitySlot.Special1)).IsEqual("hud_slot_special1");
        AssertString(HudAbilityIndicatorModel.SlotLabelKey(AbilitySlot.Special2)).IsEqual("hud_slot_special2");
        AssertString(HudAbilityIndicatorModel.SlotLabelKey(AbilitySlot.MovementAbility)).IsEqual("hud_slot_movement");
        AssertString(HudAbilityIndicatorModel.SlotLabelKey(AbilitySlot.Ultimate)).IsEqual("hud_slot_ultimate");

        AssertString(HudAbilityIndicatorModel.StatusLabelKey(StatusType.TimeDilation)).IsEqual("status_timedilation");
        AssertString(HudAbilityIndicatorModel.StatusLabelKey(StatusType.StaticCharge)).IsEqual("status_staticcharge");
        AssertString(HudAbilityIndicatorModel.StatusLabelKey(StatusType.RadiantBurn)).IsEqual("status_radiantburn");
        AssertString(HudAbilityIndicatorModel.StatusLabelKey(StatusType.Venom)).IsEqual("status_venom");
        AssertString(HudAbilityIndicatorModel.StatusLabelKey(StatusType.Root)).IsEqual("status_root");
    }
}
