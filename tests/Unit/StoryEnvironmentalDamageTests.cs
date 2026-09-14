using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.3 Story environmental-damage chokepoint
/// (<c>PlayerController.ApplyEnvironmentalDamage</c>) — the Story mirror of the
/// sim's <c>FighterDamageRules.ApplyUnattributedDamage</c>. Environmental
/// sources (extractor discharges, drown/searchlight/rift ticks, escape
/// catches, stage hazards) route through it instead of raw <c>ApplyDamage</c>,
/// so the victim-side pipeline runs: Rally echo accrual, victim meter on the
/// permanent portion, and Defy History flag consumption (the flag leak that
/// used to poison the NEXT hurtbox hit's accounting). Also pins the
/// <c>StageHazard</c> per-body accumulator's frame-rate independence.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryEnvironmentalDamageTests {

    [TestCase]
    public void EnvironmentalDamageAccruesRallyEchoAndMeter() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            int applied = player.ApplyEnvironmentalDamage(20);
            AssertThat(applied).IsEqual(20);

            float expectedFraction = (BasicComboRules.EchoFractionBase
                    + BasicComboRules.EchoFractionSlope * (20f / player.MaximumHP))
                * StoryDifficultyTuning.GetRallyEchoMultiplier(
                    StoryDifficultyTuning.CurrentStoryDifficulty);
            float expectedEcho = 20f * expectedFraction;
            AssertThat(player.EchoPool)
                .OverrideFailureMessage("An environmental hit must stash its Rally echo like any hit.")
                .IsEqualApprox(expectedEcho, 0.05f);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("The victim meter must earn the permanent (non-echo) portion.")
                .IsEqualApprox((20f - expectedEcho) * UltimateMeter.PointsPerDamageTaken, 0.05f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void ADefiedEnvironmentalHitDoesNotPoisonTheNextHurtboxHit() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            // Full meter, then a lethal environmental hit: Defy History fires.
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);
            player.ApplyEnvironmentalDamage(player.MaximumHP);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A lethal environmental hit against a full meter must not kill.")
                .IsNotEqual(CharacterState.Dead);
            AssertThat(player.CurrentHP).IsEqual(1);
            AssertThat(player.StoryDefyHistoryUsed).IsTrue();
            // The defied hit generates neither echo nor meter — the shattered
            // meter consumed the entire blow.
            AssertThat(player.EchoPool)
                .OverrideFailureMessage("A defied environmental hit must stash no Rally echo.")
                .IsEqual(0f);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("A defied environmental hit must accrue no victim meter.")
                .IsEqual(0f);

            // V7.6 D04 (Package 11 A1b): the survivor is now invulnerable
            // through the presentation and for 60 resumed control ticks, so the
            // follow-up hit has to wait that window out before the ordinary
            // accounting can be observed at all.
            HoldInput(player, GameplayButtons.None,
                StoryDefenseRules.DefyProtectionFrames + BasicComboRules.HitstopFrames(20) + 10);
            AssertThat(player.IsDefyProtected)
                .OverrideFailureMessage("The protected second must have elapsed.")
                .IsFalse();

            // The regression: the Defy flag must have been CONSUMED by the
            // environmental path — the next ordinary hurtbox hit's echo and
            // meter accounting must run normally.
            player.HealStory(60);
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 10f));
            AssertThat(player.EchoPool > 0f)
                .OverrideFailureMessage(
                    "The next hurtbox hit must accrue its Rally echo — the environmental Defy must not poison it.")
                .IsTrue();
            AssertThat(player.CurrentUltimateMeter > 0f)
                .OverrideFailureMessage(
                    "The next hurtbox hit must accrue victim meter — the environmental Defy must not poison it.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void CyclicHazardsAndSpikesRouteThroughTheEnvironmentalChokepoint() {
        // V7.3 follow-up: the last two direct environmental callers
        // (StoryCyclicHazard.ApplyToPlayer and the base level builder's
        // hazard spikes) migrated to the chokepoint — a hit from either must
        // accrue Rally echo and victim meter like every environmental source.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var hazard = new StoryCyclicHazard { Name = "ChokepointCyclicHazard", Damage = 12 };
        tree.Root.AddChild(hazard);
        try {
            hazard.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(player.EchoPool > 0f)
                .OverrideFailureMessage("A cyclic hazard hit must stash its Rally echo (chokepoint path).")
                .IsTrue();
            AssertThat(player.CurrentUltimateMeter > 0f)
                .OverrideFailureMessage("A cyclic hazard hit must accrue victim meter (chokepoint path).")
                .IsTrue();

            float echoAfterHazard = player.EchoPool;
            AssertThat(FTT.Environment.StoryLevelControllerBase.ApplySpikeDamage(player, 15)).IsEqual(15);
            AssertThat(player.EchoPool > echoAfterHazard)
                .OverrideFailureMessage("A spike hit must stash its Rally echo (chokepoint path).")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            hazard.Free();
        }
    }

    [TestCase]
    public void StageHazardDamageIsFrameRateIndependent() {
        // The old (int)(Damage * dt) dealt ZERO at 60 FPS (15/60 truncates)
        // and different totals at different frame rates. The V7.3 per-body
        // accumulator banks fractions: the same window at any tick rate pays
        // the same whole-HP total.
        AssertThat((int)(15f * (1f / 60f)))
            .OverrideFailureMessage("Precondition: the old truncation dealt nothing per 60 FPS frame.")
            .IsEqual(0);

        int TotalOver(float seconds, int steps) {
            float owed = 0f;
            float dt = seconds / steps;
            int total = 0;
            for (int step = 0; step < steps; step++) {
                total += StageHazard.AccumulateDamage(ref owed, 15f, dt);
            }
            return total;
        }

        int at60 = TotalOver(3f, 180);
        int at30 = TotalOver(3f, 90);
        int at144 = TotalOver(3f, 432);
        AssertThat(at60)
            .OverrideFailureMessage("Three active seconds at 15 damage/s must deal 45 HP at 60 FPS.")
            .IsEqual(45);
        AssertThat(at30)
            .OverrideFailureMessage("The same window at 30 FPS must deal the same total.")
            .IsEqual(45);
        AssertThat(at144)
            .OverrideFailureMessage("The same window at 144 FPS must deal the same total.")
            .IsEqual(45);
    }

    // ---- Harness -------------------------------------------------------------

    /// <summary>Advances the controller's own physics loop for N frames.</summary>
    private static void HoldInput(PlayerController player, GameplayButtons buttons, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
    }

    /// <summary>An impulse-free, stun-free test hit so state stays Idle.</summary>
    private static HitPayload Hit(float damage) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };
}
