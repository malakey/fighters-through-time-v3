using System.Collections.Generic;
using System.Text.RegularExpressions;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B6: every authored roster <c>PresentationEventID</c> must resolve to a
/// concrete effect family, not to the generic fallback.
///
/// <para>The 27 enemies and 15 bosses author 76 hook strings across their
/// <c>EnemyAbilityData</c> kits, plus one <c>{enemyID}.death</c> each raised by
/// <c>EnemyController</c>. Before Package 8 nothing consumed any of them; A3 gave
/// them one generic effect and left the ID unread. This sweep is what stops the
/// mapping decaying back to that: a new roster ability named with a verb outside the
/// vocabulary fails here with its file name rather than shipping a silently generic
/// puff.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RosterVfxMappingTests {

    private static readonly string[] ScannedDirectories = {
        "res://resources/Bosses", "res://resources/Enemies"
    };

    private static readonly Regex EventID =
        new(@"PresentationEventID\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex EnemyID =
        new(@"^EnemyID\s*=\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.Multiline);

    private static List<(string File, string EventID)> CollectEventIDs() {
        var found = new List<(string, string)>();
        foreach (string directory in ScannedDirectories) Walk(directory, found, EventID);
        return found;
    }

    private static void Walk(string directory, List<(string, string)> found, Regex pattern) {
        using DirAccess access = DirAccess.Open(directory);
        if (access == null) return;
        foreach (string sub in access.GetDirectories()) Walk($"{directory}/{sub}", found, pattern);
        foreach (string file in access.GetFiles()) {
            if (!file.EndsWith(".tres")) continue;
            string path = $"{directory}/{file}";
            string text = Godot.FileAccess.GetFileAsString(path);
            if (string.IsNullOrEmpty(text)) continue;
            foreach (Match match in pattern.Matches(text)) {
                found.Add((path, match.Groups[1].Value));
            }
        }
    }

    [TestCase]
    public void EveryAuthoredPresentationEventResolvesToAnExplicitFamily() {
        List<(string File, string EventID)> events = CollectEventIDs();

        // A scan that reached nothing would pass vacuously.
        AssertThat(events.Count).OverrideFailureMessage(
            $"Roster scan found only {events.Count} PresentationEventID strings; the walk is broken.")
            .IsGreater(70);

        var issues = new List<string>();
        foreach ((string file, string eventID) in events) {
            foreach (EnemyPresentationPhase phase in
                     new[] { EnemyPresentationPhase.Telegraph, EnemyPresentationPhase.Active }) {
                RosterVfxMap.Mapping mapping = RosterVfxMap.Resolve(eventID, phase);
                if (!mapping.IsExplicit) {
                    issues.Add($"{file}: '{eventID}' ({phase}) fell through to the generic default");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryEnemyDeathEventResolvesAndDrawsFromTheEnvironmentPool() {
        var enemies = new List<(string, string)>();
        Walk("res://resources/Enemies", enemies, EnemyID);
        AssertThat(enemies.Count).IsGreater(20);

        var issues = new List<string>();
        foreach ((string file, string enemyID) in enemies) {
            RosterVfxMap.Mapping mapping =
                RosterVfxMap.Resolve($"{enemyID}.death", EnemyPresentationPhase.Death);
            if (!mapping.IsExplicit) issues.Add($"{file}: '{enemyID}.death' unmapped");
            if (mapping.PoolID != VfxEmitter.EnvironmentPoolID) {
                issues.Add($"{file}: death draws from {mapping.PoolID}");
            }
            if (!mapping.HasEffect) issues.Add($"{file}: death draws nothing");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryFamilyIsActuallyReachedByTheAuthoredRoster() {
        var families = new HashSet<VfxEffectFamily>();
        foreach ((string _, string eventID) in CollectEventIDs()) {
            families.Add(RosterVfxMap.Resolve(eventID, EnemyPresentationPhase.Active).Family);
        }
        // If the vocabulary collapsed every ability onto one shape the sweep above
        // would still pass; this is the check that it carries information.
        AssertThat(families.Count).IsGreaterEqual(4);
    }

    [TestCase]
    public void FamilyPriorityBeatsTokenOrderWhenAnAbilityNamesTwoVerbs() {
        // "pulse_flintlock" is a gun, not a shockwave, even though "pulse" is first.
        AssertThat(RosterVfxMap.Resolve("enemy.laser_pistol_deckhand.pulse_flintlock",
            EnemyPresentationPhase.Active).Family).IsEqual(VfxEffectFamily.Beam);
        // "shell_burst" is a shell.
        AssertThat(RosterVfxMap.Resolve("enemy.overcharged_cannon_master.shell_burst",
            EnemyPresentationPhase.Active).Family).IsEqual(VfxEffectFamily.Beam);
        // "cross_slash_dash" is a slash that happens to move.
        AssertThat(RosterVfxMap.Resolve("boss.borgia_inquisitor.cross_slash_dash",
            EnemyPresentationPhase.Active).Family).IsEqual(VfxEffectFamily.Slash);
        // "lance_lunge" is a lance.
        AssertThat(RosterVfxMap.Resolve("enemy.neural_linked_knight.lance_lunge",
            EnemyPresentationPhase.Active).Family).IsEqual(VfxEffectFamily.Slash);
    }

    [TestCase]
    public void AnEventIDThatAlreadyCarriesItsPhaseSuffixIsNotMisread() {
        // A2 hit the same trap on the audio side: EnemyController already emits
        // "{enemyID}.death", so a phase token must not be mistaken for the verb.
        RosterVfxMap.Mapping plain = RosterVfxMap.Resolve(
            "boss.jackal_priest.khopesh_sweep", EnemyPresentationPhase.Active);
        RosterVfxMap.Mapping suffixed = RosterVfxMap.Resolve(
            "boss.jackal_priest.khopesh_sweep.active", EnemyPresentationPhase.Active);
        AssertThat(suffixed.Family).IsEqual(plain.Family);
        AssertThat(suffixed.MatchedKeyword).IsEqual(plain.MatchedKeyword);
    }

    [TestCase]
    public void RecoveryDeliberatelyDrawsNothing() {
        RosterVfxMap.Mapping mapping = RosterVfxMap.Resolve(
            "enemy.cyber_guard.shock_pike", EnemyPresentationPhase.Recovery);
        AssertThat(mapping.HasEffect).IsFalse();

        // Every other phase of the same ability does draw.
        foreach (EnemyPresentationPhase phase in new[] {
            EnemyPresentationPhase.Telegraph, EnemyPresentationPhase.Active,
            EnemyPresentationPhase.Death }) {
            AssertThat(RosterVfxMap.Resolve("enemy.cyber_guard.shock_pike", phase).HasEffect).IsTrue();
        }
    }

    [TestCase]
    public void BossAndEnemyBeatsAreTintedApartAndPhasesReadAsValue() {
        Color bossActive = VfxAccentPalette.ForRoster(true, EnemyPresentationPhase.Active);
        Color enemyActive = VfxAccentPalette.ForRoster(false, EnemyPresentationPhase.Active);
        AssertThat(bossActive).IsNotEqual(enemyActive);
        AssertThat(RosterVfxMap.IsBossEvent("boss.apex_eraser.erasure_blade")).IsTrue();
        AssertThat(RosterVfxMap.IsBossEvent("enemy.cyber_guard.shock_pike")).IsFalse();

        // Telegraph reads as the same hue at lower opacity, so a wind-up is legible
        // as the attack that follows it.
        Color bossTelegraph = VfxAccentPalette.ForRoster(true, EnemyPresentationPhase.Telegraph);
        AssertFloat(bossTelegraph.R).IsEqualApprox(bossActive.R, 0.0001);
        AssertFloat(bossTelegraph.A).IsLess(bossActive.A);
    }

    [TestCase]
    public void AnUnmappedVerbIsReportedRatherThanSilentlyDefaulted() {
        // The negative case for the sweep above: this is what a future roster
        // addition with new vocabulary looks like.
        RosterVfxMap.Mapping mapping = RosterVfxMap.Resolve(
            "enemy.future_thing.zorbulate", EnemyPresentationPhase.Active);
        AssertThat(mapping.IsExplicit).IsFalse();
        AssertThat(mapping.HasEffect).IsTrue();
        AssertThat(mapping.Family).IsEqual(VfxEffectFamily.Burst);
    }
}
