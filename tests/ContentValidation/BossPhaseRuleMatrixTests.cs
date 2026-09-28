using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W9 (GAP-07): design §6 "Level Bosses", Rule 1 — "Every phase
/// transition changes a <i>rule</i>, never just a speed multiplier."
///
/// <para>
/// For each of the fifteen campaign bosses (the non-recursive
/// <c>resources/Bosses</c> sweep; the nine Level 4A duplicates under
/// <c>legacy/</c> are HELD under plan D13 and audited in the W9 handoff instead)
/// every phase after the first must bring at least one rule, from either side:
/// </para>
/// <list type="bullet">
/// <item>boss data — an ability first unlocked in that phase, the T01b
/// historical recovery (the first threshold), the Borrowed Legacies (the final
/// phase), a squad's member-defeat hand-off, a teleport decoy, a guarded summon
/// scene, or arena guardians; or</item>
/// <item>the arena — a level controller that subscribes to the boss's phase
/// change and reshapes the arena, listed explicitly below and checked against
/// the controller's source.</item>
/// </list>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossPhaseRuleMatrixTests {
    private const string BossDirectory = "res://resources/Bosses";

    /// <summary>
    /// Arena-side phase rules: boss ID -> (phase index, controller source file,
    /// the subscription that proves the hook is wired).
    /// </summary>
    private static readonly (string BossID, int Phase, string Source, string Hook)[] ArenaRules = {
        ("borgia_inquisitor", 1, "scripts/Environment/Level01Controller.cs", "PhaseEntered += OnBossPhaseEntered"),
        ("chronal_inventor", 1, "scripts/Environment/Level03Controller.cs", "PhaseEntered += OnInventorPhaseEntered"),
        ("revolutionary_tribunal", 1, "scripts/Environment/Level04Controller.cs", "PhaseEntered += OnTribunalPhaseEntered"),
        ("tidal_eraser", 1, "scripts/Environment/Level05Controller.cs", "OnBossPhaseChanged += OnBossPhaseChanged"),
        ("tragedy_king", 1, "scripts/Environment/Level10Controller.cs", "PhaseEntered += OnKingPhaseEntered"),
        ("archive_prime", 1, "scripts/Environment/Level14Controller.cs", "OnBossPhaseChanged += OnBossPhaseChanged"),
        ("archive_prime", 2, "scripts/Environment/Level14Controller.cs", "OnBossPhaseChanged += OnBossPhaseChanged")
    };

    [TestCase]
    public void EveryCampaignBossPhaseAfterTheFirstChangesARuleNotJustASpeedMultiplier() {
        List<BossData> bosses = LoadCampaignBosses();
        AssertThat(bosses.Count).OverrideFailureMessage("The campaign boss sweep is broken.").IsEqual(15);

        var violations = new List<string>();
        foreach (BossData boss in bosses) {
            for (int phase = 1; phase < boss.PhaseCount; phase++) {
                List<string> rules = RulesEnteringPhase(boss, phase);
                if (rules.Count == 0) {
                    violations.Add($"{boss.BossID} phase {phase + 1} adds only "
                        + $"x{boss.GetPhaseSpeedMultiplier(phase):0.##} speed");
                }
            }
        }
        if (violations.Count > 0) AssertThat(string.Join(" | ", violations)).IsEqual("");
    }

    [TestCase]
    public void EveryArenaSidePhaseRuleIsActuallySubscribedByItsLevelController() {
        var issues = new List<string>();
        foreach ((string bossID, int phase, string source, string hook) in ArenaRules) {
            if (!File.Exists(source)) {
                issues.Add($"{bossID} phase {phase + 1}: {source} is missing");
                continue;
            }
            string text = File.ReadAllText(source);
            if (!text.Contains(hook)) issues.Add($"{bossID} phase {phase + 1}: {source} lacks '{hook}'");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheBorgiaInquisitorsSecondPhaseCarriesNoSpeedMultiplierAtAll() {
        // M18 names this one explicitly: "no speed multiplier".
        BossData borgia = AuthoredResources.Load<BossData>($"{BossDirectory}/borgia_inquisitor.tres");
        for (int phase = 0; phase < borgia.PhaseCount; phase++) {
            AssertFloat(borgia.GetPhaseSpeedMultiplier(phase)).IsEqualApprox(1f, 0.0001f);
        }
        List<string> rules = RulesEnteringPhase(borgia, 1);
        AssertThat(rules.Contains("ability:boss.borgia_inquisitor.after_image_dash")).IsTrue();
        AssertThat(rules.Contains("arena:Level01Controller.cs")).IsTrue();
    }

    // === The rule enumerator =============================================

    private static List<string> RulesEnteringPhase(BossData boss, int phase) {
        var rules = new List<string>();
        if (boss.BossAbilities != null) {
            for (int index = 0; index < boss.BossAbilities.Length; index++) {
                if (boss.BossAbilities[index] != null && boss.GetAbilityMinPhase(index) == phase) {
                    rules.Add($"ability:{boss.BossAbilities[index].AbilityID}");
                }
            }
        }
        if (boss.HasHistoricalRecovery && phase == 1) rules.Add("historical_recovery");
        if (boss.BorrowsRosterLegacies && phase == boss.PhaseCount - 1) rules.Add("borrowed_legacies");
        if (boss.PhaseTrigger == BossPhaseTrigger.MemberDefeat && boss.IsSquad && phase == 1) {
            rules.Add("squad_absorb");
        }
        if (boss.TeleportDecoyMinPhase == phase) rules.Add("teleport_decoy");
        if (boss.GuardedSummonMinPhase == phase) rules.Add("guarded_summon");
        if (boss.ArenaGuardianMinPhase == phase) rules.Add("arena_guardians");
        foreach ((string bossID, int rulePhase, string source, string _) in ArenaRules) {
            if (bossID == boss.BossID && rulePhase == phase) rules.Add($"arena:{Path.GetFileName(source)}");
        }
        return rules;
    }

    private static List<BossData> LoadCampaignBosses() {
        var bosses = new List<BossData>();
        using DirAccess access = DirAccess.Open(BossDirectory);
        if (access == null) return bosses;
        foreach (string file in access.GetFiles()) {
            if (!file.EndsWith(".tres")) continue;
            BossData boss = AuthoredResources.Load<BossData>($"{BossDirectory}/{file}");
            if (boss != null) bosses.Add(boss);
        }
        return bosses;
    }
}
