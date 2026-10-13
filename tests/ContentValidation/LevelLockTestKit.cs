using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using Godot;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GU-L12 (gameplay update 2026-10-12): the shared, non-suite half of the
/// Levels 12–15 encounter-lock pins. Each of those levels authors three approach
/// locks over a <c>(EnemyID, Wave, Position)</c> spawn table, one lock wave per
/// manifest row, and three anchors with the same baseline shape; this kit states
/// that contract once so the four level suites read it the same way. Not a
/// <c>[TestSuite]</c> (one suite per file): the level suites call it.
/// </summary>
internal static class LevelLockTestKit {

    /// <summary>The 40 px hero body's half-width.</summary>
    public const float HeroHalfWidthPixels = 20f;

    /// <summary>A checkpoint's or font's footprint reaches this far either side of its x.</summary>
    public const float AnchorHalfWidthPixels = 30f;

    /// <summary>One authored lock wave: the manifest row, the table wave it draws and its trigger.</summary>
    public readonly record struct WaveLayout(string RowID, int TableWave, EncounterWaveTrigger Trigger);

    /// <summary>One authored lock: its ID, its area's x-range and its waves, in order.</summary>
    public readonly record struct LockLayout(string LockID, float MinX, float MaxX, WaveLayout[] Waves);

    /// <summary>
    /// FIX-E's measure (Level02ContentTests): the farthest a Story basic string
    /// hit reaches in front of the pivot over the whole roster — every step's far
    /// edge from the Story box resolver, at each hero's reach profile with every
    /// AttackRange minor of its grid bought.
    /// </summary>
    public static float LongestBasicSwingPixels() {
        float longest = 0f;
        foreach (string id in CharacterRoster.IDs) {
            float minor = 0f;
            ResonanceGridData grid = ResonanceProgression.LoadGrid(id);
            foreach (ResonanceNodeData node in grid?.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null && node.StatModifierKey == ResonanceStatKeys.AttackRange) minor += node.StatModifierValue;
            }
            FTT.Combat.BasicStringProfile profile = FTT.Combat.BasicComboRules.StringProfileFor(id);
            for (int step = 0; step < FTT.Combat.BasicComboRules.StringBoxHalfWidthUnits.Length; step++) {
                PlayerController.ResolveGroundStringBoxPixels(step, profile, 1f + minor,
                    out Vector2 size, out Vector2 offset);
                longest = Mathf.Max(longest, offset.X + size.X * 0.5f);
            }
        }
        return longest;
    }

    /// <summary>
    /// Every issue with a built level's locks against its authored layout:
    /// the lock IDs and area x-ranges; each wave's manifest row, trigger and
    /// spawns (exactly the table's entries for that wave, in order, each inside
    /// its lock's area); every table entry claimed by exactly one wave; the locks
    /// Armed at load with only the exit barrier closed; the three anchors'
    /// baselines (none, the first two locks, all three); every checkpoint and the
    /// Restoration Font outside every area; and each Middle and PreBoss strike
    /// surface past the longest basic swing of a hero pressed against the closed
    /// exit of every lock its baseline lists.
    /// </summary>
    public static List<string> LayoutIssues(StoryLevelControllerBase level,
        (string EnemyID, int Wave, Vector2 Position)[] table, LockLayout[] layout) {
        var issues = new List<string>();
        IReadOnlyList<EncounterLock> locks = level.EncounterLocks;
        if (locks.Count != layout.Length) {
            issues.Add($"{locks.Count} locks, the layout authors {layout.Length}");
            return issues;
        }
        var claimedWaves = new HashSet<int>();
        for (int index = 0; index < layout.Length; index++) {
            LockLayout expected = layout[index];
            EncounterLock encounterLock = locks[index];
            string label = $"lock {index} '{encounterLock.LockID}'";
            if (encounterLock.LockID != expected.LockID) issues.Add($"{label} is not '{expected.LockID}'");
            if (!Mathf.IsEqualApprox(encounterLock.Area.Position.X, expected.MinX)
                || !Mathf.IsEqualApprox(encounterLock.Area.End.X, expected.MaxX)) {
                issues.Add($"{label} spans {encounterLock.Area.Position.X:0}..{encounterLock.Area.End.X:0}, not {expected.MinX:0}..{expected.MaxX:0}");
            }
            if (encounterLock.State != EncounterLockState.Armed) issues.Add($"{label} is {encounterLock.State} at load");
            if (encounterLock.ExitBarrier == null || !encounterLock.ExitBarrier.IsClosed) issues.Add($"{label}'s exit is not closed at load");
            if (encounterLock.EntryBarrier == null || encounterLock.EntryBarrier.IsClosed) issues.Add($"{label}'s entry is not open at load");

            EncounterLockWave[] waves = encounterLock.Spec?.Waves ?? Array.Empty<EncounterLockWave>();
            if (waves.Length != expected.Waves.Length) {
                issues.Add($"{label} has {waves.Length} waves, the layout authors {expected.Waves.Length}");
                continue;
            }
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++) {
                WaveLayout expectedWave = expected.Waves[waveIndex];
                EncounterLockWave wave = waves[waveIndex];
                string waveLabel = $"{label} wave '{wave.RowID}'";
                if (wave.RowID != expectedWave.RowID) issues.Add($"{waveLabel} is not row '{expectedWave.RowID}'");
                if (wave.Trigger != expectedWave.Trigger) issues.Add($"{waveLabel} triggers {wave.Trigger}, not {expectedWave.Trigger}");
                if (!claimedWaves.Add(expectedWave.TableWave)) issues.Add($"{waveLabel} claims table wave {expectedWave.TableWave} twice");
                var entries = new List<(string EnemyID, Vector2 Position)>();
                foreach ((string enemyID, int tableWave, Vector2 position) in table) {
                    if (tableWave == expectedWave.TableWave) entries.Add((enemyID, position));
                }
                if (wave.Spawns.Length != entries.Count) {
                    issues.Add($"{waveLabel} spawns {wave.Spawns.Length}, table wave {expectedWave.TableWave} authors {entries.Count}");
                    continue;
                }
                for (int spawnIndex = 0; spawnIndex < entries.Count; spawnIndex++) {
                    EncounterLockSpawn spawn = wave.Spawns[spawnIndex];
                    if (spawn.EnemyID != entries[spawnIndex].EnemyID || spawn.Position != entries[spawnIndex].Position) {
                        issues.Add($"{waveLabel} spawn {spawnIndex} is {spawn.EnemyID}@{spawn.Position}, the table says {entries[spawnIndex].EnemyID}@{entries[spawnIndex].Position}");
                    }
                    if (spawn.Position.X <= encounterLock.Area.Position.X || spawn.Position.X >= encounterLock.Area.End.X) {
                        issues.Add($"{waveLabel} spawns {spawn.EnemyID} at {spawn.Position}, outside its area");
                    }
                }
            }
        }
        foreach ((string enemyID, int tableWave, Vector2 position) in table) {
            if (!claimedWaves.Contains(tableWave)) issues.Add($"table wave {tableWave} ({enemyID}@{position}) belongs to no lock");
        }

        // The three anchors: none, the first two locks, all three.
        IReadOnlyDictionary<string, string[]> baselines = level.AuthoredEncounterBaselines;
        string levelID = level.LevelID;
        string[] ids = new string[layout.Length];
        for (int index = 0; index < layout.Length; index++) ids[index] = layout[index].LockID;
        ExpectBaseline(baselines, $"{levelID}_checkpoint_0", Array.Empty<string>(), issues);
        ExpectBaseline(baselines, $"{levelID}_checkpoint_1", new[] { ids[0], ids[1] }, issues);
        ExpectBaseline(baselines, $"{levelID}_checkpoint_2", ids, issues);

        // Anchors outside every area; Middle/PreBoss surfaces out of swing reach.
        float reach = LongestBasicSwingPixels();
        foreach (Node child in Children(level)) {
            if (child is CheckpointTrigger checkpoint) {
                float x = checkpoint.GlobalPosition.X;
                foreach (EncounterLock encounterLock in locks) {
                    if (x + AnchorHalfWidthPixels > encounterLock.Area.Position.X && x - AnchorHalfWidthPixels < encounterLock.Area.End.X) {
                        issues.Add($"{checkpoint.CheckpointID} (x {x:0}) stands inside '{encounterLock.LockID}'");
                    }
                }
                if (checkpoint.Role == CheckpointRole.Entry) continue;
                if (!baselines.TryGetValue(checkpoint.CheckpointID, out string[] listed)) continue;
                foreach (EncounterLock encounterLock in locks) {
                    if (Array.IndexOf(listed, encounterLock.LockID) < 0 || encounterLock.ExitBarrier == null) continue;
                    float pivot = encounterLock.ExitBarrier.Footprint.Position.X - HeroHalfWidthPixels;
                    if (pivot + reach >= x - AnchorHalfWidthPixels) {
                        issues.Add($"a swing from inside '{encounterLock.LockID}' reaches x {pivot + reach:0}; "
                            + $"{checkpoint.CheckpointID}'s surface starts at {x - AnchorHalfWidthPixels:0}");
                    }
                }
            } else if (child is RestorationFont font) {
                float x = font.GlobalPosition.X;
                foreach (EncounterLock encounterLock in locks) {
                    if (x + AnchorHalfWidthPixels > encounterLock.Area.Position.X && x - AnchorHalfWidthPixels < encounterLock.Area.End.X) {
                        issues.Add($"the font (x {x:0}) stands inside '{encounterLock.LockID}'");
                    }
                }
            }
        }
        return issues;
    }

    private static void ExpectBaseline(IReadOnlyDictionary<string, string[]> baselines, string checkpointID,
        string[] expected, List<string> issues) {
        if (!baselines.TryGetValue(checkpointID, out string[] actual) || actual == null) {
            issues.Add($"{checkpointID} has no baseline");
            return;
        }
        if (string.Join(",", actual) != string.Join(",", expected)) {
            issues.Add($"{checkpointID} lists [{string.Join(",", actual)}], expected [{string.Join(",", expected)}]");
        }
    }

    // === Real frames =========================================================

    public static async Task Frames(int count) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>Plays out any active dialogue (the entrance beat, a scripted line) so a paused tree runs again.</summary>
    public static void SkipDialogue() {
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
    }

    /// <summary>Puts the hero on the floor at <paramref name="x"/>, still.</summary>
    public static void PlaceHero(PlayerController hero, float x, float feetY) {
        hero.GlobalPosition = new Vector2(x, feetY);
        hero.Velocity = Vector2.Zero;
    }

    /// <summary>
    /// Kills every living member of an engaged lock, round after round (each
    /// round lets a reinforcement wave spawn), until it clears or the rounds
    /// run out. Returns true once it is Cleared.
    /// </summary>
    public static async Task<bool> ClearByForce(EncounterLock encounterLock, int rounds = 6) {
        for (int round = 0; round < rounds && encounterLock.State != EncounterLockState.Cleared; round++) {
            foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
            SkipDialogue();
            await Frames(3);
            SkipDialogue();
        }
        return encounterLock.State == EncounterLockState.Cleared;
    }

    public static List<Node> Children(Node parent) {
        var collected = new List<Node>();
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) collected.Add(child);
        return collected;
    }
}
