using System.Collections.Generic;
using System.Text.RegularExpressions;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W8: Story kit deliveries that resolve through a shape query straight
/// into <see cref="Hurtbox.TakeHit"/> now reach <c>PersistentObject</c>
/// environment strike surfaces (checkpoint fractures, extractors, strikeable
/// props) the way a <see cref="Hitbox"/> does, through the one shared
/// <see cref="StoryShapeQuery.DeliveryMask"/>. One real cast per character
/// family against an <see cref="EnvironmentHurtboxAdapter"/>, plus the mask rule,
/// the documented exclusions and the Time Freeze discard.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShapeQueryPropDeliveryTests {
    private const double Step = 1.0 / 60.0;
    private const int MaxFrames = 900;
    private static readonly Vector2 PlayerOrigin = new(-30000f, 900f);

    private sealed class PropProbe {
        public EnvironmentHurtboxAdapter Surface;
        public int Hits;
        public int LastAttacker = int.MinValue;
    }

    private static PropProbe AddProp(Node host, Vector2 center) {
        var probe = new PropProbe();
        probe.Surface = new EnvironmentHurtboxAdapter {
            Name = "StrikeableProp",
            OwnerPlayerIndex = -1,
            CollisionLayer = CollisionLayers.PersistentObject,
            CollisionMask = CollisionLayers.PlayerHitbox,
            Monitoring = true,
            Monitorable = true,
            Position = center
        };
        probe.Surface.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(1600f, 900f) }
        });
        probe.Surface.OnHit += payload => {
            probe.Hits++;
            probe.LastAttacker = payload.AttackerIndex;
            return 0f;
        };
        host.AddChild(probe.Surface);
        return probe;
    }

    private static Node CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var floor = new StaticBody2D {
            Name = "Floor",
            CollisionLayer = CollisionLayers.Environment,
            Position = PlayerOrigin + new Vector2(0f, 20f)
        };
        floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) } });
        host.AddChild(floor);
        return host;
    }

    /// <summary>Casts one slot and ticks it until the prop is struck or the cast ends.</summary>
    private static void AssertCastStrikesProp(string characterID, string slotNode, bool ultimate) {
        Node host = CreateHost($"PropDelivery_{characterID}");
        PlayerController player = null;
        try {
            player = CharacterFactory.CreateCharacter(characterID, 0);
            player.Position = PlayerOrigin;
            host.AddChild(player);
            PropProbe prop = AddProp(host, PlayerOrigin + new Vector2(0f, -100f));
            int hpBefore = player.CurrentHP;
            if (ultimate) player.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue);

            var ability = player.GetNode<BaseSpecial>(slotNode);
            AssertThat(ability.TryExecute())
                .OverrideFailureMessage($"{characterID} {slotNode} refused to cast.")
                .IsTrue();
            for (int frame = 0; frame < MaxFrames && prop.Hits == 0 && ability.IsExecuting; frame++) {
                ability._PhysicsProcess(Step);
            }

            AssertThat(prop.Hits)
                .OverrideFailureMessage(
                    $"{characterID} {slotNode} ({ability.GetType().Name}) never struck the PersistentObject prop.")
                .IsGreater(0);
            AssertThat(prop.LastAttacker).IsEqual(player.PlayerIndex);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A shape-query delivery reached its own caster.")
                .IsEqual(hpBefore);
        } finally {
            if (player != null) InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            host.Free();
        }
    }

    // === The mask rule =======================================================

    [TestCase]
    public void TheDeliveryMaskAddsPersistentObjectsAndNeverTheCastersOwnLayer() {
        uint heroMask = StoryShapeQuery.DeliveryMask(CollisionLayers.EnemyHurtbox);
        AssertThat(heroMask).IsEqual(CollisionLayers.HitboxMaskForFighterSlot(0));
        AssertThat(heroMask & CollisionLayers.PlayerHurtbox).IsEqual(0u);
        AssertThat(StoryShapeQuery.DeliveryMask(CollisionLayers.PlayerHurtbox))
            .IsEqual(CollisionLayers.HitboxMaskForFighterSlot(1));
    }

    [TestCase]
    public void EveryDamagingKitShapeQueryUsesTheSharedDeliveryMask() {
        // The documented exclusions: pure displacement with no damage. The four
        // autonomous construct nodes (*Node.cs) are target acquisition and are
        // outside this sweep by file.
        var allowedBareMaskMethods = new HashSet<string> { "PushAdjacentTargets", "ApplyTornadoLift" };
        var methodPattern = new Regex(@"^\s*(?:public|private|protected|internal)[^=;(]*\s(\w+)\s*\(");
        var issues = new List<string>();
        int shared = 0;
        foreach (string file in DirAccess.GetFilesAt("res://scripts/Characters/Abilities")) {
            if (!file.EndsWith(".cs") || file.EndsWith("Node.cs")) continue;
            string path = "res://scripts/Characters/Abilities/" + file;
            string[] lines = FileAccess.GetFileAsString(path).Split('\n');
            string method = "";
            for (int i = 0; i < lines.Length; i++) {
                Match match = methodPattern.Match(lines[i]);
                if (match.Success) method = match.Groups[1].Value;
                string line = lines[i].Trim();
                if (line.Contains("StoryShapeQuery.DeliveryMask(")) shared++;
                if (line == "CollisionMask = targetHurtboxLayer" && !allowedBareMaskMethods.Contains(method)) {
                    issues.Add($"{file}:{i + 1} ({method}) masks only the hurtbox layer");
                }
            }
        }
        AssertThat(shared).IsGreaterEqual(19);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // === One real cast per character family ==================================

    [TestCase]
    public void EinsteinsCosmologicalConstantStrikesAProp() => AssertCastStrikesProp("einstein", "Ultimate", true);

    [TestCase]
    public void JoansDivinePiercingStrikesAProp() => AssertCastStrikesProp("joan", "Special2", false);

    [TestCase]
    public void LeonardosGoldenRatioStrikesAProp() => AssertCastStrikesProp("leonardo", "Special1", false);

    [TestCase]
    public void LincolnsEmancipatorStrikesAProp() => AssertCastStrikesProp("lincoln", "Special1", false);

    [TestCase]
    public void MozartsSymphonyOfSorrowStrikesAProp() => AssertCastStrikesProp("mozart", "Ultimate", true);

    [TestCase]
    public void PocahontassTidewaterTempestStrikesAProp() => AssertCastStrikesProp("pocahontas", "Ultimate", true);

    [TestCase]
    public void ShakespearesAllTheWorldsAStageStrikesAProp() => AssertCastStrikesProp("shakespeare", "Ultimate", true);

    [TestCase]
    public void TeslasLorentzPulseStrikesAProp() => AssertCastStrikesProp("tesla", "Special2", false);

    [TestCase]
    public void CleopatrasWrathOfTheNileStrikesAProp() => AssertCastStrikesProp("cleopatra", "Ultimate", true);

    // === Gates ===============================================================

    [TestCase]
    public void AFrozenWorldDiscardsAPlayerStrikeOnAPropButNotAnUnattributedOne() {
        Node host = CreateHost("PropDeliveryFreeze");
        StoryManager.Instance?.SetTimeFreezeCooldown(0f);
        var freeze = new TimeFreezeController { Name = "PropDeliveryTimeFreeze" };
        host.AddChild(freeze);
        freeze.SetPhysicsProcess(false);
        PropProbe prop = AddProp(host, PlayerOrigin);
        try {
            AssertThat(freeze.TryBeginTimeFreeze()).IsTrue();
            prop.Surface.TakeHit(new HitPayload { AttackerIndex = 0, Damage = 10f });
            AssertThat(prop.Hits)
                .OverrideFailureMessage("A player strike reached a prop during Time Freeze.")
                .IsEqual(0);
            prop.Surface.TakeHit(new HitPayload { AttackerIndex = -1, Damage = 10f });
            AssertThat(prop.Hits).IsEqual(1);

            freeze.EndFreeze(early: true);
            prop.Surface.TakeHit(new HitPayload { AttackerIndex = 0, Damage = 10f });
            AssertThat(prop.Hits).IsEqual(2);
        } finally {
            if (freeze.IsFrozen) freeze.EndFreeze(early: true);
            StoryManager.Instance?.SetTimeFreezeCooldown(0f);
            host.Free();
        }
    }
}
