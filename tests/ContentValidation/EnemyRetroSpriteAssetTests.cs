using System.Collections.Generic;
using System.Text.RegularExpressions;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class EnemyRetroSpriteAssetTests {
    private static readonly StringName[] EnemyAnimations = {
        "idle", "patrol", "attack", "elite_attack", "hitstun", "death"
    };

    private static readonly StringName[] BossAnimations = {
        "idle", "move", "melee_attack", "ranged_attack", "phase_transition", "death"
    };

    private static IEnumerable<string> ResourcePaths(string directory) {
        using DirAccess access = DirAccess.Open(directory);
        foreach (string file in access.GetFiles()) {
            if (file.EndsWith(".tres")) yield return $"{directory}/{file}";
        }
    }

    private static void CheckAnimations(SpriteFrames frames, IReadOnlyList<StringName> names,
        string source, List<string> issues) {
        if (frames == null) { issues.Add($"{source}: SpriteFramesResource missing"); return; }
        foreach (StringName name in names) {
            if (!frames.HasAnimation(name)) { issues.Add($"{source}: missing {name}"); continue; }
            if (frames.GetFrameCount(name) != 3) {
                issues.Add($"{source}: {name} has {frames.GetFrameCount(name)} frames, expected 3");
            }
            for (int frame = 0; frame < frames.GetFrameCount(name); frame++) {
                if (frames.GetFrameTexture(name, frame) == null) {
                    issues.Add($"{source}: {name}[{frame}] texture missing");
                }
            }
        }
    }

    [TestCase]
    public void EveryEnemyUsesTheSixAnimationRetroContract() {
        var issues = new List<string>();
        int inspected = 0;
        foreach (string path in ResourcePaths("res://resources/Enemies")) {
            EnemyData data = AuthoredResources.Load<EnemyData>(path);
            inspected++;
            if (data == null) issues.Add($"{path}: failed to load");
            else CheckAnimations(data.SpriteFramesResource, EnemyAnimations, path, issues);
        }
        AssertThat(inspected).IsEqual(27);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryScriptedBossExceptTheMirrorUsesTheSixAnimationRetroContract() {
        var issues = new List<string>();
        int inspected = 0;
        foreach (string path in ResourcePaths("res://resources/Bosses")) {
            if (path.EndsWith("/mirror_paradox.tres")) continue;
            BossData data = AuthoredResources.Load<BossData>(path);
            inspected++;
            if (data == null) issues.Add($"{path}: failed to load");
            else CheckAnimations(data.SpriteFramesResource, BossAnimations, path, issues);
        }
        AssertThat(inspected).IsEqual(14);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryAuthoredEnemyAbilityPresentationEventResolvesRetroVfx() {
        var pattern = new Regex(@"PresentationEventID\s*=\s*""([^""]+)""");
        var issues = new List<string>();
        int inspected = 0;
        foreach (string root in new[] { "res://resources/Enemies/Abilities", "res://resources/Bosses/Abilities" }) {
            Walk(root, pattern, issues, ref inspected);
        }
        AssertThat(inspected).IsEqual(76);
        foreach (string fallback in new[] {
            "chrono_rioter.basic", "chrono_slasher.basic",
            "rift_phantom.basic", "shock_shield_legionnaire.basic"
        }) {
            if (!EnemyAbilityVisualLibrary.TryResolve(fallback, out _, out _, out _)) {
                issues.Add($"{fallback}: generated basic VFX missing");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static void Walk(string directory, Regex pattern, List<string> issues,
        ref int inspected) {
        using DirAccess access = DirAccess.Open(directory);
        if (access == null) { issues.Add($"{directory}: cannot open"); return; }
        foreach (string sub in access.GetDirectories()) Walk($"{directory}/{sub}", pattern, issues, ref inspected);
        foreach (string file in access.GetFiles()) {
            if (!file.EndsWith(".tres")) continue;
            string path = $"{directory}/{file}";
            Match match = pattern.Match(Godot.FileAccess.GetFileAsString(path));
            if (!match.Success) continue;
            inspected++;
            string eventID = match.Groups[1].Value;
            if (!EnemyAbilityVisualLibrary.TryResolve(eventID, out SpriteFrames frames,
                    out StringName animation, out _)) {
                issues.Add($"{path}: {eventID} unresolved");
            } else if (frames.GetFrameCount(animation) != 3) {
                issues.Add($"{path}: {eventID} has {frames.GetFrameCount(animation)} frames");
            }
        }
    }
}
