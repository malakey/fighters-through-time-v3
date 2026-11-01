using System.Collections.Generic;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W7a (T02, A05, D12) — the Story Lorentz Pulse: it roots for 1.0 s, Lorentz
/// Attraction adds 0.5 s, and the Minor Pulse Radius node widens it. (Its T01 mark chain,
/// "each coil within 8 units fires an 8-damage arc at a marked target", was replaced by the coil
/// discharge in the lingering-kit pass: <c>StoryLorentzDischargeTests</c>.)
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryLorentzChainTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-58000f, 600f);

    [TestCase]
    public void ThePulseRootsForOneSecondAndAttractionAddsHalfASecond() {
        AbilityData pulse = AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/special_2.tres");
        AssertThat(pulse.AppliedStatus).IsEqual(StatusType.Root);
        AssertThat(pulse.StatusDuration).IsEqual(1f);
        AssertThat(TeslaLorentzPulse.AttractionRootBonusSeconds).IsEqual(0.5f);
    }

    /// <summary>
    /// Minor Pulse Radius (<c>tesla_pulse_radius</c>, <c>AbilityRange</c> scoped
    /// to the Lorentz Pulse, +15%) used to have no reader: the pulse queried a
    /// fixed 2.5-unit circle. Through the real grid it now widens the pulse to
    /// 172.5 px. An enemy whose near hurtbox edge sits 161 px away is outside
    /// the base pulse and inside the widened one.
    /// </summary>
    [TestCase]
    public void TheBoughtPulseRadiusNodeWidensThePulseByFifteenPercent() {
        float basePixels = KitMotionRules.LorentzPulseRadiusUnits * KitMotionRules.StoryPixelsPerUnit;
        // StandardEnemy's hurtbox is 46 px wide, centred on its feet: its near
        // edge is 23 px closer than its feet.
        const float EnemyFeetOffset = 184f;

        (int baseLoss, float baseRadius) = PulseEnemyAt("PulseBase", EnemyFeetOffset, ScopedStoryStats.Empty);
        AssertFloat(baseRadius).IsEqualApprox(basePixels, 0.01f);
        AssertThat(baseLoss)
            .OverrideFailureMessage("Unbought, the 150 px pulse must miss a hurtbox edge 161 px away.")
            .IsEqual(0);

        ResonanceGridData grid = ResonanceProgression.LoadGrid("tesla");
        var save = new StorySaveData { SelectedCharacterID = "tesla" };
        save.GridProgress["tesla"] = new List<string> { "tesla_cane_edge", "tesla_pulse_radius" };
        var lanes = new ScopedStoryStats(ResonanceProgression.Resolve(grid, save).ScopedMultipliers);
        (int wideLoss, float wideRadius) = PulseEnemyAt("PulseWide", EnemyFeetOffset, lanes);
        AssertFloat(wideRadius).IsEqualApprox(basePixels * 1.15f, 0.01f);
        AssertThat(wideLoss > 0)
            .OverrideFailureMessage("The bought node must widen the pulse onto that enemy.")
            .IsTrue();
    }

    private static (int Loss, float Radius) PulseEnemyAt(string name, float offsetX, ScopedStoryStats lanes) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController tesla = CharacterFactory.CreateCharacter("tesla", 0, applyStoryProgression: false);
            host.AddChild(tesla);
            tesla.GlobalPosition = Origin;
            tesla.StoryScopedStats = lanes;

            EnemyController enemy = CreateEnemy("chrono_slasher", host, Origin + new Vector2(offsetX, 0f));
            int hpBefore = enemy.CurrentHP;

            var lorentz = tesla.GetNode<TeslaLorentzPulse>("Special2");
            float radius = lorentz.PulseRadius;
            AssertThat(lorentz.TryExecute()).IsTrue();
            for (int frame = 0; frame < 120 && lorentz.IsExecuting; frame++) lorentz._PhysicsProcess(Step);
            return (hpBefore - enemy.CurrentHP, radius);
        } finally {
            host.Free();
        }
    }

    private static EnemyController CreateEnemy(string enemyID, Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        // Positioned before entering the tree, so the physics server registers
        // its hurtbox where the shape queries will look.
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }
}
