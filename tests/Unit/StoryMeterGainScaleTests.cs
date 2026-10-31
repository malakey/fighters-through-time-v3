using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D5 (user direction 2026-10-11, ledger <c>USER-STORY-METER-SCALE</c>): "The
/// Ultimate is harder to get: Story meter gain ×0.35" (×0.5 since the 2026-10-31 meter and Defy update, ledger
/// <c>USER-STORY-METER-SCALE-HALF</c>). The campaign hero — any
/// build on <c>CharacterFactory</c>'s <c>applyStoryProgression &amp;&amp;
/// applyLegacyUnlockLocks</c> branch — earns every meter GAIN (damage dealt,
/// damage taken, drained Rally echo) at
/// <see cref="StoryCombatRules.MeterGainScale"/>, times Mozart's build-rate
/// lane, through <see cref="PlayerController.StoryMeterGainFactor"/>. Flat
/// grants, direct sets and spends are not scaled. Every other build (the Level
/// 13 Mirror clone, the drills, the Holodeck, Fighter) earns its lane alone.
///
/// <para>Every case that expects the bare scale writes the lane to 1.0 after
/// construction, so a save another suite left in the session cannot leak a
/// lane in. The expectations multiply by <see cref="StoryCombatRules.MeterGainScale"/>
/// literally, so they fail on the pre-D5 build (1.0 per HP dealt).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryMeterGainScaleTests {

    private const float MinorResonanceRate = 1.1f;
    private const string Level00ScenePath = "res://scenes/campaign/Level_00_Tutorial.tscn";

    [TestCase]
    public void TheStoryMeterGainFactorIsTheScaleOnTheCampaignHeroTimesItsLane() {
        AssertThat(StoryCombatRules.MeterGainScale).IsEqual(0.5f);
        AssertFloat(StoryCombatRules.MeterGainFactor(true, 1f)).IsEqualApprox(0.5f, 0.00001f);
        AssertFloat(StoryCombatRules.MeterGainFactor(true, MinorResonanceRate)).IsEqualApprox(0.55f, 0.00001f);
        AssertThat(StoryCombatRules.MeterGainFactor(true, -1f)).IsEqual(0f);
        AssertFloat(StoryCombatRules.MeterGainFactor(false, MinorResonanceRate)).IsEqualApprox(MinorResonanceRate, 0.00001f);
        AssertThat(StoryCombatRules.MeterGainFactor(false, 1f)).IsEqual(1f);
        AssertThat(StoryCombatRules.MeterGainFactor(false, -1f)).IsEqual(0f);
    }

    [TestCase]
    public void TheCampaignHeroEarnsDamageDealtMeterAtTheStoryScale() {
        PlayerController hero = SpawnCampaignHero();
        try {
            AssertThat(hero.AppliesStoryMeterGainScale)
                .OverrideFailureMessage("CharacterFactory's campaign-hero branch must set the D5 flag.")
                .IsTrue();
            hero.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
            float expected = 20f * StoryCombatRules.MeterGainScale;
            AssertFloat(Meter(hero).CurrentValue)
                .OverrideFailureMessage("20 HP dealt by the campaign hero earns 20 x 0.5 = 10 meter.")
                .IsEqualApprox(expected, 0.001f);
            AssertFloat(hero.CurrentUltimateMeter).IsEqualApprox(expected, 0.001f);
        } finally {
            Free(hero);
        }
    }

    [TestCase]
    public void TheCampaignHeroEarnsDamageTakenAndDrainedEchoMeterAtTheStoryScale() {
        PlayerController hero = SpawnCampaignHero();
        try {
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 20f));
            float echo = hero.EchoPool;
            AssertThat(echo > 0f).OverrideFailureMessage("The hit must stash a Rally echo.").IsTrue();
            AssertFloat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("The hit's permanent share earns taken meter at the Story scale.")
                .IsEqualApprox((20f - echo) * InfluenceMeterRules.PointsPerDamageTaken * StoryCombatRules.MeterGainScale, 0.001f);

            HoldInput(hero, GameplayButtons.None,
                BasicComboRules.HitstopFrames(20) + BasicComboRules.EchoDrainFrames + 10);
            AssertThat(hero.EchoPool).IsEqual(0f);
            AssertFloat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("Drained to empty: exactly 20 x 0.25 x 0.5 = 2.5 in total, echo included.")
                .IsEqualApprox(20f * InfluenceMeterRules.PointsPerDamageTaken * StoryCombatRules.MeterGainScale, 0.02f);
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            Free(hero);
        }
    }

    [TestCase]
    public void TheMozartBuildRateLaneMultipliesTheStoryScale() {
        PlayerController hero = SpawnCampaignHero();
        try {
            hero.StoryUltimateBuildRateMultiplier = MinorResonanceRate;
            AssertFloat(hero.StoryMeterGainFactor)
                .IsEqualApprox(StoryCombatRules.MeterGainScale * MinorResonanceRate, 0.0001f);
            hero.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
            AssertFloat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("The Minor Resonance multiplies D5's scale: 20 x 0.5 x 1.1 = 11.")
                .IsEqualApprox(20f * StoryCombatRules.MeterGainScale * MinorResonanceRate, 0.001f);
        } finally {
            Free(hero);
        }
    }

    [TestCase]
    public void FlatGrantsDirectSetsAndSpendsAreNotScaledOnTheCampaignHero() {
        PlayerController hero = SpawnCampaignHero();
        try {
            // A stale live attempt from another suite can seed a spent Defy; this
            // case is about the meter, so it starts from an unspent one.
            hero.SetStoryDefyHistoryUsed(false);
            hero.SetUltimateMeter(50f);
            Meter(hero).AddFlat(5f);
            AssertThat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("A flat grant (Shield of Orléans, the MeterBoost orb) is not a gain.")
                .IsEqual(55f);
            hero.DrainUltimateMeter(30f);
            AssertThat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("A spend is never scaled.")
                .IsEqual(55f - 30f);
            hero.SetUltimateMeter(InfluenceMeterRules.MaxValue);
            AssertThat(hero.CurrentUltimateMeter).IsEqual(InfluenceMeterRules.MaxValue);
            AssertThat(Meter(hero).IsFull).IsTrue();

            hero.ApplyDamage(hero.MaximumHP);
            AssertThat(hero.CurrentHP)
                .OverrideFailureMessage("A full bar set directly still arms Defy History.")
                .IsEqual(1);
            AssertThat(hero.StoryDefyHistoryUsed).IsTrue();
            AssertThat(hero.CurrentUltimateMeter).IsEqual(0f);
        } finally {
            Free(hero);
        }
    }

    [TestCase]
    public void TheUncreditedRallyRecordIsStoredInScaledMeterUnits() {
        PlayerController hero = SpawnCampaignHero();
        try {
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 40f));
            float echo = hero.EchoPool;
            AssertThat(echo > 0f).OverrideFailureMessage("The hit must stash a Rally echo.").IsTrue();
            AssertFloat(hero.RallyUncreditedMeter)
                .OverrideFailureMessage("The outstanding echo is recorded at the rate the drain would credit it.")
                .IsEqualApprox(echo * InfluenceMeterRules.PointsPerDamageTaken * StoryCombatRules.MeterGainScale, 0.001f);
        } finally {
            Free(hero);
        }
    }

    [TestCase]
    public void NonCampaignBuildsKeepTheirLaneWithoutTheStoryScale() {
        // Fighter Mode, the Holodeck and the drills (applyStoryProgression: false),
        // and the Hard Mirror's construction (perks copied, Legacy gate never
        // installed): neither is the campaign hero.
        PlayerController normalized = null;
        PlayerController mirrorShaped = null;
        try {
            normalized = AddToTree(CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false));
            mirrorShaped = AddToTree(CharacterFactory.CreateCharacter(
                "einstein", 1, applyStoryProgression: true, applyLegacyUnlockLocks: false));
            foreach (PlayerController build in new[] { normalized, mirrorShaped }) {
                AssertThat(build.AppliesStoryMeterGainScale)
                    .OverrideFailureMessage("Only the campaign hero carries the D5 scale.")
                    .IsFalse();

                build.StoryUltimateBuildRateMultiplier = MinorResonanceRate;
                AssertFloat(build.StoryMeterGainFactor).IsEqualApprox(MinorResonanceRate, 0.0001f);
                build.SetUltimateMeter(0f);
                build.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
                AssertFloat(build.CurrentUltimateMeter)
                    .OverrideFailureMessage("A non-campaign build keeps its lane and takes no Story scale.")
                    .IsEqualApprox(20f * MinorResonanceRate, 0.0001f);

                build.StoryUltimateBuildRateMultiplier = 1f;
                build.SetUltimateMeter(0f);
                build.AddInfluenceFromDamageDealt(20f, collectsEcho: false);
                AssertThat(build.CurrentUltimateMeter).IsEqual(20f);
            }
        } finally {
            Free(normalized);
            Free(mirrorShaped);
        }
    }

    [TestCase]
    public void TheMeterAndDefyLessonsLethalHitIsDefiedOnTheScaledCampaignHero() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(Level00ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            var player = level.GetNode<PlayerController>("Player");

            // Level00FlowSceneTests' Full-calibration path. Breaking the beam ends
            // the hold, which clears its unlosable ScriptedHPFloor.
            level.BreakTheBeam();
            level.TranslateToBay();
            EventBus.Instance.RaiseDialogueComplete("level_00.intro");
            EventBus.Instance.RaiseDialogueComplete("level_00.wren_offer");
            TwoOptionPrompt prompt = level.CalibrationPrompt;
            AssertObject(prompt).IsNotNull();
            prompt.ChooseFirst();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Calibration);
            AssertThat(player.ScriptedHPFloor).IsEqual(0);

            AssertThat(player.AppliesStoryMeterGainScale)
                .OverrideFailureMessage("The Level 0 hero is the campaign hero.")
                .IsTrue();
            player.StoryUltimateBuildRateMultiplier = 1f;
            AssertFloat(player.StoryMeterGainFactor).IsEqualApprox(StoryCombatRules.MeterGainScale, 0.0001f);
            // A stale live attempt from another suite can seed a spent Defy, which
            // makes the lesson (correctly) skip its grant; this case starts from
            // the fresh attempt the lesson is written for.
            player.SetStoryDefyHistoryUsed(false);
            player.SetUltimateMeter(0f);

            EventBus.Instance.RaiseDialogueComplete("level_00.ultimate_intro");
            AssertThat(Meter(player).CurrentValue)
                .OverrideFailureMessage("The lesson's grant is a direct set, so D5's scale never short-fills it.")
                .IsEqual(InfluenceMeterRules.MaxValue);
            AssertThat(player.CurrentUltimateMeter).IsEqual(InfluenceMeterRules.MaxValue);

            // The lesson's own lethal-tagged hit.
            player.ApplyEnvironmentalDamage(player.CurrentHP);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A full bar on the scaled campaign hero must still defy the lesson's lethal hit.")
                .IsEqual(1);
            AssertThat(player.StoryDefyHistoryUsed).IsTrue();
            AssertThat(player.CurrentUltimateMeter).IsEqual(0f);
        } finally {
            Free(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    // === Helpers ========================================================

    /// <summary>The campaign hero, with the lane pinned to 1.0 and an empty bar.</summary>
    private static PlayerController SpawnCampaignHero() {
        PlayerController hero = AddToTree(CharacterFactory.CreateCharacter("einstein"));
        hero.StoryUltimateBuildRateMultiplier = 1f;
        hero.SetUltimateMeter(0f);
        return hero;
    }

    private static PlayerController AddToTree(PlayerController hero) {
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hero);
        return hero;
    }

    private static UltimateMeter Meter(PlayerController hero) => hero.GetNode<UltimateMeter>("UltimateMeter");

    private static void HoldInput(PlayerController player, GameplayButtons buttons, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
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
