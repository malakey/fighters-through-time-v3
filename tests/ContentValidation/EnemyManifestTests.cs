using System.Collections.Generic;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class EnemyManifestTests {
    private static readonly string[] PrototypeEnemyIDs = {
        "chrono_slasher", "tech_enforcer", "cyber_guard", "steam_automaton", "hologram_drone"
    };

    [TestCase]
    public void PrototypeEnemiesLoadWithUniqueStableIDsAndValidRanges() {
        var ids = new HashSet<string>();
        foreach (string enemyID in PrototypeEnemyIDs) {
            EnemyData data = ResourceLoader.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).IsNotNull();
            AssertThat(data.EnemyID == enemyID).IsTrue();
            AssertThat(ids.Add(data.EnemyID)).IsTrue();
            AssertThat(data.MaxHP > 0).IsTrue();
            AssertThat(data.Weight > 0).IsTrue();
            AssertThat(data.AttackDamage >= 0).IsTrue();
            AssertThat(data.ReactionDelayMinFrames <= data.ReactionDelayMaxFrames).IsTrue();
        }
    }

    [TestCase]
    public void FlorenceBossLoadsFromCanonicalResource() {
        BossData boss = ResourceLoader.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
        AssertObject(boss).IsNotNull();
        AssertThat(boss.BossID == "borgia_inquisitor").IsTrue();
        AssertThat(boss.MaxHP).IsEqual(500);
        AssertThat(boss.PhaseThresholds.Length).IsEqual(1);
    }
}
