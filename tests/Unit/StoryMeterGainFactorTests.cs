using System.Collections.Generic;
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
/// Mozart's 50-dust Minor Resonance (<c>mozart_resonance</c>, "The Influence
/// meter builds 10% faster", <c>UltimateBuildRate</c> +0.10) was resolved into
/// <see cref="PlayerController.StoryUltimateBuildRateMultiplier"/> and then read
/// by nothing, so the node did nothing — and every Mozart node above Tier 1
/// sits behind it. It is now read once, as
/// <see cref="PlayerController.StoryMeterGainFactor"/>, by the meter's two gain
/// paths: damage dealt, and damage taken including Rally echo as it drains.
///
/// <para>Flat grants, direct sets and spends never read it, so a scripted grant
/// (the Level 0 Meter &amp; Defy lesson) still lands on exactly the value it
/// asks for. Fighter Mode, the Holodeck and the drills build their heroes with
/// <c>applyStoryProgression: false</c> and never see it; the Level 13 Mirror
/// clone mirrors it on Hard only, as it mirrors every purchased grid perk
/// (F20). The Fighter sim's own gain is pinned bit for bit by
/// <c>InfluenceMeterRulesTests</c> and <c>FighterMeterGainTests</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryMeterGainFactorTests {

    private const string MozartID = "mozart";
    private const float MinorResonanceRate = 1.1f;
    private const string MirrorResourcePath = "res://resources/Bosses/mirror_paradox.tres";
    private const string Level00ScenePath = "res://scenes/campaign/Level_00_Tutorial.tscn";

    [TestCase]
    public void TheBuildRateScalesDamageDealtGainButNeverUltimateOriginDamage() {
        foreach (float rate in new[] { 1f, MinorResonanceRate }) {
            PlayerController hero = SpawnHero(rate);
            try {
                AssertFloat(hero.StoryMeterGainFactor).IsEqualApprox(rate, 0.0001f);
                hero.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
                AssertFloat(hero.CurrentUltimateMeter)
                    .OverrideFailureMessage($"20 HP dealt at a build rate of {rate} earns {20f * rate}.")
                    .IsEqualApprox(20f * rate, 0.0001f);
                AssertFloat(Meter(hero).CurrentValue).IsEqualApprox(20f * rate, 0.0001f);

                // D03h: Ultimate-origin damage still earns the caster nothing.
                hero.AddInfluenceFromDamageDealt(30f, collectsEcho: false, ultimateOrigin: true);
                AssertFloat(hero.CurrentUltimateMeter).IsEqualApprox(20f * rate, 0.0001f);
            } finally {
                Free(hero);
            }
        }
    }

    [TestCase]
    public void TheBuildRateScalesDamageTakenAndTheRallyEchoAsItDrains() {
        (float atHit, float echo, float uncredited, float drained) baseline = TakeAndDrain(1f);
        (float atHit, float echo, float uncredited, float drained) mozart = TakeAndDrain(MinorResonanceRate);

        // The hit's permanent share earns at the build rate; the echo itself is
        // HP, untouched by a meter lane.
        AssertThat(baseline.atHit > 0f).IsTrue();
        AssertFloat(mozart.atHit).IsEqualApprox(baseline.atHit * MinorResonanceRate, 0.001f);
        AssertFloat(mozart.echo).IsEqualApprox(baseline.echo, 0.0001f);
        // The outstanding echo is recorded at the rate the drain would credit,
        // so the load-time settlement adds it back without a second scaling.
        AssertFloat(baseline.uncredited)
            .IsEqualApprox(baseline.echo * InfluenceMeterRules.PointsPerDamageTaken, 0.0001f);
        AssertFloat(mozart.uncredited).IsEqualApprox(baseline.uncredited * MinorResonanceRate, 0.001f);

        // Drained to empty: exactly damage x 0.25 x rate in total.
        AssertFloat(baseline.drained).IsEqualApprox(20f * InfluenceMeterRules.PointsPerDamageTaken, 0.01f);
        AssertFloat(mozart.drained)
            .OverrideFailureMessage("A Minor Resonance hero earns 10% more from every hit taken, echo included.")
            .IsEqualApprox(20f * InfluenceMeterRules.PointsPerDamageTaken * MinorResonanceRate, 0.01f);
    }

    [TestCase]
    public void FlatGrantsDirectSetsAndSpendsIgnoreTheBuildRate() {
        PlayerController hero = SpawnHero(MinorResonanceRate);
        try {
            UltimateMeter meter = Meter(hero);
            meter.AddFlat(15f);
            AssertThat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("A flat grant (an orb, Shield of Orléans) is not a build-rate gain.")
                .IsEqual(15f);
            hero.SetUltimateMeter(40f);
            AssertThat(hero.CurrentUltimateMeter).IsEqual(40f);
            hero.DrainUltimateMeter(10f);
            AssertThat(hero.CurrentUltimateMeter).IsEqual(30f);
            hero.ApplyStockLossMeterRetention();
            AssertThat(hero.CurrentUltimateMeter).IsEqual(30f * InfluenceMeterRules.StockLossRetention);
            hero.SetUltimateMeter(InfluenceMeterRules.MaxValue + 50f);
            AssertThat(hero.CurrentUltimateMeter).IsEqual(InfluenceMeterRules.MaxValue);
            meter.Consume();
            AssertThat(hero.CurrentUltimateMeter).IsEqual(0f);
        } finally {
            Free(hero);
        }
    }

    [TestCase]
    public void TheCampaignHeroPicksUpTheBoughtNodeAndANonCampaignBuildNeverDoes() {
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[0];
        GameManager.Instance.CurrentSession.ActiveSaveSlot = 0;
        GameManager.Instance.CurrentSession.SelectedCharacterID = MozartID;
        SaveManager.Instance.SaveSlots[0] = MozartSaveWithMinorResonance();

        PlayerController campaign = null;
        PlayerController normalized = null;
        try {
            campaign = AddToTree(CharacterFactory.CreateCharacter(MozartID, 0, applyStoryProgression: true));
            AssertFloat(campaign.StoryUltimateBuildRateMultiplier).IsEqualApprox(MinorResonanceRate, 0.0001f);
            // D5 (USER-STORY-METER-SCALE): the campaign hero's gains also carry the
            // Story scale, which the lane multiplies.
            AssertThat(campaign.AppliesStoryMeterGainScale).IsTrue();
            campaign.SetUltimateMeter(0f);
            campaign.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
            AssertFloat(campaign.CurrentUltimateMeter)
                .OverrideFailureMessage("The Minor Resonance makes the Story meter build 10% faster.")
                .IsEqualApprox(20f * StoryCombatRules.MeterGainScale * MinorResonanceRate, 0.0001f);
            Free(campaign);
            campaign = null;

            // Fighter Mode, the Holodeck and the drills: the same save, no lane.
            normalized = AddToTree(CharacterFactory.CreateCharacter(MozartID, 0, applyStoryProgression: false));
            AssertThat(normalized.StoryMeterGainFactor).IsEqual(1f);
            normalized.SetUltimateMeter(0f);
            normalized.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
            AssertThat(normalized.CurrentUltimateMeter).IsEqual(20f);
        } finally {
            Free(campaign);
            Free(normalized);
            SaveManager.Instance.SaveSlots[0] = originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
        }
    }

    [TestCase]
    public void AHardMirrorOfAMozartBuildMirrorsTheBuildRateAndANormalOneDoesNot() {
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[0];
        GameManager.Instance.CurrentSession.ActiveSaveSlot = 0;
        GameManager.Instance.CurrentSession.SelectedCharacterID = MozartID;
        SaveManager.Instance.SaveSlots[0] = MozartSaveWithMinorResonance();

        MirrorParadoxController hard = null;
        MirrorParadoxController normal = null;
        try {
            // F20: Hard mirrors every purchased grid perk, so the clone's meter
            // builds at the player's rate too.
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Hard;
            hard = CreateMirror();
            AssertThat(hard.MirrorsPurchasedPerks).IsTrue();
            AssertFloat(hard.Clone.StoryMeterGainFactor).IsEqualApprox(MinorResonanceRate, 0.0001f);
            hard.Clone.SetUltimateMeter(0f);
            hard.Clone.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
            AssertFloat(hard.Clone.CurrentUltimateMeter).IsEqualApprox(22f, 0.0001f);

            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;
            normal = CreateMirror();
            AssertThat(normal.MirrorsPurchasedPerks).IsFalse();
            AssertThat(normal.Clone.StoryMeterGainFactor).IsEqual(1f);
            normal.Clone.SetUltimateMeter(0f);
            normal.Clone.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
            AssertThat(normal.Clone.CurrentUltimateMeter).IsEqual(20f);
        } finally {
            Free(hard);
            Free(normal);
            SaveManager.Instance.SaveSlots[0] = originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
        }
    }

    [TestCase]
    public void TheLevel0DefyLessonFillsTheBarToExactlyOneHundredWhateverTheGainFactor() {
        // The lesson's grant used to be a damage-dealt gain of 100, which a
        // gain factor below 1 would have short-filled (the bar unlit, the
        // scripted lethal hit then killing the hero instead of being defied).
        // It is a direct set now. 0.5 stands in for any sub-unity Story scale.
        foreach (float rate in new[] { 0.5f, MinorResonanceRate }) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            bool originalPaused = tree.Paused;
            int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            Level00Controller level = null;
            try {
                level = ResourceLoader.Load<PackedScene>(Level00ScenePath).Instantiate<Level00Controller>();
                tree.Root.AddChild(level);
                var player = level.GetNode<PlayerController>("Player");
                player.StoryUltimateBuildRateMultiplier = rate;
                player.SetUltimateMeter(0f);
                // CharacterFactory seeds a spent Defy when StoryManager holds a live
                // attempt that spent it, and an earlier suite can leave one behind;
                // the lesson then (correctly) skips its grant. This case is about the
                // grant, so it starts from the fresh attempt the lesson is written for.
                // The spent-Defy skip is pinned in TutorialCalibrationTests.
                bool seededSpent = player.StoryDefyHistoryUsed;
                player.SetStoryDefyHistoryUsed(false);

                EventBus.Instance.RaiseDialogueComplete("level_00.ultimate_intro");

                AssertThat(player.CurrentUltimateMeter)
                    .OverrideFailureMessage(
                        $"At a gain factor of {rate} the lesson must still light a full bar (Defy seeded spent: {seededSpent}).")
                    .IsEqual(InfluenceMeterRules.MaxValue);
                AssertThat(Meter(player).CurrentValue).IsEqual(InfluenceMeterRules.MaxValue);
                AssertThat(Meter(player).IsFull).IsTrue();
                AssertThat(player.StoryDefyHistoryUsed).IsFalse();
            } finally {
                Free(level);
                GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
                tree.Paused = originalPaused;
            }
        }
    }

    // === Helpers ========================================================

    private static (float atHit, float echo, float uncredited, float drained) TakeAndDrain(float rate) {
        PlayerController hero = SpawnHero(rate);
        try {
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 20f));
            float atHit = hero.CurrentUltimateMeter;
            float echo = hero.EchoPool;
            float uncredited = hero.RallyUncreditedMeter;
            AssertThat(echo > 0f).OverrideFailureMessage("The hit must stash a Rally echo.").IsTrue();

            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            int frames = BasicComboRules.HitstopFrames(20) + BasicComboRules.EchoDrainFrames + 10;
            for (int frame = 0; frame < frames; frame++) hero._PhysicsProcess(1.0 / 60.0);
            AssertThat(hero.EchoPool).IsEqual(0f);
            return (atHit, echo, uncredited, hero.CurrentUltimateMeter);
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            Free(hero);
        }
    }

    /// <summary>
    /// A hero built off the campaign path (no save dependence), with the lane
    /// written as <c>CharacterFactory</c> writes it for a bought node.
    /// </summary>
    private static PlayerController SpawnHero(float buildRate) {
        PlayerController hero = AddToTree(CharacterFactory.CreateCharacter(MozartID, 0, applyStoryProgression: false));
        hero.StoryUltimateBuildRateMultiplier = buildRate;
        hero.SetUltimateMeter(0f);
        return hero;
    }

    private static PlayerController AddToTree(PlayerController hero) {
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hero);
        return hero;
    }

    private static UltimateMeter Meter(PlayerController hero) => hero.GetNode<UltimateMeter>("UltimateMeter");

    private static StorySaveData MozartSaveWithMinorResonance() => new() {
        SelectedCharacterID = MozartID,
        DepositedChronalDust = new Dictionary<string, int>(),
        GridProgress = new Dictionary<string, List<string>> {
            [MozartID] = new() { "mozart_tempo", "mozart_baton", "mozart_resonance" }
        }
    };

    private static MirrorParadoxController CreateMirror() {
        var mirror = new MirrorParadoxController {
            Name = "MozartMirrorUnderTest",
            Data = AuthoredResources.Load<BossData>(MirrorResourcePath),
            CharacterIDOverride = MozartID,
            DecisionSeed = 4242,
            // Construction and the meter only; an active clone would fight on
            // every frame the runner yields between tests.
            ActivateOnSpawn = false
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(mirror);
        return mirror;
    }

    /// <summary>An impulse-free, stun-free test hit so the hero stays Idle.</summary>
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

    private static void Free(Node node) {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        node.GetParent()?.RemoveChild(node);
        node.Free();
    }
}
