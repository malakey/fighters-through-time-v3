using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit M-31: the haptic patterns must match the design trigger table
/// (design-godot.md:1731-1741). The magnitudes live as named constants on
/// <see cref="HapticFeedbackManager"/> precisely so this suite can pin the table
/// in one place; call sites ask for named events and can no longer drift.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HapticPatternTests {

    [TestCase]
    public void TheDesignTableMagnitudesAreExact() {
        // Taking Damage: 0.4 / 0.3 / 100 ms (was 0.3/0.5/80 ms).
        AssertThat(HapticFeedbackManager.TakingDamageWeak).IsEqual(0.4f);
        AssertThat(HapticFeedbackManager.TakingDamageStrong).IsEqual(0.3f);
        AssertThat(HapticFeedbackManager.TakingDamageSeconds).IsEqual(0.1f);

        // Guard Impact: 0.2 / 0.4 / 60 ms.
        AssertThat(HapticFeedbackManager.GuardImpactWeak).IsEqual(0.2f);
        AssertThat(HapticFeedbackManager.GuardImpactStrong).IsEqual(0.4f);
        AssertThat(HapticFeedbackManager.GuardImpactSeconds).IsEqual(0.06f);

        // Guard Break: 0.8 / 0.9 / 200 ms.
        AssertThat(HapticFeedbackManager.GuardBreakWeak).IsEqual(0.8f);
        AssertThat(HapticFeedbackManager.GuardBreakStrong).IsEqual(0.9f);
        AssertThat(HapticFeedbackManager.GuardBreakSeconds).IsEqual(0.2f);

        // Heavy Landing (fall > 3 units): 0.5 / 0.2 / 100 ms.
        AssertThat(HapticFeedbackManager.HeavyLandingWeak).IsEqual(0.5f);
        AssertThat(HapticFeedbackManager.HeavyLandingStrong).IsEqual(0.2f);
        AssertThat(HapticFeedbackManager.HeavyLandingSeconds).IsEqual(0.1f);
        AssertThat(HapticFeedbackManager.HeavyLandingFallUnits).IsEqual(3.0f);

        // Stage Hazard Hit: 0.3 / 0.6 / 150 ms — distinct from Taking Damage.
        AssertThat(HapticFeedbackManager.StageHazardWeak).IsEqual(0.3f);
        AssertThat(HapticFeedbackManager.StageHazardStrong).IsEqual(0.6f);
        AssertThat(HapticFeedbackManager.StageHazardSeconds).IsEqual(0.15f);

        // KO / Death: full intensity, 300 ms (was 500 ms).
        AssertThat(HapticFeedbackManager.KnockoutWeak).IsEqual(1.0f);
        AssertThat(HapticFeedbackManager.KnockoutStrong).IsEqual(1.0f);
        AssertThat(HapticFeedbackManager.KnockoutSeconds).IsEqual(0.3f);
    }

    [TestCase]
    public void TheUltimateRampRunsFromPointFourToFullOverHalfASecond() {
        AssertThat(HapticFeedbackManager.UltimateRampStart).IsEqual(0.4f);
        AssertThat(HapticFeedbackManager.UltimateRampEnd).IsEqual(1.0f);
        AssertThat(HapticFeedbackManager.UltimateRampSeconds).IsEqual(0.5f);

        AssertThat(HapticFeedbackManager.UltimateRampMagnitude(0f)).IsEqualApprox(0.4f, 0.001f);
        AssertThat(HapticFeedbackManager.UltimateRampMagnitude(0.25f)).IsEqualApprox(0.7f, 0.001f);
        AssertThat(HapticFeedbackManager.UltimateRampMagnitude(0.5f)).IsEqualApprox(1.0f, 0.001f);
        // Past the window the ramp holds full rather than extrapolating.
        AssertThat(HapticFeedbackManager.UltimateRampMagnitude(2f)).IsEqualApprox(1.0f, 0.001f);
        AssertThat(HapticFeedbackManager.UltimateRampMagnitude(-1f)).IsEqualApprox(0.4f, 0.001f);
    }

    /// <summary>
    /// The staged ramp only exists for a device that can vibrate: the keyboard and
    /// unassigned-slot sentinels are refused, so no pulse loop spins for a player
    /// who owns no pad.
    /// </summary>
    [TestCase]
    public void UltimateRampsAreNeverArmedForSentinelDevices() {
        HapticFeedbackManager haptics = HapticFeedbackManager.Instance;
        AssertObject(haptics).IsNotNull();
        haptics.OnUltimateActivation(InputManager.UnassignedDevice);
        haptics.OnUltimateActivation(InputManager.KeyboardDevice);
        AssertThat(haptics.ActiveUltimateRampCount).IsEqual(0);
    }

    /// <summary>The named events are safe no-ops for slots that own no device.</summary>
    [TestCase]
    public void NamedEventsAreSafeForUnownedSlots() {
        HapticFeedbackManager haptics = HapticFeedbackManager.Instance;
        AssertObject(haptics).IsNotNull();
        haptics.OnTakingDamage(-1);
        haptics.OnGuardImpact(-1);
        haptics.OnHeavyLanding(-1);
        haptics.OnStageHazardHit(-1);
        haptics.OnKnockout(-1);
    }
}
