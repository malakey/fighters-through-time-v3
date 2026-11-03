using System;
using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Gameplay review 2026-11-03 (rulings 2b, 2b+; plan section 1.12): traits are soft
/// counters, never strictly required. The carriers are the three the plan names,
/// each a grabbable Standard mob, and no required objective of any encounter lock
/// in any campaign scene sits behind a trait mob.
///
/// <para><b>What "behind" means.</b> A lock's members wake and chase on engage and
/// no mob body blocks the hero (body masks are separate, pushboxes are soft), so a
/// mob standing between the entry and an objective on the floor holds nothing: the
/// hero walks or rolls past it, or draws it off. What a trait mob can really hold
/// is an objective the hero must leave the floor to work, under the mob's cover:
/// the jump into it is what Anti-air punishes, and a fight on a perch is where a
/// thrown Armoured mob or a Catcher's counter costs most. That is the rule swept
/// here (<see cref="SitsBehind"/>; the handoff records the choice).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyTraitContentTests {

    /// <summary>A hero on the floor works an objective up to this high (64 px of body and a swing's reach).</summary>
    private const float WorkableFromFloorPixels = 180f;
    /// <summary>A mob's post covers an objective this far along: the rising strike's reach and a hero's approach.</summary>
    private const float MobCoverPixels = 320f;

    private static readonly Dictionary<string, EnemyTrait> RuledCarriers = new(StringComparer.Ordinal) {
        ["shock_shield_legionnaire"] = EnemyTrait.Armoured,
        ["cyber_guard"] = EnemyTrait.Catcher,
        ["plasma_spear_ward"] = EnemyTrait.AntiAir
    };

    [TestCase]
    public void TheCarriersAreExactlyTheThreeRuledGrabbableStandardMobs() {
        var issues = new List<string>();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in DirAccess.GetFilesAt("res://resources/Enemies/")) {
            if (!file.EndsWith(".tres", StringComparison.Ordinal)) continue;
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{file}");
            if (data == null || data.Trait == EnemyTrait.None) continue;
            found.Add(data.EnemyID);
            if (!RuledCarriers.TryGetValue(data.EnemyID, out EnemyTrait ruled)) {
                issues.Add($"{data.EnemyID} carries {data.Trait}, which no ruling gives it");
                continue;
            }
            if (data.Trait != ruled) issues.Add($"{data.EnemyID} carries {data.Trait}, ruled {ruled}");
            // A throw must be open on every carrier, or Armoured has no answer at all.
            if (data.Tier != EnemyTier.Standard) issues.Add($"{data.EnemyID} is {data.Tier}: traits sit on Standard mobs only");
            if (data.FrontalDamageReduction != 0f) {
                issues.Add($"{data.EnemyID} keeps FrontalDamageReduction {data.FrontalDamageReduction}: it sits on the path a throw takes");
            }
        }
        foreach (string carrier in RuledCarriers.Keys) {
            if (!found.Contains(carrier)) issues.Add($"{carrier} carries no trait");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheBehindRuleFailsARaisedObjectiveUnderAMobsCoverAndNothingElse() {
        const float floor = 900f;
        // Raised over the mob's post: the jump goes into its cover.
        AssertThat(SitsBehind(new Vector2(1000f, floor - 300f), floor, new Vector2(1100f, floor))).IsTrue();
        // On either side of the post.
        AssertThat(SitsBehind(new Vector2(1000f, floor - 300f), floor, new Vector2(900f, floor))).IsTrue();
        // The mob holds the perch itself.
        AssertThat(SitsBehind(new Vector2(1000f, floor - 300f), floor, new Vector2(1000f, floor - 300f))).IsTrue();
        // On the floor: nothing is held, however close the post.
        AssertThat(SitsBehind(new Vector2(1000f, floor - 80f), floor, new Vector2(1000f, floor))).IsFalse();
        // Raised, but well outside the post's cover.
        AssertThat(SitsBehind(new Vector2(1000f, floor - 300f), floor, new Vector2(1000f + MobCoverPixels + 1f, floor))).IsFalse();
    }

    [TestCase]
    public void NoRequiredObjectiveSitsBehindATraitMobInAnyCampaignScene() {
        var issues = new List<string>();
        int objectiveLocks = 0;
        int traitMembersBesideObjectives = 0;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int index = 0; index <= 15; index++) {
            string path = StoryManager.GetLevelScenePath((CampaignLevel)index);
            SweepLevel(tree, $"Level{index:00}", path, issues, ref objectiveLocks, ref traitMembersBesideObjectives);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(objectiveLocks).OverrideFailureMessage("No lock requires an objective: the sweep is vacuous.").IsGreater(0);
        AssertThat(traitMembersBesideObjectives)
            .OverrideFailureMessage("No trait mob stands in a lock with a required objective: the sweep is vacuous.")
            .IsGreater(0);
    }

    /// <summary>
    /// An objective the hero cannot work from the floor (<see cref="WorkableFromFloorPixels"/>),
    /// within a trait mob's post's cover. The mob's own height does not matter: on
    /// the floor below or on the same perch, the climb goes into its reach.
    /// </summary>
    private static bool SitsBehind(Vector2 objective, float floorY, Vector2 mobPost) =>
        floorY - objective.Y > WorkableFromFloorPixels
        && Mathf.Abs(mobPost.X - objective.X) <= MobCoverPixels;

    private static void SweepLevel(SceneTree tree, string label, string scenePath, List<string> issues,
        ref int objectiveLocks, ref int traitMembers) {
        bool originalPaused = tree.Paused;
        SessionData originalSession = GameManager.Instance.CurrentSession;
        Node level = null;
        try {
            SessionData session = originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;
            PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene == null) { issues.Add($"{label}: scene '{scenePath}' did not load"); return; }
            level = scene.Instantiate();
            level.Name = $"{label}_EnemyTraitContent";
            tree.Root.AddChild(level);
            var locks = new List<EncounterLock>();
            CollectLocks(level, locks);
            foreach (EncounterLock encounterLock in locks) {
                List<(string Name, Vector2 Position)> objectives = ObjectivesOf(tree, encounterLock);
                if (objectives.Count == 0) continue;
                objectiveLocks++;
                foreach (EncounterLockWave wave in encounterLock.Spec?.Waves ?? Array.Empty<EncounterLockWave>()) {
                    foreach (EncounterLockSpawn spawn in wave?.Spawns ?? Array.Empty<EncounterLockSpawn>()) {
                        if (spawn == null) continue;
                        if (EnemyFactory.LoadData(spawn.EnemyID)?.Trait is null or EnemyTrait.None) continue;
                        traitMembers++;
                        foreach ((string name, Vector2 position) in objectives) {
                            if (!SitsBehind(position, encounterLock.Spec.BarrierFloorY, spawn.Position)) continue;
                            issues.Add($"{label}: {name} at {position} in lock '{encounterLock.LockID}' sits behind {spawn.EnemyID} at {spawn.Position}");
                        }
                    }
                }
            }
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession = originalSession;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>A lock's required breakables and, when it requires a puzzle, every puzzle object in its area.</summary>
    private static List<(string Name, Vector2 Position)> ObjectivesOf(SceneTree tree, EncounterLock encounterLock) {
        var objectives = new List<(string, Vector2)>();
        foreach (DamageableEnvironmentObject breakable in encounterLock.Spec?.RequiredBreakables ?? Array.Empty<DamageableEnvironmentObject>()) {
            if (breakable != null) objectives.Add((breakable.Name, breakable.GlobalPosition));
        }
        if ((encounterLock.Spec?.RequiredPuzzleIDs?.Length ?? 0) == 0) return objectives;
        Godot.Collections.Array<Node> nodes = tree.GetNodesInGroup("puzzle_object");
        using (nodes.AsDisposable()) {
            foreach (Node node in nodes) {
                if (node is not Node2D placed || !encounterLock.Area.HasPoint(placed.GlobalPosition)) continue;
                if (placed is PathMovingPlatform or RotatingPlatform or PendulumAnchor) continue; // the way there, not the objective
                objectives.Add((placed.Name, placed.GlobalPosition));
            }
        }
        return objectives;
    }

    private static void CollectLocks(Node node, List<EncounterLock> into) {
        if (node is EncounterLock encounterLock) into.Add(encounterLock);
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) CollectLocks(child, into);
    }
}
