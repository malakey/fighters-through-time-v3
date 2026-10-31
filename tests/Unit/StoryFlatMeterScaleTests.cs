using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Story flat Influence-meter effects are halved on the campaign hero (meter and Defy update
/// 2026-10-31, plan 2.8, ledger <c>USER-STORY-FLAT-EFFECTS-SCALED</c>): the Extractor discharge,
/// the searchlight drain and the Siphon Snare scale by <see cref="StoryCombatRules.FlatEffectScale"/>
/// at their call sites through <c>PlayerController.ScaleStoryFlatMeter</c>; the Mirror clone and every
/// non-campaign build stay unscaled, Mozart's build-rate lane never reaches them, and a spend
/// (<c>DrainUltimateMeter</c> itself) is untouched.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryFlatMeterScaleTests {
    private const float Step = 1f / 60f;
    private const string EraserID = "unbound_eraser";

    [TestCase]
    public void TheHelperHalvesAFlatAmountOnTheCampaignHeroOnlyAndNeverReadsMozartsLane() {
        PlayerController campaign = CharacterFactory.CreateCharacter("einstein");
        PlayerController clone = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
        try {
            AssertThat(campaign.AppliesStoryMeterGainScale).IsTrue();
            AssertFloat(campaign.ScaleStoryFlatMeter(20f)).IsEqualApprox(10f, 0.0001f);
            AssertFloat(clone.ScaleStoryFlatMeter(20f))
                .OverrideFailureMessage("The Mirror clone and non-campaign builds stay unscaled.")
                .IsEqualApprox(20f, 0.0001f);

            campaign.StoryUltimateBuildRateMultiplier = 1.1f;
            AssertFloat(campaign.ScaleStoryFlatMeter(20f))
                .OverrideFailureMessage("O27: Mozart's build-rate lane never reaches a flat effect.")
                .IsEqualApprox(10f, 0.0001f);
            AssertFloat(StoryCombatRules.FlatEffectScale).IsEqualApprox(StoryCombatRules.MeterGainScale, 0.0001f);

            // A spend is not a flat effect: draining 30 takes 30 on the scaled hero.
            campaign.CurrentUltimateMeter = 100f;
            campaign.DrainUltimateMeter(30f);
            AssertFloat(campaign.CurrentUltimateMeter).IsEqualApprox(70f, 0.0001f);
        } finally {
            campaign.Free();
            clone.Free();
        }
    }

    [TestCase]
    public void TheExtractorDischargeDrainsTenOnTheCampaignHeroAndTwentyOnAnUnscaledBuild() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/templates/ChronalExtractorTemplate.tscn");
        var extractor = scene.Instantiate<ChronalExtractor>();
        tree.Root.AddChild(extractor);
        PlayerController campaign = CharacterFactory.CreateCharacter("einstein");
        PlayerController plain = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
        tree.Root.AddChild(campaign);
        tree.Root.AddChild(plain);
        try {
            AssertFloat(extractor.UltimateDrain).IsEqualApprox(20f, 0.001f);
            // Isolate the drain from the damage-taken meter the discharge's own HP hit earns.
            extractor.HazardDamage = 0;
            campaign.RestoreStoryCheckpoint(Vector2.Zero, campaign.MaximumHP, 50f);
            plain.RestoreStoryCheckpoint(Vector2.Zero, plain.MaximumHP, 50f);
            extractor.ApplyDischargeToPlayer(campaign);
            extractor.ApplyDischargeToPlayer(plain);
            AssertFloat(campaign.CurrentUltimateMeter).IsEqualApprox(40f, 0.01f);
            AssertFloat(plain.CurrentUltimateMeter).IsEqualApprox(30f, 0.01f);
        } finally {
            campaign.Free();
            plain.Free();
            extractor.Free();
        }
    }

    [TestCase]
    public void ASearchlightBeamDrainsTwoAndAHalfPerSecondOnTheCampaignHeroAndFiveOnAnUnscaledBuild() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var beam = new SearchlightZone {
            SearchlightID = "test.flat.searchlight",
            Mode = SearchlightMode.UltimateDrain,
            UltimateDrainPerSecond = 5f
        };
        tree.Root.AddChild(beam);
        PlayerController campaign = CharacterFactory.CreateCharacter("einstein");
        PlayerController plain = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
        tree.Root.AddChild(campaign);
        tree.Root.AddChild(plain);
        try {
            campaign.RestoreStoryCheckpoint(Vector2.Zero, campaign.MaximumHP, 50f);
            plain.RestoreStoryCheckpoint(Vector2.Zero, plain.MaximumHP, 50f);
            beam.AddPlayer(campaign);
            beam.AddPlayer(plain);
            beam.TickExposure(2f);
            AssertFloat(campaign.CurrentUltimateMeter).IsEqualApprox(45f, 0.001f);
            AssertFloat(plain.CurrentUltimateMeter).IsEqualApprox(40f, 0.001f);
        } finally {
            campaign.Free();
            plain.Free();
            beam.Free();
        }
    }

    [TestCase]
    public void TheSiphonSnareTakesFivePerSecondCappedAtFifteenFromTheCampaignHero() {
        (float oneSecond, float drained, float meter) = RunSnare(storyScaled: true, ticksAfterTheFirstSecond: 600);
        AssertFloat(oneSecond).OverrideFailureMessage("One live second takes 5 points, not 10.").IsEqualApprox(95f, 0.01f);
        AssertFloat(drained).OverrideFailureMessage("The cap halves with the rate.").IsEqualApprox(15f, 0.01f);
        AssertFloat(meter).IsEqualApprox(85f, 0.01f);

        (float plainOneSecond, float plainDrained, float plainMeter) = RunSnare(storyScaled: false, ticksAfterTheFirstSecond: 600);
        AssertFloat(plainOneSecond).IsEqualApprox(90f, 0.01f);
        AssertFloat(plainDrained).IsEqualApprox(30f, 0.01f);
        AssertFloat(plainMeter).IsEqualApprox(70f, 0.01f);
    }

    /// <summary>Casts the snare at a full-meter hero and returns the meter after 1 s, the drained total and the final meter.</summary>
    private static (float oneSecond, float drained, float meter) RunSnare(bool storyScaled, int ticksAfterTheFirstSecond) {
        using var world = new SnareWorld(storyScaled);
        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Attached);
        SiphonTetherChannel channel = world.Channel;
        for (int frame = 0; frame < 60; frame++) channel.TickChannel(Step);
        float oneSecond = world.Player.CurrentUltimateMeter;
        for (int frame = 0; frame < ticksAfterTheFirstSecond && channel.IsLive; frame++) channel.TickChannel(Step);
        return (oneSecond, channel.DrainedTotal, world.Player.CurrentUltimateMeter);
    }

    private sealed class SnareWorld : IDisposable {
        public readonly PlayerController Player;
        public readonly EnemyController Eraser;
        public readonly EnemyAbilityData Ability;

        public SiphonTetherChannel Channel => Eraser.ActiveSiphonTether;

        public SnareWorld(bool storyScaled) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            Player = storyScaled
                ? CharacterFactory.CreateCharacter("einstein")
                : CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            tree.Root.AddChild(Player);
            Player.GlobalPosition = new Vector2(1000f, 500f);
            Player.CurrentUltimateMeter = 100f;
            Player.GetNodeOrNull<UltimateMeter>("UltimateMeter")?.SetValue(100f);

            EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{EraserID}.tres");
            var data = (EnemyData)canonical.Duplicate();
            PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
            Eraser = scene.Instantiate<EnemyController>();
            Eraser.Data = data;
            tree.Root.AddChild(Eraser);
            Eraser.OnSpawn();
            Eraser.GlobalPosition = Player.GlobalPosition + new Vector2(90f, 0f);
            Ability = data.EliteAbilities[1];
        }

        public SiphonAttachResult Cast() {
            Eraser.BeginAttack(Ability);
            for (int frame = 0; frame < Ability.TelegraphFrames; frame++) Eraser.TickAbilityExecutor(Step);
            return Eraser.LastSiphonResult;
        }

        public void Dispose() {
            Eraser.ActiveSiphonTether?.Release();
            if (GodotObject.IsInstanceValid(Eraser)) Eraser.Free();
            if (GodotObject.IsInstanceValid(Player)) Player.Free();
        }
    }
}
