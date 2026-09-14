using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A7b — V7.5 "Borrowed Legacies", the First Unbound's Phase 3 identity
/// (plan §2.5). In its final phase the villain fights with the signature move of
/// every roster legend the player did not pick, projected from the authored
/// character data at encounter start.
///
/// <para>The rule these cases exist to protect is that the set is <b>never a
/// literal nine-element list</b>: it is the content manifest's character roster
/// minus the active hero, so a tenth character widens it with no code change.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BorrowedLegaciesTests {

    [TestCase]
    public void TheBorrowedSetIsTheManifestRosterMinusTheActiveHeroAndScalesWithIt() {
        IReadOnlyList<string> roster = CampaignCaptiveRoster.Roster();
        AssertThat(roster.Count)
            .OverrideFailureMessage("The manifest must carry the whole character roster.")
            .IsEqual(9);

        // For each of the nine possible active heroes: exactly the other eight,
        // and never the hero's own move.
        foreach (string hero in roster) {
            EnemyAbilityData[] borrowed = BorrowedLegacies.ProjectFor(hero);
            AssertThat(borrowed.Length)
                .OverrideFailureMessage($"Hero '{hero}' must borrow rosterCount - 1 legacies.")
                .IsEqual(roster.Count - 1);
            foreach (EnemyAbilityData ability in borrowed) {
                AssertThat(ability.AbilityID.Contains($".{hero}."))
                    .OverrideFailureMessage($"'{hero}' must never borrow its own legacy.")
                    .IsFalse();
            }
        }

        // Scaling proof against a hypothetical tenth character: the rule is the
        // roster's size, not the number nine.
        var widened = new List<string>(roster) { "einstein" }; // duplicate is ignored
        AssertThat(BorrowedLegacies.ProjectFor("einstein", widened).Length).IsEqual(8);
        var tenHeroes = new List<string>(roster) { "leonardo", "cleopatra" };
        AssertThat(BorrowedLegacies.ProjectFor("joan", tenHeroes).Length)
            .OverrideFailureMessage("Duplicates collapse; the distinct roster minus the hero survives.")
            .IsEqual(8);
    }

    [TestCase]
    public void TheArchetypeIsChosenByTheAbilitysShapeAndNothingElse() {
        AssertThat(BorrowedLegacies.ArchetypeFor(AbilityExecutionType.Projectile))
            .IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(BorrowedLegacies.ArchetypeFor(AbilityExecutionType.Melee))
            .IsEqual(EnemyAbilityArchetype.MeleeStrike);
        AssertThat(BorrowedLegacies.ArchetypeFor(AbilityExecutionType.Area))
            .IsEqual(EnemyAbilityArchetype.AreaPulse);
        // A placed construct is zone-shaped for projection purposes.
        AssertThat(BorrowedLegacies.ArchetypeFor(AbilityExecutionType.PersistentObject))
            .IsEqual(EnemyAbilityArchetype.AreaPulse);
        // Shapes a Special 1 never uses fall to the resource's own default.
        AssertThat(BorrowedLegacies.ArchetypeFor(AbilityExecutionType.Movement))
            .IsEqual(EnemyAbilityArchetype.MeleeStrike);

        // And the live roster proves the mapping against real authored data:
        // Einstein's Mass-Energy Conversion is a projectile, Pocahontas's Spirit
        // Strike is melee, Tesla's coil is a placed construct.
        AssertThat(ProjectionFor("einstein").Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(ProjectionFor("pocahontas").Archetype).IsEqual(EnemyAbilityArchetype.MeleeStrike);
        AssertThat(ProjectionFor("tesla").Archetype).IsEqual(EnemyAbilityArchetype.AreaPulse);
    }

    [TestCase]
    public void DamageComesFromBaseDamageAndTheTelegraphFromTheAuthoredStartupFrames() {
        foreach (string characterID in CampaignCaptiveRoster.Roster()) {
            CharacterData character = FTT.Core.AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            AbilityData source = character.SpecialAttackOne;
            EnemyAbilityData projected = ProjectionFor(characterID);

            AssertThat(projected.Damage)
                .OverrideFailureMessage($"'{characterID}' damage must come from BaseDamage.")
                .IsEqualApprox(source.BaseDamage, 0.0001f);
            AssertThat(projected.TelegraphFrames)
                .OverrideFailureMessage($"'{characterID}' telegraph must be the authored startup.")
                .IsEqual(source.StartupFrames);
            // The cradle hook: the borrowed character's own canonical ability ID, so
            // the move tears out wearing its owner's effect.
            AssertThat(projected.PresentationEventID).IsEqual(source.AbilityID);
            AssertThat(projected.DisplayNameKey).IsEqual(source.DisplayNameKey);
            // Everything else is the archetype default; nothing bespoke is invented.
            AssertThat(projected.SelectionWeight).IsEqualApprox(1f, 0.0001f);
            AssertThat(projected.RangeClass).IsEqual(EnemyAbilityRangeClass.Any);
            AssertThat(projected.HasValidIdentity()).IsTrue();
            AssertThat(projected.HasValidArchetypeFields()).IsTrue();
        }
    }

    [TestCase]
    public void TheCompositeKitAppendsTheBorrowedMovesGatedToTheFinalPhaseAtUniformWeight() {
        string original = GameManager.Instance?.CurrentSession.SelectedCharacterID;
        if (GameManager.Instance != null) {
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
        }
        BossController boss = CreateBorrowingBoss();
        try {
            int authored = boss.Data.BossAbilities.Length;
            AssertThat(boss.BorrowedAbilityStartIndex).IsEqual(authored);
            AssertThat(boss.BorrowedAbilityCount).IsEqual(8);
            AssertThat(boss.ActiveAbilities.Count).IsEqual(authored + 8);

            // Weighted-uniform across the projected set: every borrowed entry
            // carries the same default weight, so the existing weighted roll is a
            // uniform roll over them.
            for (int index = authored; index < boss.ActiveAbilities.Count; index++) {
                AssertThat(boss.ActiveAbilities[index].SelectionWeight).IsEqualApprox(1f, 0.0001f);
                // Gated to the boss's FINAL phase through the existing
                // AbilityMinPhase mechanism — [0.66, 0.33] => phase index 2 => P3.
                AssertThat(boss.GetActiveAbilityMinPhase(index)).IsEqual(2);
            }

            // Locked out before P3...
            boss.CurrentPhase = 1;
            for (int roll = 0; roll < 200; roll++) {
                AssertThat(boss.SelectAbilityIndex(60f) < authored)
                    .OverrideFailureMessage("A borrowed legacy must not be selectable before P3.")
                    .IsTrue();
            }
            // ...and reachable in it.
            boss.CurrentPhase = 2;
            bool sawBorrowed = false;
            for (int roll = 0; roll < 400; roll++) {
                if (boss.SelectAbilityIndex(60f) >= authored) sawBorrowed = true;
            }
            AssertThat(sawBorrowed).IsTrue();
        } finally {
            boss.Free();
            if (GameManager.Instance != null) {
                GameManager.Instance.CurrentSession.SelectedCharacterID = original;
            }
        }
    }

    [TestCase]
    public void ProjectionsAreRuntimeOnlyAndNeverReachDiskOrTheContentManifest() {
        EnemyAbilityData[] borrowed = BorrowedLegacies.ProjectFor("einstein");
        AssertThat(borrowed.Length).IsEqual(8);

        var manifestAbilityIDs = new HashSet<string>();
        ContentManifest manifest = ContentManifest.LoadDefault();
        foreach (ContentManifestEntry entry in manifest.Entries) {
            manifestAbilityIDs.Add(entry.ContentID);
        }

        var issues = new List<string>();
        foreach (EnemyAbilityData ability in borrowed) {
            // A runtime instance has no resource path: it was never saved and can
            // never be picked up by AuthoredResources or a .tres reference.
            if (!string.IsNullOrEmpty(ability.ResourcePath)) {
                issues.Add($"{ability.AbilityID} carries a resource path '{ability.ResourcePath}'");
            }
            if (manifestAbilityIDs.Contains(ability.AbilityID)) {
                issues.Add($"{ability.AbilityID} is registered in the content manifest");
            }
            if (!ability.AbilityID.StartsWith($"{BorrowedLegacies.AbilityIDPrefix}.")) {
                issues.Add($"{ability.AbilityID} is not marked as a borrowed projection");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // Nothing borrowed was written next to the authored boss kits either.
        AssertThat(DirAccess.DirExistsAbsolute("res://resources/Bosses/Abilities/borrowed")).IsFalse();
    }

    // === Helpers ===

    private static EnemyAbilityData ProjectionFor(string characterID) {
        EnemyAbilityData ability = BorrowedLegacies.ProjectCharacter(characterID);
        AssertObject(ability).IsNotNull();
        return ability;
    }

    /// <summary>
    /// A fixture boss shaped like the First Unbound: two authored abilities and the
    /// [0.66, 0.33] thresholds, with <c>BorrowsRosterLegacies</c> on.
    /// </summary>
    private static BossController CreateBorrowingBoss() {
        var data = new BossData {
            BossID = "borrowing_boss_under_test",
            DisplayNameKey = "boss_apex_eraser_name",
            MaxHP = 1000,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.WeightedRandom,
            PhaseThresholds = new[] { 0.66f, 0.33f },
            BorrowsRosterLegacies = true,
            BossAbilities = new[] {
                new EnemyAbilityData {
                    AbilityID = "boss.borrowing_boss_under_test.swing",
                    Archetype = EnemyAbilityArchetype.MeleeStrike,
                    RangeClass = EnemyAbilityRangeClass.Any,
                    SelectionWeight = 1f,
                    TelegraphFrames = 20, ActiveFrames = 6, RecoveryFrames = 10, Damage = 10f
                },
                new EnemyAbilityData {
                    AbilityID = "boss.borrowing_boss_under_test.shot",
                    Archetype = EnemyAbilityArchetype.Projectile,
                    RangeClass = EnemyAbilityRangeClass.Any,
                    SelectionWeight = 1f,
                    TelegraphFrames = 20, ActiveFrames = 6, RecoveryFrames = 10, Damage = 8f
                }
            },
            AbilityMinPhase = new[] { 0, 0 }
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 913913;
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }
}
