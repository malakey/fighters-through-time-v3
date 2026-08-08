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

    [TestCase]
    public void TheNewestStatusCompletelyReplacesThePreviousOne() {
        var model = new HudAbilityIndicatorModel();

        model.ApplyStatus(StatusType.Venom, 5f);
        AssertThat(model.ActiveStatus).IsEqual(StatusType.Venom);
        AssertFloat(model.StatusRemaining).IsEqual(5f);

        // Project rule: one status at a time, no stacking, newest wins outright.
        model.ApplyStatus(StatusType.Root, 2f);
        AssertThat(model.ActiveStatus).IsEqual(StatusType.Root);
        AssertFloat(model.StatusRemaining).IsEqual(2f);

        model.ClearStatus();
        AssertThat(model.ActiveStatus).IsEqual(StatusType.None);
        AssertFloat(model.StatusRemaining).IsEqual(0f);
    }

    [TestCase]
    public void AStatusWhoseClearEventNeverArrivesStillExpiresLocally() {
        var model = new HudAbilityIndicatorModel();
        model.ApplyStatus(StatusType.RadiantBurn, 1f);

        model.Tick(0.5f);
        AssertThat(model.ActiveStatus).IsEqual(StatusType.RadiantBurn);

        // The bus falling edge is the primary source; this timer is the fallback
        // for a status whose owner left the tree mid-effect.
        model.Tick(0.6f);
        AssertThat(model.ActiveStatus).IsEqual(StatusType.None);
    }

    [TestCase]
    public void ApplyingNoneOrANonPositiveDurationClearsInsteadOfLatching() {
        var model = new HudAbilityIndicatorModel();
        model.ApplyStatus(StatusType.StaticCharge, 3f);

        model.ApplyStatus(StatusType.None, 3f);
        AssertThat(model.ActiveStatus).IsEqual(StatusType.None);

        model.ApplyStatus(StatusType.Venom, 4f);
        model.ApplyStatus(StatusType.Venom, 0f);
        AssertThat(model.ActiveStatus).IsEqual(StatusType.None);
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
