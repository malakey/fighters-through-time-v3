using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 free respec (design "Respec — free, always available"): the grid
/// screen's respec refunds EVERY Chronal Dust point spent on the active
/// character's grid back into their Repository pool and clears all unlocked
/// nodes — no fee, no cooldown. Pure save-state rules, exercised against the
/// authored einstein grid resource.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceRespecTests {

    [TestCase]
    public void RespecRefundsEverySpentPointAndClearsTheGrid() {
        ResonanceGridData grid = ResonanceProgression.LoadGrid("einstein");
        AssertObject(grid).IsNotNull();
        var save = new StorySaveData { SelectedCharacterID = "einstein" };
        save.DepositedChronalDust["einstein"] = 500;

        // Buy two real tier-1 nodes through the production unlock path.
        int purchased = 0;
        int spent = 0;
        foreach (ResonanceNodeData node in grid.Nodes) {
            if (purchased >= 2) break;
            if (node == null || (node.PrerequisiteNodeIDs?.Length ?? 0) > 0) continue;
            AssertThat(ResonanceProgression.TryUnlock(grid, save, node.NodeID))
                .IsEqual(ResonanceUnlockResult.Unlocked);
            purchased++;
            spent += node.UnlockCost;
        }
        AssertThat(purchased).IsEqual(2);
        AssertThat(spent > 0).IsTrue();
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(500 - spent);
        AssertThat(ResonanceProgression.CalculateSpentDust(grid, save)).IsEqual(spent);

        int refunded = ResonanceProgression.RespecAll(grid, save);
        AssertThat(refunded)
            .OverrideFailureMessage("The respec must refund exactly what was spent.")
            .IsEqual(spent);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(500);
        AssertThat(ResonanceProgression.GetUnlockedNodes(save, "einstein").Count).IsEqual(0);
    }

    [TestCase]
    public void RespecOnTheWrongCharactersGridIsANoOp() {
        ResonanceGridData grid = ResonanceProgression.LoadGrid("einstein");
        AssertObject(grid).IsNotNull();
        var save = new StorySaveData { SelectedCharacterID = "joan" };
        save.DepositedChronalDust["einstein"] = 100;
        // Residue that should be untouchable through the wrong grid.
        save.GridProgress["einstein"] = new List<string> { grid.Nodes[0].NodeID };

        AssertThat(ResonanceProgression.CalculateSpentDust(grid, save)).IsEqual(0);
        AssertThat(ResonanceProgression.RespecAll(grid, save)).IsEqual(0);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(100);
        AssertThat(save.GridProgress["einstein"].Count).IsEqual(1);
    }

    [TestCase]
    public void ASecondRespecRefundsZero() {
        ResonanceGridData grid = ResonanceProgression.LoadGrid("einstein");
        AssertObject(grid).IsNotNull();
        var save = new StorySaveData { SelectedCharacterID = "einstein" };
        save.DepositedChronalDust["einstein"] = 500;
        foreach (ResonanceNodeData node in grid.Nodes) {
            if (node != null && (node.PrerequisiteNodeIDs?.Length ?? 0) == 0) {
                ResonanceProgression.TryUnlock(grid, save, node.NodeID);
                break;
            }
        }
        AssertThat(ResonanceProgression.RespecAll(grid, save) > 0).IsTrue();
        int balance = save.DepositedChronalDust["einstein"];

        AssertThat(ResonanceProgression.RespecAll(grid, save))
            .OverrideFailureMessage("A respec of an already-empty grid must refund nothing.")
            .IsEqual(0);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(balance);
    }

    [TestCase]
    public void RespecOfAnUntouchedGridRefundsZero() {
        ResonanceGridData grid = ResonanceProgression.LoadGrid("einstein");
        AssertObject(grid).IsNotNull();
        var save = new StorySaveData { SelectedCharacterID = "einstein" };
        save.DepositedChronalDust["einstein"] = 75;

        AssertThat(ResonanceProgression.CalculateSpentDust(grid, save)).IsEqual(0);
        AssertThat(ResonanceProgression.RespecAll(grid, save)).IsEqual(0);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(75);
    }

    /// <summary>
    /// Package 11 A4 (F08). The one-time, versioned, FREE respec a pre-V7.6
    /// save gets on load. V7.6 re-authored every node ID in every grid, so the
    /// migration generalizes F08's Shakespeare case to all nine characters:
    /// drop every purchase whose node no longer exists and refund exactly its
    /// recorded paid cost, once. Campaign progress and undeposited earnings are
    /// untouched, and a missing node is never granted for free.
    /// </summary>
    [TestCase]
    public void TheF08MigrationRefundsEveryRetiredPurchaseOnceAndGrantsNothingFree() {
        var save = new StorySaveData {
            SelectedCharacterID = "shakespeare",
            CurrentLevelID = "res://scenes/campaign/Level_11_Gettysburg.tscn",
            LevelChronalDust = 40
        };
        save.DepositedChronalDust["shakespeare"] = 25;
        // A pre-F08 Shakespeare build: a Major plus two retired minors,
        // 200 + 50 + 75 = 325.
        save.GridProgress["shakespeare"] = new List<string> {
            "shakespeare_t3", "shakespeare_t1", "shakespeare_c2"
        };

        int refunded = ResonanceProgression.MigrateGridProgressToV76(save);

        AssertThat(refunded).IsEqual(325);
        AssertThat(save.DepositedChronalDust["shakespeare"]).IsEqual(350);
        AssertThat(save.GridProgress["shakespeare"].Count).IsEqual(0);
        // Campaign progress and undeposited earnings survive untouched.
        AssertThat(save.CurrentLevelID).IsEqual("res://scenes/campaign/Level_11_Gettysburg.tscn");
        AssertThat(save.LevelChronalDust).IsEqual(40);

        // Nothing is granted for free: the refunded dust must be spent again
        // through the ordinary evaluator, and the new Act III Major still
        // needs all three Act II prerequisites.
        ResonanceGridData grid = ResonanceProgression.LoadGrid("shakespeare");
        AssertThat(ResonanceProgression.EvaluateUnlock(grid, save, "shakespeare_macbeths_curse"))
            .IsEqual(ResonanceUnlockResult.MissingPrerequisite);

        // Idempotent: running it again refunds nothing.
        AssertThat(ResonanceProgression.MigrateGridProgressToV76(save)).IsEqual(0);
        AssertThat(save.DepositedChronalDust["shakespeare"]).IsEqual(350);
    }
}
