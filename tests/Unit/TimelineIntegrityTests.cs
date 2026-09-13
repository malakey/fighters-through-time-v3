using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins <b>Timeline Integrity as the level timer</b> (V7.6 F01, Package 11 A3):
/// the normalized drain equation, the denominator fixed at level load, the
/// pause set, the PreBoss freeze, the clamp at zero and the collapse it fires,
/// the at-par arithmetic, and the 50/20 tier lines. Also pins the two rules
/// that are easiest to break by accident — destroying a machine never moves
/// the gauge, and a found secret only lowers the future RATE.
///
/// The V7.1/V7.3 siphon model this replaces (the 10% share, the 10 s grace,
/// the 600 px engagement, the +3/+2/+5 restoration paths, the 90/70 lines,
/// the Hard-doubles flat rate) and the Chronal Rating stamp are retired
/// outright, so their four cases are gone rather than retuned.
///
/// The secret-cache counter, the boss intro's once-per-boss memory and the
/// Chrono-Warden elite contract are unchanged and stay here.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TimelineIntegrityTests {

    private const float Par = 600f;

    [TestCase]
    public void TheDrainEquationNormalizesAgainstParAndTheLivingMachines() {
        // 100 / (par * multiplier) * currentFactor / initialFactor.
        // Three machines alive, none broken, no secret: the factors cancel and
        // the rate is exactly the all-alive normalized rate.
        AssertThat(TimelineIntegrityRules.DifficultyTimeMultiplier(Difficulty.Easy)).IsEqual(2.0f);
        AssertThat(TimelineIntegrityRules.DifficultyTimeMultiplier(Difficulty.Normal)).IsEqual(1.5f);
        AssertThat(TimelineIntegrityRules.DifficultyTimeMultiplier(Difficulty.Hard)).IsEqual(1.2f);

        AssertThat(TimelineIntegrityRules.InitialDrainFactor(3)).IsEqualApprox(1.6f, 0.0001f);
        AssertThat(TimelineIntegrityRules.CurrentDrainFactor(3, false)).IsEqualApprox(1.6f, 0.0001f);
        AssertThat(TimelineIntegrityRules.DrainPerSecond(Par, Difficulty.Normal, 3, 3, false))
            .IsEqualApprox(TimelineIntegrityRules.AllAliveDrainPerSecond(Par, Difficulty.Normal), 0.000001f);

        // Break one: the numerator drops 0.2, the denominator does not move.
        AssertThat(TimelineIntegrityRules.DrainPerSecond(Par, Difficulty.Normal, 2, 3, false))
            .IsEqualApprox(100f / (Par * 1.5f) * 1.4f / 1.6f, 0.000001f);
        // Find the secret as well: another 0.1 off the numerator, permanently.
        AssertThat(TimelineIntegrityRules.DrainPerSecond(Par, Difficulty.Normal, 2, 3, true))
            .IsEqualApprox(100f / (Par * 1.5f) * 1.3f / 1.6f, 0.000001f);

        // The raw-factor floor: an all-broken one-machine level with the secret
        // found would fall to 1.0 - it floors at 0.9.
        AssertThat(TimelineIntegrityRules.MinimumRawFactor).IsEqual(0.9f);
        AssertThat(TimelineIntegrityRules.CurrentDrainFactor(0, true)).IsEqualApprox(0.9f, 0.0001f);

        // An untimed level (par 0) never drains.
        AssertThat(TimelineIntegrityRules.DrainPerSecond(0f, Difficulty.Normal, 3, 3, false)).IsEqual(0f);
    }

    [TestCase]
    public void TheDenominatorIsFixedAtLoadAndNeverRecalculatedFromSurvivors() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(Par, startingExtractors: 3);
            AssertThat(story.StartingExtractorCount).IsEqual(3);
            AssertThat(story.LivingExtractorCount).IsEqual(3);

            story.NotifyExtractorDestroyed();
            story.RecordExtractorDestroyed("fixed_denominator_extractor");
            AssertThat(story.LivingExtractorCount).IsEqual(2);

            // The resume path re-arms the clock from the SAME authored
            // population. If the denominator were recomputed from survivors,
            // the rate would snap back to the all-alive rate and the break the
            // player already paid for would be silently refunded.
            story.BeginIntegrityClock(Par, startingExtractors: 3);
            AssertThat(story.StartingExtractorCount)
                .OverrideFailureMessage("The F01 denominator is the AUTHORED population, always.")
                .IsEqual(3);
            AssertThat(story.LivingExtractorCount)
                .OverrideFailureMessage("A resume must keep the attempt's broken machines broken.")
                .IsEqual(2);
            AssertThat(story.CurrentIntegrityDrainPerSecond)
                .IsEqualApprox(100f / (Par * 1.5f) * 1.4f / 1.6f, 0.000001f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ThePauseSetStopsTheClockAndReleasesAreIdempotent() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(Par, startingExtractors: 0);
            Pump(story, seconds: 10f);
            float afterTenSeconds = story.TimelineIntegrityPercent;
            AssertThat(afterTenSeconds).IsLess(100f);

            foreach (IntegrityClockPause scope in new[] {
                IntegrityClockPause.Dialogue, IntegrityClockPause.PauseMenu,
                IntegrityClockPause.DeathRewind, IntegrityClockPause.BossIntro
            }) {
                story.SetIntegrityClockPause(scope, true);
                AssertThat(story.IsIntegrityClockPaused).IsTrue();
                AssertThat(story.CurrentIntegrityDrainPerSecond).IsEqual(0f);
                Pump(story, seconds: 30f);
                AssertThat(story.TimelineIntegrityPercent)
                    .OverrideFailureMessage($"Frozen presentation ({scope}) must not cost the player time.")
                    .IsEqualApprox(afterTenSeconds, 0.0001f);
                // A mask, not a counter: releasing twice is harmless.
                story.SetIntegrityClockPause(scope, false);
                story.SetIntegrityClockPause(scope, false);
                AssertThat(story.IsIntegrityClockPaused).IsFalse();
            }

            // And it runs again the moment play resumes.
            Pump(story, seconds: 5f);
            AssertThat(story.TimelineIntegrityPercent).IsLess(afterTenSeconds);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ThePreBossFractureFreezesTheGaugePermanently() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(Par, startingExtractors: 0);
            Pump(story, seconds: 30f);
            float atLock = story.TimelineIntegrityPercent;

            story.LockIntegrityAtPreBoss();
            AssertThat(story.IsPreBossLocked).IsTrue();
            AssertThat(story.IsIntegrityClockRunning).IsFalse();
            AssertThat(story.CheckpointIntegrityPercent)
                .OverrideFailureMessage("The lock banks the level's final Integrity in the same transaction.")
                .IsEqualApprox(atLock, 0.0001f);

            Pump(story, seconds: 120f);
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("The boss fight is untimed: the locked gauge never moves again.")
                .IsEqualApprox(atLock, 0.0001f);
            // Even the explicit sink refuses once the clock is locked.
            AssertThat(story.DrainTimelineIntegrityAmount(10f)).IsEqual(0f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void TheClockClampsAtZeroAndCollapsesExactlyOnce() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        int collapses = 0;
        try {
            story.BeginLevelRun();
            // A brutally short par so the pin need not pump ten minutes.
            story.BeginIntegrityClock(2f, startingExtractors: 0);
            story.ActIIICollapseOverride = () => collapses++;

            Pump(story, seconds: 10f);
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("The gauge clamps at zero; it never goes negative.")
                .IsEqual(0f);
            AssertThat(collapses)
                .OverrideFailureMessage("Zero Integrity must fire a Timeline Collapse.")
                .IsEqual(1);

            // Keep pumping: the collapse is a one-shot, not a per-frame storm.
            Pump(story, seconds: 10f);
            AssertThat(collapses).IsEqual(1);
            AssertThat(story.PendingCollapseCause).IsEqual(TimelineCollapseCause.Timer);
        } finally {
            story.ActIIICollapseOverride = null;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AParRunWithNoDetoursEndsAtFiftyThirtyThreeAndSixteenPercent() {
        // The headline arithmetic. At exactly par, every machine alive and no
        // secret, the remaining gauge is 100 - 100/multiplier.
        foreach ((Difficulty difficulty, float expected) in new[] {
            (Difficulty.Easy, 50f), (Difficulty.Normal, 100f / 3f), (Difficulty.Hard, 100f / 6f)
        }) {
            float rate = TimelineIntegrityRules.DrainPerSecond(Par, difficulty, 3, 3, false);
            float remaining = 100f - rate * Par;
            AssertThat(remaining)
                .OverrideFailureMessage($"A par run on {difficulty} must end at {expected:0.00}%.")
                .IsEqualApprox(expected, 0.001f);
        }
    }

    [TestCase]
    public void TheTierLinesAreFiftyAndTwentyAndTheEndingThresholdFollowsThem() {
        AssertThat(TimelineIntegrityRules.RestoredThreshold).IsEqual(50f);
        AssertThat(TimelineIntegrityRules.StabilizedThreshold).IsEqual(20f);
        AssertThat(TimelineIntegrityRules.EndingThresholdPercent).IsEqual(50f);

        AssertThat(TimelineIntegrityRules.Tier(100f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.Tier(50f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.Tier(49.9f)).IsEqual(1);
        AssertThat(TimelineIntegrityRules.Tier(20f)).IsEqual(1);
        AssertThat(TimelineIntegrityRules.Tier(19.9f)).IsEqual(2);

        AssertThat(TimelineIntegrityRules.TierKey(60f)).IsEqual("integrity_tier_restored");
        AssertThat(TimelineIntegrityRules.TierKey(30f)).IsEqual("integrity_tier_stabilized");
        AssertThat(TimelineIntegrityRules.TierKey(5f)).IsEqual("integrity_tier_fractured");

        AssertThat(TimelineIntegrityRules.DustBonusPercent(60f)).IsEqual(10);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(30f)).IsEqual(5);
        AssertThat(TimelineIntegrityRules.DustBonusPercent(5f)).IsEqual(0);
    }

    [TestCase]
    public void DestroyingAMachineSlowsTheRateAndNeverMovesTheGauge() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        var extractor = new ChronalExtractor {
            Name = "F01TestExtractor",
            ObjectID = "f01_test_extractor",
            // Park the discharge cycle: this case is about the clock.
            SafeWindowSeconds = 100000f
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(extractor);
        int walletBefore = story.ChronalDustCollected;
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(Par, startingExtractors: 3);
            Pump(story, seconds: 30f);
            float beforeBreak = story.TimelineIntegrityPercent;
            float rateBefore = story.CurrentIntegrityDrainPerSecond;

            extractor.TakeEnvironmentDamage(extractor.MaxHP);
            AssertThat(extractor.IsDestroyed).IsTrue();

            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("F01 is explicit: destruction buys FUTURE time, never a refill.")
                .IsEqualApprox(beforeBreak, 0.0001f);
            AssertThat(story.LivingExtractorCount).IsEqual(2);
            AssertThat(story.CurrentIntegrityDrainPerSecond)
                .OverrideFailureMessage("Breaking a machine must lower the future rate.")
                .IsLess(rateBefore);
            AssertThat(story.IsExtractorDestroyed("f01_test_extractor")).IsTrue();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            // The destruction paid its dust award into the shared wallet.
            story.SetDust(walletBefore);
            extractor.Free();
        }
    }

    [TestCase]
    public void TheSecretSubtractsFromTheRateForTheRestOfTheLevelAndNeverPaysIntegrity() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(Par, startingExtractors: 2);
            Pump(story, seconds: 30f);
            float beforeSecret = story.TimelineIntegrityPercent;
            float rateBefore = story.CurrentIntegrityDrainPerSecond;

            AssertThat(story.RegisterSecretFound("f01_secret")).IsTrue();
            AssertThat(story.SecretFoundThisLevel).IsTrue();
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("V7.6 retired the +2/+5 restoration: a secret pays no integrity.")
                .IsEqualApprox(beforeSecret, 0.0001f);
            AssertThat(story.CurrentIntegrityDrainPerSecond)
                .IsEqualApprox(rateBefore * (1.3f / 1.4f), 0.000001f);

            // Once per attempt, and the special/ordinary flag selects nothing.
            AssertThat(story.RegisterSecretFound("f01_secret")).IsFalse();
            AssertThat(story.RegisterSecretFound("f01_secret_b", isSpecialSecret: false)).IsTrue();
            AssertThat(story.CurrentIntegrityDrainPerSecond)
                .OverrideFailureMessage("A second secret must not stack another -0.1.")
                .IsEqualApprox(rateBefore * (1.3f / 1.4f), 0.000001f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void NothingEverAddsIntegrityBackAndARewindDoesNotRefund() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(Par, startingExtractors: 2);
            Pump(story, seconds: 60f);
            float spent = story.TimelineIntegrityPercent;
            AssertThat(spent).IsLess(100f);

            // The death-rewind presentation PAUSES the clock; it never refunds
            // a point. This is the single most important thing not to soften:
            // a refunding rewind turns the timer into an infinite resource.
            story.SetIntegrityClockPause(IntegrityClockPause.DeathRewind, true);
            Pump(story, seconds: 20f);
            story.SetIntegrityClockPause(IntegrityClockPause.DeathRewind, false);
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("A rewind stops the bleeding; it never gives time back.")
                .IsEqualApprox(spent, 0.0001f);

            // And there is deliberately no restoration entry point at all: the
            // V7.3 RestoreTimelineIntegrity sink is gone, not merely unused.
            AssertThat(typeof(StoryManager).GetMethod("RestoreTimelineIntegrity"))
                .OverrideFailureMessage("V7.6 F01 has no restoration path; the old sink must not exist.")
                .IsNull();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
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
            story.ClearLevelAttemptState();
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

        // V7.3 rework: the Dilation Field is a persistent zone dropped at the
        // captured cast position (4 s life), no longer a caster-centred pulse.
        EnemyAbilityData dilation = warden.EliteAbilities[0];
        AssertThat(dilation.Archetype).IsEqual(EnemyAbilityArchetype.PersistentFieldAtTarget);
        AssertThat(dilation.AppliedStatus).IsEqual(StatusType.TimeDilation);
        AssertThat(dilation.StatusDuration > 0f).IsTrue();
        AssertThat(dilation.FieldDurationSeconds).IsEqualApprox(4f, 0.001f);
        AssertThat(dilation.PulseRadius > 0f).IsTrue();

        // Phase Skip stays a Teleport, but V7.3 makes it reactive: excluded
        // from the sequential elite cycle, fired when the target closes in.
        EnemyAbilityData phaseSkip = warden.EliteAbilities[1];
        AssertThat(phaseSkip.Archetype).IsEqual(EnemyAbilityArchetype.Teleport);
    }

    /// <summary>Drives the autoload's own clock tick at 60 Hz, headlessly.</summary>
    internal static void Pump(StoryManager story, float seconds) {
        int frames = Mathf.CeilToInt(seconds * 60f);
        for (int frame = 0; frame < frames; frame++) story.TickIntegrityClock(1.0 / 60.0);
    }
}
