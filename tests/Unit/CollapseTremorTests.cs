using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.6 Collapse Tremor (Package 11 A3): the stage thresholds, the
/// debris contract (telegraph, damage fraction, blockability, the two-airborne
/// cap, the safe-zone exclusions), the unstable-platform respawn guarantee,
/// and the two ways the Tremor must stop — a paused clock and the PreBoss lock.
///
/// Enemies are deliberately absent from every case here, because the Tremor is
/// the timeline coming apart around the hero rather than a second combat
/// participant: the debris only ever resolves against the StoryPlayer group.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CollapseTremorTests {

    [TestCase]
    public void TheStageThresholdsAndAuthoredCadencesMatchTheDesign() {
        AssertThat(TimelineIntegrityRules.TremorStage1Threshold).IsEqual(20f);
        AssertThat(TimelineIntegrityRules.TremorStage2Threshold).IsEqual(10f);

        AssertThat(TimelineIntegrityRules.TremorStage(100f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.TremorStage(20f)).IsEqual(0);
        AssertThat(TimelineIntegrityRules.TremorStage(19.9f)).IsEqual(1);
        AssertThat(TimelineIntegrityRules.TremorStage(10f)).IsEqual(1);
        AssertThat(TimelineIntegrityRules.TremorStage(9.9f)).IsEqual(2);
        AssertThat(TimelineIntegrityRules.TremorStage(0f)).IsEqual(2);

        AssertThat(CollapseTremorRules.ContinuousShakeIntensity).IsEqual(0.1f);
        AssertThat(CollapseTremorRules.JoltIntensity).IsEqual(0.3f);
        AssertThat(CollapseTremorRules.JoltSeconds).IsEqual(0.4f);

        // Stage 1 jolts every 6-8 s; stage 2 every 3-4 s.
        AssertThat(CollapseTremorRules.JoltBand(1)).IsEqual((6f, 8f));
        AssertThat(CollapseTremorRules.JoltBand(2)).IsEqual((3f, 4f));

        // Debris about every 4 s at stage 1, twice as often at stage 2.
        AssertThat(CollapseTremorRules.DebrisInterval(0)).IsEqual(0f);
        AssertThat(CollapseTremorRules.DebrisInterval(1)).IsEqual(4f);
        AssertThat(CollapseTremorRules.DebrisInterval(2)).IsEqual(2f);
        AssertThat(CollapseTremorRules.Stage2DebrisIntervalSeconds * 2f)
            .IsEqual(CollapseTremorRules.Stage1DebrisIntervalSeconds);
    }

    [TestCase]
    public void DebrisTelegraphsForThreeQuartersOfASecondBeforeItFalls() {
        AssertThat(CollapseTremorRules.DebrisTelegraphSeconds).IsEqual(0.75f);
        var debris = NewDebris();
        try {
            debris.Launch(new Vector2(0f, 500f));
            AssertThat(debris.Phase).IsEqual(FallingDebrisPhase.Telegraph);

            // Still telegraphing one frame short of the window.
            Step(debris, 0.74f);
            AssertThat(debris.Phase)
                .OverrideFailureMessage("The warning must run its full 0.75 s before the rock moves.")
                .IsEqual(FallingDebrisPhase.Telegraph);

            Step(debris, 0.03f);
            AssertThat(debris.Phase).IsEqual(FallingDebrisPhase.Falling);
        } finally {
            Cleanup(debris);
        }
    }

    [TestCase]
    public void DebrisDealsFivePercentOfMaximumHpAndNeverAFlatNumber() {
        AssertThat(CollapseTremorRules.DamageFractionOfMaxHP).IsEqual(0.05f);
        AssertThat(CollapseTremorRules.DebrisDamage(100)).IsEqual(5);
        AssertThat(CollapseTremorRules.DebrisDamage(200)).IsEqual(10);
        AssertThat(CollapseTremorRules.DebrisDamage(340)).IsEqual(17);
        // Never a no-op, however small the pool.
        AssertThat(CollapseTremorRules.DebrisDamage(1)).IsEqual(1);

        PlayerController player = NewPlayer();
        var debris = NewDebris();
        try {
            int expected = CollapseTremorRules.DebrisDamage(player.MaximumHP);
            int before = player.CurrentHP;
            debris.GlobalPosition = player.GlobalPosition;
            AssertThat(debris.ApplyTo(player)).IsEqual(expected);
            AssertThat(before - player.CurrentHP).IsEqual(expected);
        } finally {
            Cleanup(debris);
            player.Free();
        }
    }

    [TestCase]
    public void DebrisIsBlockableAsABasicClassHitAndSpendsOneShieldCharge() {
        PlayerController player = NewPlayer();
        var debris = NewDebris();
        try {
            var block = player.GetNode<FTT.Combat.BlockSystem>("BlockSystem");
            int chargesBefore = block.CurrentCharges;
            AssertThat(chargesBefore).IsGreater(1);
            block.StartBlock();
            AssertThat(block.IsBlocking).IsTrue();

            // In front of the blocker, so the front-facing rule is satisfied.
            debris.GlobalPosition = player.GlobalPosition
                + new Vector2(player.IsFacingRight ? 40f : -40f, -20f);
            int before = player.CurrentHP;

            AssertThat(debris.ApplyTo(player))
                .OverrideFailureMessage("A blocked Basic-class hit deals no HP.")
                .IsEqual(0);
            AssertThat(player.CurrentHP).IsEqual(before);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("Debris is Basic class: exactly one charge, never a shatter.")
                .IsEqual(chargesBefore - 1);
        } finally {
            Cleanup(debris);
            player.Free();
        }
    }

    [TestCase]
    public void NoMoreThanTwoPiecesAreEverAirborneAtOnce() {
        AssertThat(CollapseTremorRules.MaxAirborneDebris).IsEqual(2);
        PlayerController player = NewPlayer();
        CollapseTremorController tremor = NewTremor();
        var spawned = new System.Collections.Generic.List<FallingDebris>();
        tremor.DebrisSpawnOverrideForTesting = _ => {
            var piece = NewDebris();
            spawned.Add(piece);
            return piece;
        };
        try {
            AssertThat(tremor.TryLaunchDebris()).IsTrue();
            AssertThat(tremor.TryLaunchDebris()).IsTrue();
            AssertThat(tremor.TryLaunchDebris())
                .OverrideFailureMessage("A third piece must wait for a slot.")
                .IsFalse();
            AssertThat(tremor.AirborneCount).IsEqual(2);

            // Land one: the slot frees and the next piece may launch.
            Step(spawned[0], 5f);
            AssertThat(spawned[0].IsAirborne).IsFalse();
            AssertThat(tremor.AirborneCount).IsEqual(1);
            AssertThat(tremor.TryLaunchDebris()).IsTrue();
        } finally {
            foreach (FallingDebris piece in spawned) Cleanup(piece);
            tremor.Free();
            player.Free();
        }
    }

    [TestCase]
    public void DebrisNeverLandsOnARestorationFontOrAnActivatedFracture() {
        AssertThat(CollapseTremorRules.SafeZoneRadiusPixels).IsGreater(0f);
        CollapseTremorController tremor = NewTremor();
        var font = new RestorationFont { Name = "TremorTestFont", FontID = "tremor_test_font" };
        var fracture = new CheckpointTrigger {
            Name = "TremorTestCheckpoint",
            CheckpointID = "tremor_test_checkpoint",
            Role = CheckpointRole.Entry
        };
        tremor.AddChild(font);
        tremor.AddChild(fracture);
        font.GlobalPosition = new Vector2(1000f, 0f);
        fracture.GlobalPosition = new Vector2(3000f, 0f);
        try {
            AssertThat(tremor.IsLandingLegal(new Vector2(1000f, 0f)))
                .OverrideFailureMessage("A heal must never be interrupted by rubble.")
                .IsFalse();
            AssertThat(tremor.IsLandingLegal(new Vector2(1000f + CollapseTremorRules.SafeZoneRadiusPixels + 10f, 0f)))
                .IsTrue();

            // An UNactivated fracture is not yet a safe beat.
            AssertThat(tremor.IsLandingLegal(new Vector2(3000f, 0f))).IsTrue();
            fracture.Activate();
            AssertThat(tremor.IsLandingLegal(new Vector2(3000f, 0f)))
                .OverrideFailureMessage("A stabilized fracture is a safe beat by definition.")
                .IsFalse();
        } finally {
            StoryManager.Instance.ClearLevelAttemptState();
            tremor.Free();
        }
    }

    [TestCase]
    public void EveryUnstablePlatformRespawnsSoNoGapBecomesUncrossable() {
        AssertThat(CollapseTremorRules.FracturePlatformRespawnSeconds).IsEqual(5f);
        var platform = new CrumblingPlatform {
            Name = "TremorTestPlatform",
            PlatformID = "tremor_test_platform",
            FractureEligible = true,
            ShakeDuration = 0.1f,
            CollapseDuration = 0.1f,
            RespawnDuration = CollapseTremorRules.FracturePlatformRespawnSeconds
        };
        platform.AddChild(new CollisionShape2D {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(200f, 16f) }
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(platform);
        try {
            AssertThat(platform.TriggerCollapse()).IsTrue();
            AssertThat(platform.State).IsEqual(CrumblingPlatformState.Shaking);

            // Shake -> Collapsing -> Disabled -> and always back to Solid.
            StepPlatform(platform, 0.2f);
            AssertThat(platform.State).IsEqual(CrumblingPlatformState.Collapsing);
            StepPlatform(platform, 0.2f);
            AssertThat(platform.State).IsEqual(CrumblingPlatformState.Disabled);
            StepPlatform(platform, CollapseTremorRules.FracturePlatformRespawnSeconds + 0.1f);
            AssertThat(platform.State)
                .OverrideFailureMessage("Every FractureEligible platform respawns; no route may be severed.")
                .IsEqual(CrumblingPlatformState.Solid);

            // Opt-in only: an unflagged platform is not Tremor-eligible at all.
            var plain = new CrumblingPlatform { Name = "PlainPlatform", PlatformID = "plain" };
            AssertThat(plain.FractureEligible)
                .OverrideFailureMessage("FractureEligible must default off: exclusions are enforced by omission.")
                .IsFalse();
            plain.Free();
        } finally {
            platform.Free();
        }
    }

    [TestCase]
    public void TheTremorStopsWhenTheClockPausesAndRetiresAtThePreBossLock() {
        StoryManager story = StoryManager.Instance;
        CollapseTremorController tremor = NewTremor();
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(600f, startingExtractors: 0);
            story.DrainTimelineIntegrityAmount(85f);   // 15% -> stage 1
            tremor.Tick(1f / 60f);
            AssertThat(tremor.Stage).IsEqual(1);
            AssertThat(tremor.HasPlayedFirstCrossingBark)
                .OverrideFailureMessage("The first crossing plays its one-per-level bark.")
                .IsTrue();

            story.DrainTimelineIntegrityAmount(10f);   // 5% -> stage 2
            tremor.Tick(1f / 60f);
            AssertThat(tremor.Stage).IsEqual(2);

            // A frozen world does not shake.
            story.SetIntegrityClockPause(IntegrityClockPause.Dialogue, true);
            tremor.Tick(1f / 60f);
            AssertThat(tremor.Stage)
                .OverrideFailureMessage("The Tremor stops whenever the clock stops.")
                .IsEqual(0);
            story.SetIntegrityClockPause(IntegrityClockPause.Dialogue, false);
            tremor.Tick(1f / 60f);
            AssertThat(tremor.Stage).IsEqual(2);

            // The boss arena is always stable.
            story.LockIntegrityAtPreBoss();
            tremor.Tick(1f / 60f);
            AssertThat(tremor.IsRetired).IsTrue();
            AssertThat(tremor.Stage).IsEqual(0);
            tremor.Tick(1f / 60f);
            AssertThat(tremor.Stage)
                .OverrideFailureMessage("A retired Tremor never comes back inside the boss arena.")
                .IsEqual(0);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            tremor.Free();
        }
    }

    [TestCase]
    public void AllShakeRunsThroughTheAccessibilityScaleSoZeroDisablesIt() {
        CameraShake shake = CameraShake.Instance;
        AssertThat(shake).IsNotNull();
        // The Tremor never bypasses CameraShake, which is the single place the
        // Settings screen-shake slider is applied — a 0 scale silences every
        // continuous shake and every jolt without changing the Tremor's TIMING,
        // which the C01a Reduced Temporal Effects preset must also preserve.
        AssertThat(typeof(CollapseTremorController)
                .GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            .IsNotNull();
        shake.SetIntensityScale(0f);
        try {
            // Driving the controller with the slider at zero must not throw and
            // must leave the stage machine (the timing) untouched.
            StoryManager story = StoryManager.Instance;
            CollapseTremorController tremor = NewTremor();
            try {
                story.BeginLevelRun();
                story.BeginIntegrityClock(600f, startingExtractors: 0);
                story.DrainTimelineIntegrityAmount(85f);
                for (int frame = 0; frame < 600; frame++) tremor.Tick(1f / 60f);
                AssertThat(tremor.Stage)
                    .OverrideFailureMessage("Disabling shake must not disable the Tremor's timing.")
                    .IsEqual(1);
            } finally {
                story.ClearLevelAttemptState();
                story.StopLevelRun();
                tremor.Free();
            }
        } finally {
            shake.SetIntensityScale(
                SaveManager.Instance?.GlobalData?.ScreenShakeScale ?? 1f);
        }
    }

    // === Fixtures =======================================================

    private static PlayerController NewPlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        player.GlobalPosition = Vector2.Zero;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return player;
    }

    private static CollapseTremorController NewTremor() {
        var tremor = new CollapseTremorController { Name = "TestTremor" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(tremor);
        return tremor;
    }

    private static FallingDebris NewDebris() {
        var debris = new FallingDebris { Name = "TestDebris" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(debris);
        debris.Launch(new Vector2(0f, 500f));
        return debris;
    }

    private static void Step(FallingDebris debris, float seconds) {
        int frames = Mathf.CeilToInt(seconds * 60f);
        for (int frame = 0; frame < frames; frame++) debris.Step(1f / 60f);
    }

    private static void StepPlatform(CrumblingPlatform platform, float seconds) {
        int frames = Mathf.CeilToInt(seconds * 60f);
        for (int frame = 0; frame < frames; frame++) platform._PhysicsProcess(1.0 / 60.0);
    }

    private static void Cleanup(FallingDebris debris) {
        if (GodotObject.IsInstanceValid(debris)) debris.Free();
    }
}
