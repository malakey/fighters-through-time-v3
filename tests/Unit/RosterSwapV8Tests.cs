using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W5 — the declared v7 → v8 D4 derivation, the roster swap
/// Pocahontas → Tubman (<see cref="RosterSwapV8.MigrateRosterSwapV8"/> and
/// <see cref="RosterSwapV8.EnsureReplacementUnlocked"/>). Phase C wires both into
/// the single v8 step; this suite pins the derivation itself.
///
/// <para><b>Pure C#, no Godot runtime</b> — deliberately. Save payloads and their
/// derivations must stay engine-free (CLAUDE.md failure signature 7), so nothing
/// here touches <c>CharacterRoster</c>, a grid resource or the manifest.</para>
/// </summary>
[TestSuite]
public class RosterSwapV8Tests {

    private static readonly string[] FullV76Grid = {
        "pocahontas_windstep", "pocahontas_staff_edge", "pocahontas_forest_vigor",
        "pocahontas_glide_duration", "pocahontas_snare_duration", "pocahontas_second_glide",
        "pocahontas_tornado_lift", "pocahontas_thorn_snare", "pocahontas_leaf_barrier"
    };

    [TestCase]
    public void APocahontasSaveIsLockedToTubmanAndAFullGridRefundsNineHundredSeventyFive() {
        StorySaveData save = PocahontasSave();
        save.GridProgress["pocahontas"] = new List<string>(FullV76Grid);
        save.DepositedChronalDust["pocahontas"] = 30;
        save.DepositedChronalDust["tubman"] = 5;

        int refund = RosterSwapV8.MigrateRosterSwapV8(save);

        AssertThat(refund).IsEqual(975);
        AssertString(save.SelectedCharacterID).IsEqual("tubman");
        AssertThat(save.DepositedChronalDust["tubman"]).IsEqual(5 + 30 + 975);
        AssertThat(save.DepositedChronalDust.ContainsKey("pocahontas")).IsFalse();
        AssertThat(save.GridProgress.ContainsKey("pocahontas")).IsFalse();
        AssertThat(save.GridProgress.ContainsKey("tubman"))
            .OverrideFailureMessage("Tubman starts with an empty grid; no node is carried or granted.")
            .IsFalse();
        // The open attempt's undeposited wallet follows the selection, untouched.
        AssertThat(save.LevelChronalDust).IsEqual(14);
    }

    [TestCase]
    public void TheSelectionMatchIsTrimmedAndCaseInsensitive() {
        StorySaveData save = PocahontasSave();
        save.SelectedCharacterID = "  Pocahontas ";
        RosterSwapV8.MigrateRosterSwapV8(save);
        AssertString(save.SelectedCharacterID).IsEqual("tubman");
    }

    [TestCase]
    public void APartialGridRefundsOnlyWhatWasBought() {
        StorySaveData save = PocahontasSave();
        save.GridProgress["pocahontas"] = new List<string> {
            "pocahontas_windstep", "pocahontas_second_glide", "pocahontas_leaf_barrier"
        };

        AssertThat(RosterSwapV8.MigrateRosterSwapV8(save)).IsEqual(50 + 75 + 200);
        AssertThat(save.DepositedChronalDust["tubman"]).IsEqual(325);
    }

    [TestCase]
    public void PreV76BranchIDsRefundAtTheirRecordedPriceAndUnknownIDsRefundNothing() {
        StorySaveData save = PocahontasSave();
        save.GridProgress["pocahontas"] = new List<string> {
            "pocahontas_fs1", "pocahontas_wr2", "pocahontas_pw3", "pocahontas_not_a_node"
        };

        AssertThat(RosterSwapV8.MigrateRosterSwapV8(save)).IsEqual(50 + 75 + 200);
        AssertThat(save.DepositedChronalDust["tubman"]).IsEqual(325);
        AssertThat(RosterSwapV8.RefundFor("pocahontas_fs3")).IsEqual(200);
        AssertThat(RosterSwapV8.RefundFor("pocahontas_not_a_node")).IsEqual(0);
        AssertThat(RosterSwapV8.RefundFor(null)).IsEqual(0);
    }

    [TestCase]
    public void LegacyUnlockSlotsCarryOverAsAUnion() {
        StorySaveData save = PocahontasSave();
        save.UnlockedLegacyAbilities["pocahontas"] = new List<string> { "movement", "special1", "special2" };
        save.UnlockedLegacyAbilities["tubman"] = new List<string> { "movement" };

        RosterSwapV8.MigrateRosterSwapV8(save);

        AssertThat(save.UnlockedLegacyAbilities.ContainsKey("pocahontas")).IsFalse();
        List<string> slots = save.UnlockedLegacyAbilities["tubman"];
        AssertThat(slots.Count).IsEqual(3);
        AssertThat(slots).Contains("movement", "special1", "special2");
    }

    [TestCase]
    public void AnotherHerosSaveKeepsEverythingButAStrayPocahontasEntryStillMigrates() {
        var save = new StorySaveData { SelectedCharacterID = "einstein", LevelChronalDust = 6 };
        save.DepositedChronalDust["einstein"] = 40;
        save.GridProgress["einstein"] = new List<string> { "einstein_node_1" };
        save.UnlockedLegacyAbilities["einstein"] = new List<string> { "movement" };
        save.ViewedDialogueIDs.Add("dlg_l00_open@pocahontas");
        // Stray Pocahontas state no code path can read after the swap.
        save.GridProgress["pocahontas"] = new List<string> { "pocahontas_windstep" };
        save.DepositedChronalDust["pocahontas"] = 12;

        AssertThat(RosterSwapV8.MigrateRosterSwapV8(save)).IsEqual(50);

        AssertString(save.SelectedCharacterID).IsEqual("einstein");
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(40);
        AssertThat(save.GridProgress["einstein"].Count).IsEqual(1);
        AssertThat(save.UnlockedLegacyAbilities["einstein"].Count).IsEqual(1);
        AssertThat(save.LevelChronalDust).IsEqual(6);
        AssertThat(save.DepositedChronalDust["tubman"]).IsEqual(12 + 50);
        AssertThat(save.GridProgress.ContainsKey("pocahontas")).IsFalse();
        // D4: seen dialogue stays as dead data.
        AssertThat(save.ViewedDialogueIDs).Contains("dlg_l00_open@pocahontas");

        // A save with no Pocahontas state at all is untouched.
        var clean = new StorySaveData { SelectedCharacterID = "joan" };
        clean.DepositedChronalDust["joan"] = 9;
        AssertThat(RosterSwapV8.MigrateRosterSwapV8(clean)).IsEqual(0);
        AssertThat(clean.DepositedChronalDust.Count).IsEqual(1);
        AssertThat(clean.DepositedChronalDust.ContainsKey("tubman")).IsFalse();
    }

    [TestCase]
    public void TheDerivationIsIdempotent() {
        StorySaveData save = PocahontasSave();
        save.GridProgress["pocahontas"] = new List<string>(FullV76Grid);
        save.DepositedChronalDust["pocahontas"] = 30;
        save.UnlockedLegacyAbilities["pocahontas"] = new List<string> { "movement", "special1" };
        AssertThat(RosterSwapV8.MigrateRosterSwapV8(save)).IsEqual(975);

        AssertThat(RosterSwapV8.MigrateRosterSwapV8(save)).IsEqual(0);
        AssertString(save.SelectedCharacterID).IsEqual("tubman");
        AssertThat(save.DepositedChronalDust["tubman"]).IsEqual(1005);
        AssertThat(save.DepositedChronalDust.Count).IsEqual(1);
        AssertThat(save.UnlockedLegacyAbilities["tubman"].Count).IsEqual(2);
        AssertThat(save.GridProgress.Count).IsEqual(0);
    }

    [TestCase]
    public void NullSaveAndNullCollectionsAreSafe() {
        AssertThat(RosterSwapV8.MigrateRosterSwapV8(null)).IsEqual(0);

        StorySaveData save = PocahontasSave();
        save.GridProgress = null;
        save.DepositedChronalDust = null;
        save.UnlockedLegacyAbilities = null;
        AssertThat(RosterSwapV8.MigrateRosterSwapV8(save)).IsEqual(0);
        AssertString(save.SelectedCharacterID).IsEqual("tubman");

        // A null grid list under the pocahontas key refunds nothing but is removed.
        StorySaveData nullList = PocahontasSave();
        nullList.GridProgress["pocahontas"] = null;
        nullList.UnlockedLegacyAbilities["pocahontas"] = null;
        AssertThat(RosterSwapV8.MigrateRosterSwapV8(nullList)).IsEqual(0);
        AssertThat(nullList.GridProgress.ContainsKey("pocahontas")).IsFalse();
        AssertThat(nullList.UnlockedLegacyAbilities["tubman"].Count).IsEqual(0);
    }

    [TestCase]
    public void TheD2RetirementAndThisDerivationCommute() {
        // A Pocahontas save parked in the retired Level 4A with a held wallet.
        StorySaveData d2First = ParkedLegacyPocahontasSave();
        StoryAttemptState.RetireLegacyLevelV8(d2First);
        RosterSwapV8.MigrateRosterSwapV8(d2First);

        StorySaveData d4First = ParkedLegacyPocahontasSave();
        RosterSwapV8.MigrateRosterSwapV8(d4First);
        StoryAttemptState.RetireLegacyLevelV8(d4First);

        AssertThat(d2First.DepositedChronalDust["tubman"]).IsEqual(20 + 11 + 50);
        AssertThat(d4First.DepositedChronalDust["tubman"]).IsEqual(20 + 11 + 50);
        AssertThat(d2First.DepositedChronalDust.ContainsKey("pocahontas")).IsFalse();
        AssertThat(d4First.DepositedChronalDust.ContainsKey("pocahontas")).IsFalse();
        AssertString(d2First.SelectedCharacterID).IsEqual("tubman");
        AssertString(d4First.SelectedCharacterID).IsEqual("tubman");
    }

    [TestCase]
    public void TheGlobalHelperAddsTubmanOnceAndLeavesDeadDataInPlace() {
        var global = new GlobalSaveData();
        global.UnlockedCharacters.AddRange(new[] { "einstein", "joan", "pocahontas" });
        global.CharacterWins["pocahontas"] = 4;
        global.CharacterLosses["pocahontas"] = 2;

        AssertThat(RosterSwapV8.EnsureReplacementUnlocked(global)).IsTrue();
        AssertThat(RosterSwapV8.EnsureReplacementUnlocked(global)).IsFalse();

        int tubmanCount = 0;
        foreach (string id in global.UnlockedCharacters) if (id == "tubman") tubmanCount++;
        AssertThat(tubmanCount).IsEqual(1);
        AssertThat(global.UnlockedCharacters).Contains("pocahontas");
        AssertThat(global.CharacterWins["pocahontas"]).IsEqual(4);
        AssertThat(global.CharacterLosses["pocahontas"]).IsEqual(2);

        // A fresh (empty) list is left to SaveManager.EnsureRosterUnlocked's fill.
        var fresh = new GlobalSaveData();
        AssertThat(RosterSwapV8.EnsureReplacementUnlocked(fresh)).IsFalse();
        AssertThat(fresh.UnlockedCharacters.Count).IsEqual(0);
        AssertThat(RosterSwapV8.EnsureReplacementUnlocked(null)).IsFalse();
    }

    private static StorySaveData PocahontasSave() => new StorySaveData {
        SelectedCharacterID = "pocahontas",
        CurrentLevelID = "res://scenes/campaign/Level_07_Nassau.tscn",
        LevelChronalDust = 14
    };

    private static StorySaveData ParkedLegacyPocahontasSave() {
        var save = new StorySaveData {
            SelectedCharacterID = "pocahontas",
            CurrentLevelID = "res://scenes/campaign/Level_04A_pocahontas.tscn",
            LastCheckpointID = "level_04a_pocahontas_checkpoint_1",
            LevelChronalDust = 11
        };
        save.DepositedChronalDust["pocahontas"] = 20;
        save.GridProgress["pocahontas"] = new List<string> { "pocahontas_windstep" };
        save.AttemptState = StoryAttemptState.CreateFresh("level_04a_pocahontas", 0);
        return save;
    }
}
