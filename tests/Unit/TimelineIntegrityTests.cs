using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.1 Timeline Integrity core (the Siphon Clock, tiers, the
/// secret's +5%), the V7 Chronal Rating stamp, the secret-cache counter, the
/// boss intro's once-per-boss memory, and the Chrono-Warden elite's authored
/// contract (~170 HP, 0.5 stun resistance, Dilation Field + Phase Skip).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TimelineIntegrityTests {

    [TestCase]
    public void TheIntegrityTiersDrainRatesAndSecretRestoreMatchTheDesign() {
        AssertThat(TimelineIntegrityRules.Tier(95f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.Tier(90f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.Tier(75f)).IsEqual(1);
        AssertThat(TimelineIntegrityRules.Tier(69.9f)).IsEqual(2);
        AssertThat(TimelineIntegrityRules.TierKey(92f)).IsEqual("integrity_tier_restored");
        AssertThat(TimelineIntegrityRules.TierKey(72f)).IsEqual("integrity_tier_stabilized");
        AssertThat(TimelineIntegrityRules.TierKey(40f)).IsEqual("integrity_tier_fractured");
        AssertThat(TimelineIntegrityRules.DrainPerSecond(Difficulty.Normal)).IsEqual(0.1f);
        AssertThat(TimelineIntegrityRules.DrainPerSecond(Difficulty.Hard)).IsEqual(0.2f);
        AssertThat(TimelineIntegrityRules.SecretRestorePercent).IsEqual(5f);
        // The tier dust bonus is authored but deliberately unapplied (economy
        // pass deferred) — the rule still records the design's numbers.
        AssertThat(TimelineIntegrityRules.DustBonusPercent(95f)).IsEqual(10);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(75f)).IsEqual(5);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(40f)).IsEqual(0);
    }

    [TestCase]
    public void TheChronalRatingRewardsCleanRunsAndForgivesNothingItShouldNot() {
        // A flawless run: full integrity, no rewinds, secret found, under par.
        AssertThat(ChronalRatingRules.Compute(100f, 0, 1, 1, 300f)).IsEqual("S");
        // Strong but imperfect runs land A/B.
        AssertThat(ChronalRatingRules.Compute(92f, 1, 1, 1, 900f)).IsEqual("A");
        AssertThat(ChronalRatingRules.Compute(75f, 2, 0, 1, 900f)).IsEqual("B");
        // A fractured, rewind-heavy, secretless slog is a C.
        AssertThat(ChronalRatingRules.Compute(40f, 5, 0, 1, 2000f)).IsEqual("C");
    }

    [TestCase]
    public void TheSiphonClockDrainsPerExtractorAndTheSecretRestoresFive() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        story.BeginLevelRun();
        AssertThat(story.TimelineIntegrityPercent).IsEqual(100f);

        // Two engaged extractors for 10 seconds at Normal: 2 x 0.1 x 10 = 2%.
        story.DrainTimelineIntegrity(2, 10f);
        AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(98f, 0.01f);

        story.RegisterSecretFound();
        AssertThat(story.LevelSecretsFound).IsEqual(1);
        AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(100f, 0.01f);

        // A fresh attempt resets both.
        story.BeginLevelRun();
        AssertThat(story.TimelineIntegrityPercent).IsEqual(100f);
        AssertThat(story.LevelSecretsFound).IsEqual(0);
        story.StopLevelRun();
    }

    [TestCase]
    public void ASecretCacheCountsOnceAndTheBossIntroPlaysOncePerBoss() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        story.BeginLevelRun();
        var cache = new SecretCache { Name = "TestSecret", SecretID = "test_secret" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(cache);
        try {
            AssertThat(cache.Discover()).IsTrue();
            AssertThat(cache.Discover())
                .OverrideFailureMessage("A secret counts once per attempt.")
                .IsFalse();
            AssertThat(story.LevelSecretsFound).IsEqual(1);

            AssertThat(story.HasSeenBossIntro("test_boss")).IsFalse();
            story.RecordBossIntroSeen("test_boss");
            AssertThat(story.HasSeenBossIntro("test_boss"))
                .OverrideFailureMessage("The intro's seen-set survives for retry skips.")
                .IsTrue();
        } finally {
            story.StopLevelRun();
            cache.Free();
        }
    }

    [TestCase]
    public void TheChronoWardenMatchesItsAuthoredEliteContract() {
        EnemyData warden = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_warden.tres");
        AssertThat(warden).IsNotNull();
        AssertThat(warden.Tier).IsEqual(EnemyTier.Elite);
        AssertThat(warden.MaxHP).IsEqual(170);
        AssertThat(warden.StunResistance).IsEqualApprox(0.5f, 0.001f);
        AssertThat(warden.HasEliteAbilities).IsTrue();
        AssertThat(warden.EliteAbilities.Length).IsEqual(2);

        EnemyAbilityData dilation = warden.EliteAbilities[0];
        AssertThat(dilation.Archetype).IsEqual(EnemyAbilityArchetype.AreaPulse);
        AssertThat(dilation.AppliedStatus).IsEqual(StatusType.TimeDilation);
        AssertThat(dilation.StatusDuration > 0f).IsTrue();

        EnemyAbilityData phaseSkip = warden.EliteAbilities[1];
        AssertThat(phaseSkip.Archetype).IsEqual(EnemyAbilityArchetype.Teleport);
    }
}
