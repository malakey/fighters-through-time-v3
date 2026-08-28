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
    public void TheIntegrityTiersDrainRatesAndRestorationPathsMatchTheDesign() {
        AssertThat(TimelineIntegrityRules.Tier(95f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.Tier(90f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.Tier(75f)).IsEqual(1);
        AssertThat(TimelineIntegrityRules.Tier(69.9f)).IsEqual(2);
        AssertThat(TimelineIntegrityRules.TierKey(92f)).IsEqual("integrity_tier_restored");
        AssertThat(TimelineIntegrityRules.TierKey(72f)).IsEqual("integrity_tier_stabilized");
        AssertThat(TimelineIntegrityRules.TierKey(40f)).IsEqual("integrity_tier_fractured");
        AssertThat(TimelineIntegrityRules.DrainPerSecond(Difficulty.Normal)).IsEqual(0.1f);
        AssertThat(TimelineIntegrityRules.DrainPerSecond(Difficulty.Hard)).IsEqual(0.2f);
        // V7.3 rebalance: the 10% siphon share cap, the 10 s engagement
        // grace, the +3/+2/+5 restoration paths, and the 85% ending
        // threshold (authored now; its consumer is the Level 15 ending work).
        AssertThat(TimelineIntegrityRules.MaxSiphonSharePercent).IsEqual(10f);
        AssertThat(TimelineIntegrityRules.SiphonGraceSeconds).IsEqual(10f);
        AssertThat(TimelineIntegrityRules.ExtractorDestroyRestorePercent).IsEqual(3f);
        AssertThat(TimelineIntegrityRules.GenericSecretRestorePercent).IsEqual(2f);
        AssertThat(TimelineIntegrityRules.SpecialSecretRestorePercent).IsEqual(5f);
        AssertThat(TimelineIntegrityRules.EndingThresholdPercent).IsEqual(85f);
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
    public void TheSiphonIsGracedCappedAtItsShareAndPausedByTheRewindFreeze() {
        // V7.3 rewrite of the unbounded-drain pin: the drain accounting lives
        // on the extractor — 10 s of drain-free grace after engagement, a
        // hard 10-point share cap, and a full stop while the world is frozen
        // for a rewind/scrub.
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var extractor = new ChronalExtractor {
            Name = "SiphonTestExtractor",
            ObjectID = "siphon_test_extractor",
            // Park the discharge cycle out of the way: this case is about the
            // siphon, and discharges would spray VFX into the runner tree.
            SafeWindowSeconds = 100000f
        };
        FTT.Characters.PlayerController player =
            FTT.Characters.CharacterFactory.CreateCharacter("einstein");
        player.GlobalPosition = Vector2.Zero;
        extractor.GlobalPosition = Vector2.Zero;
        tree.Root.AddChild(player);
        tree.Root.AddChild(extractor);
        try {
            story.BeginLevelRun();
            AssertThat(story.TimelineIntegrityPercent).IsEqual(100f);

            // The freeze-group membership the rewind manager sweeps (V7.3).
            AssertThat(extractor.IsInGroup("chronal_extractor")).IsTrue();
            AssertThat(System.Array.IndexOf(
                    ChronalRewindManager.FrozenSimulationGroups, "chronal_extractor") >= 0)
                .OverrideFailureMessage("Extractors must join the rewind freeze sweep.")
                .IsTrue();

            // Engagement (player inside 600 px) starts the 10 s grace: no drain.
            Pump(extractor, seconds: 10f);
            AssertThat(extractor.SiphonEngaged).IsTrue();
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("Peeking into the room must leave no scar during the grace window.")
                .IsEqualApprox(100f, 0.01f);

            // Past the grace, Normal drains 0.1%/s.
            Pump(extractor, seconds: 30f);
            AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(97f, 0.1f);
            AssertThat(extractor.DrainedSharePercent).IsEqualApprox(3f, 0.1f);

            // The rewind freeze pauses the siphon (and would pause the grace).
            extractor.SetStoryRewindFrozen(true);
            Pump(extractor, seconds: 20f);
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("A frozen extractor must not siphon.")
                .IsEqualApprox(97f, 0.1f);
            extractor.SetStoryRewindFrozen(false);

            // Dawdling forever: the share cap bounds the loss at 10 points.
            Pump(extractor, seconds: 120f);
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("No single Extractor may ever cost more than its 10% share.")
                .IsEqualApprox(90f, 0.1f);
            AssertThat(extractor.DrainedSharePercent).IsEqualApprox(10f, 0.01f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            player.Free();
            extractor.Free();
        }
    }

    [TestCase]
    public void RestorationPaysThreePerExtractorTwoPerSecretAndFiveForTheSpecial() {
        StoryManager story = StoryManager.Instance;
        AssertThat(story).IsNotNull();
        var extractor = new ChronalExtractor {
            Name = "RestoreTestExtractor",
            ObjectID = "restore_test_extractor",
            SafeWindowSeconds = 100000f
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(extractor);
        int walletBefore = story.ChronalDustCollected;
        try {
            story.BeginLevelRun();
            story.DrainTimelineIntegrityAmount(20f);
            AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(80f, 0.01f);

            // Destroying an Extractor restores +3% and enters the registry.
            extractor.TakeEnvironmentDamage(extractor.MaxHP);
            AssertThat(extractor.IsDestroyed).IsTrue();
            AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(83f, 0.01f);
            AssertThat(story.IsExtractorDestroyed("restore_test_extractor")).IsTrue();

            // An ordinary secret restores +2%, the special secret +5%.
            AssertThat(story.RegisterSecretFound("ordinary_secret", isSpecialSecret: false)).IsTrue();
            AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(85f, 0.01f);
            AssertThat(story.RegisterSecretFound("special_secret", isSpecialSecret: true)).IsTrue();
            AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(90f, 0.01f);
            AssertThat(story.LevelSecretsFound).IsEqual(2);

            // A re-registration of the same secret is refused (no double pay).
            AssertThat(story.RegisterSecretFound("special_secret")).IsFalse();
            AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(90f, 0.01f);

            // Restoration is capped at 100.
            story.RestoreTimelineIntegrity(50f);
            AssertThat(story.TimelineIntegrityPercent).IsEqual(100f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            // The destruction paid its dust award into the shared wallet.
            story.SetDust(walletBefore);
            extractor.Free();
        }
    }

    private static void Pump(ChronalExtractor extractor, float seconds) {
        int frames = Mathf.CeilToInt(seconds * 60f);
        for (int frame = 0; frame < frames; frame++) extractor._PhysicsProcess(1.0 / 60.0);
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
}
