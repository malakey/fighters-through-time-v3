using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A5 — the V7.5 Legacy Unlock Schedule, shifted one level earlier
/// by the 2026-11-03 gameplay review (GR-SCHED, ruling 3b+). The hero's kit is
/// earned across the opening levels: Special 1 on completing Level 0, the
/// Movement Ability after Level 1, Special 2 after Level 2, and the Ultimate
/// <i>cast</i> after Level 3. The meter itself, Block, Rally, the death rewind
/// and the basics are never locked — Defy and Level 4's dampening beams depend
/// on the meter existing from Level 0.
///
/// <para>The pillar these cases guard is <b>Story/Fighter isolation</b>: the
/// gate is installed only on <c>CharacterFactory.CreateCharacter(...,
/// applyStoryProgression: true)</c>, so Fighter Mode, the hub Holodeck, the
/// Calibration Drills and the Mirror Paradox clone always run the full
/// normalized kit.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LegacyUnlockScheduleTests {

    [TestCase]
    public void TheMilestoneTableMatchesTheShiftedSchedule() {
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.Special1)).IsEqual(0);
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.MovementAbility)).IsEqual(1);
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.Special2)).IsEqual(2);
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.Ultimate)).IsEqual(3);
        AssertThat(LegacyUnlockSchedule.GatedSlots.Count).IsEqual(4);
        for (int i = 1; i < LegacyUnlockSchedule.GatedSlots.Count; i++) {
            AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(LegacyUnlockSchedule.GatedSlots[i]))
                .OverrideFailureMessage("GatedSlots must list the slots in unlock order.")
                .IsGreater(LegacyUnlockSchedule.MilestoneLevelFor(LegacyUnlockSchedule.GatedSlots[i - 1]));
        }

        // Nothing is unlocked on a fresh campaign beyond what the schedule never gates.
        var none = new List<string>();
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            AssertThat(LegacyUnlockSchedule.IsUnlocked(slot, none))
                .OverrideFailureMessage($"{slot} must be dormant on a fresh campaign.")
                .IsFalse();
        }

        var afterLevelThree = new List<string> {
            "level_00_tutorial", "level_01_florence", "level_02_orleans", "level_03_chicago"
        };
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            AssertThat(LegacyUnlockSchedule.IsUnlocked(slot, afterLevelThree))
                .OverrideFailureMessage($"{slot} must be restored once Level 3 is complete.")
                .IsTrue();
        }

        // Partial progress restores exactly the earned slots, in order.
        var afterTwo = new List<string> { "level_00_tutorial", "level_01_florence", "level_02_orleans" };
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Special1, afterTwo)).IsTrue();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.MovementAbility, afterTwo)).IsTrue();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Special2, afterTwo)).IsTrue();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Ultimate, afterTwo)).IsFalse();
        var afterZero = new List<string> { "level_00_tutorial" };
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Special1, afterZero)).IsTrue();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.MovementAbility, afterZero)).IsFalse();
    }

    [TestCase]
    public void TheUngatedSentinelIsNegativeSoLevelZeroCanBeAMilestone() {
        // GR-SCHED: 0 is Special 1's milestone, so "not gated" cannot be 0 (nor "<= 0").
        AssertThat(LegacyUnlockSchedule.NotGated).IsEqual(-1);
        const AbilitySlot notASlot = (AbilitySlot)99;
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(notASlot)).IsEqual(LegacyUnlockSchedule.NotGated);
        AssertThat(LegacyUnlockSchedule.IsUnlocked(notASlot, new List<string>())).IsTrue();
        AssertThat(LegacyUnlockSchedule.IsUnlockedFor(new StorySaveData(), "joan", notASlot)).IsTrue();
        AssertThat(LegacyUnlockSchedule.SlotKey(notASlot)).IsEqual("");

        // The controller's gate refuses a gated slot at milestone 0 but never an ungated one.
        PlayerController gated = BuildBareController();
        try {
            gated.ApplyLegacyUnlockLocks(new List<AbilitySlot>());
            AssertThat(gated.IsAbilityUnlocked(AbilitySlot.Special1))
                .OverrideFailureMessage("Special 1 (milestone 0) must be refused until Level 0 is complete.").IsFalse();
            AssertThat(gated.IsAbilityUnlocked(notASlot)).IsTrue();
        } finally {
            Free(gated);
        }

        // Level 0 grants Special 1; an unrecognized or retired ID (LevelNumberOf -1) grants nothing.
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_00_tutorial", out AbilitySlot granted)).IsTrue();
        AssertThat(granted).IsEqual(AbilitySlot.Special1);
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("hub", out _)).IsFalse();
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_04a_joan", out _)).IsFalse();
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("", out _)).IsFalse();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Special1, new List<string> { "level_04a_joan", "hub" }))
            .OverrideFailureMessage("A retired or unrecognized ID must never satisfy the Level 0 milestone.").IsFalse();
    }

    [TestCase]
    public void SavedKeysSurviveTheShiftSoNoSaveEverLosesASlot() {
        // GR-SCHED (ruling 3b+, plan correction 5): no derivation. A save written under the old table carries its keys,
        // and UnlockedSlotsFor unions them with the history, so the new table only ever adds slots.
        // Old table: Movement after Level 1, Special 1 after Level 2, Special 2 after Level 3.
        var oldSave = new StorySaveData {
            SelectedCharacterID = "joan",
            CompletedLevels = new List<string> { "level_01_florence", "level_02_orleans", "level_03_chicago" },
            UnlockedLegacyAbilities = new Dictionary<string, List<string>> {
                ["joan"] = new List<string> { "movement", "special1", "special2" }
            }
        };
        HashSet<AbilitySlot> read = LegacyUnlockSchedule.UnlockedSlotsFor(oldSave, "joan");
        AssertThat(read.Contains(AbilitySlot.Special1)).IsTrue();
        AssertThat(read.Contains(AbilitySlot.MovementAbility)).IsTrue();
        AssertThat(read.Contains(AbilitySlot.Special2)).IsTrue();
        // The history alone now also restores the Ultimate (Level 3), which the old table did not.
        AssertThat(read.Contains(AbilitySlot.Ultimate)).IsTrue();

        // Keys alone are enough, with no history at all.
        var keysOnly = new StorySaveData {
            UnlockedLegacyAbilities = new Dictionary<string, List<string>> {
                ["joan"] = new List<string> { "movement", "special1" }
            }
        };
        HashSet<AbilitySlot> keyed = LegacyUnlockSchedule.UnlockedSlotsFor(keysOnly, "joan");
        AssertThat(keyed.Count).IsEqual(2);
        AssertThat(keyed.Contains(AbilitySlot.Special1)).IsTrue();
        AssertThat(keyed.Contains(AbilitySlot.MovementAbility)).IsTrue();
        // Another hero's keys are never read.
        AssertThat(LegacyUnlockSchedule.UnlockedSlotsFor(keysOnly, "tesla").Count).IsEqual(0);
        AssertThat(LegacyUnlockSchedule.IsUnlockedFor(keysOnly, "joan", AbilitySlot.Special1)).IsTrue();

        // The persisted key strings are a serialization contract and did not move with the milestones.
        AssertThat(LegacyUnlockSchedule.SlotKey(AbilitySlot.Special1)).IsEqual("special1");
        AssertThat(LegacyUnlockSchedule.SlotKey(AbilitySlot.MovementAbility)).IsEqual("movement");
    }

    [TestCase]
    public void AHistoryWithNoLevelZeroReadsSpecialOneOnlyFromItsSavedKey() {
        // Plan correction 5: the old table took Special 1 from Level 2, the new one takes it from Level 0, so a
        // hand-built history with no level_00_tutorial restores no Special 1 on its own. A real save carries the
        // saved key (the milestone grant wrote it), which is why no derivation is needed. This pins both halves so
        // a fixture that hand-builds CompletedLevels is known to move.
        var history = new List<string> { "level_01_florence", "level_02_orleans" };
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Special1, history)).IsFalse();
        AssertThat(LegacyUnlockSchedule.UnlockedKeysFor(history).Contains("special1")).IsFalse();

        var realSave = new StorySaveData {
            CompletedLevels = new List<string>(history),
            UnlockedLegacyAbilities = new Dictionary<string, List<string>> {
                ["joan"] = new List<string> { "movement", "special1" }
            }
        };
        AssertThat(LegacyUnlockSchedule.UnlockedSlotsFor(realSave, "joan").Contains(AbilitySlot.Special1))
            .OverrideFailureMessage("A real save's saved special1 key must keep the slot restored.").IsTrue();
    }

    [TestCase]
    public void LevelZerosCompletionRestoresSpecialOneOnItsResultsBeat() {
        WithFreshCampaignSlot(save => {
            StoryManager story = StoryManager.Instance;
            story.GrantLegacyUnlockMilestoneForTests("level_00_tutorial");
            AssertThat(story.ShouldPlayResonanceRestoredOnResults)
                .OverrideFailureMessage("Level 0's results screen plays the Resonance Restored beat for Special 1.")
                .IsTrue();
            AssertThat(story.LastLegacyUnlockSlot).IsEqual(AbilitySlot.Special1);
            AssertThat(save.UnlockedLegacyAbilities["einstein"].Contains("special1")).IsTrue();
            AssertThat(save.UnlockedLegacyAbilities["einstein"].Contains("movement")).IsFalse();
            AssertThat(CharacterFactory.ResolveStoryUnlockedSlots("einstein").Contains(AbilitySlot.Special1)).IsTrue();

            // A re-completion never replays it.
            story.GrantLegacyUnlockMilestoneForTests("level_00_tutorial");
            AssertThat(story.ShouldPlayResonanceRestoredOnResults).IsFalse();
        });
    }

    [TestCase]
    public void LevelIdsResolveTheirMilestoneAndFourAGrantsNothing() {
        AssertThat(LegacyUnlockSchedule.LevelNumberOf("level_00_tutorial")).IsEqual(0);
        AssertThat(LegacyUnlockSchedule.LevelNumberOf("level_11_gettysburg")).IsEqual(11);
        // The retired Level 4A (S27) was a separate slot, not a second Level 4;
        // the dead ID an old save carries must never grant a milestone.
        AssertThat(LegacyUnlockSchedule.LevelNumberOf("level_04a_einstein")).IsEqual(-1);
        AssertThat(LegacyUnlockSchedule.LevelNumberOf("hub")).IsEqual(-1);
        AssertThat(LegacyUnlockSchedule.LevelNumberOf("")).IsEqual(-1);

        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_02_orleans", out AbilitySlot granted)).IsTrue();
        AssertThat(granted).IsEqual(AbilitySlot.Special2);
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_04_paris", out _)).IsFalse();
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_04a_pocahontas", out _)).IsFalse();
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_09_berlin", out _)).IsFalse();
    }

    [TestCase]
    public void TheSlotKeysRoundTripBecauseTheyAreASerializationContract() {
        // A4's grid gating and the save payload both key on these strings.
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            string key = LegacyUnlockSchedule.SlotKey(slot);
            AssertThat(key.Length > 0).IsTrue();
            AssertThat(LegacyUnlockSchedule.TryParseSlotKey(key, out AbilitySlot parsed)).IsTrue();
            AssertThat(parsed).IsEqual(slot);
        }
        AssertThat(LegacyUnlockSchedule.SlotKey(AbilitySlot.MovementAbility)).IsEqual("movement");
        AssertThat(LegacyUnlockSchedule.SlotKey(AbilitySlot.Special1)).IsEqual("special1");
        AssertThat(LegacyUnlockSchedule.SlotKey(AbilitySlot.Special2)).IsEqual("special2");
        AssertThat(LegacyUnlockSchedule.SlotKey(AbilitySlot.Ultimate)).IsEqual("ultimate");
        AssertThat(LegacyUnlockSchedule.TryParseSlotKey("block", out _)).IsFalse();

        HashSet<AbilitySlot> restored = LegacyUnlockSchedule.SlotsFromSavedKeys(
            new[] { "movement", "special1", "nonsense" });
        AssertThat(restored.Count).IsEqual(2);
        AssertThat(restored.Contains(AbilitySlot.MovementAbility)).IsTrue();
        AssertThat(restored.Contains(AbilitySlot.Special2)).IsFalse();
    }

    [TestCase]
    public void AGatedControllerRefusesTheFourSlotsAndAnUngatedOneRefusesNothing() {
        PlayerController gated = BuildBareController();
        PlayerController ungated = BuildBareController();
        try {
            // A fresh campaign: nothing earned yet.
            gated.ApplyLegacyUnlockLocks(new List<AbilitySlot>());
            AssertThat(gated.LegacyAbilityLocksActive).IsTrue();
            foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
                AssertThat(gated.IsAbilityUnlocked(slot))
                    .OverrideFailureMessage($"{slot} must be refused on a fresh campaign.")
                    .IsFalse();
            }

            // Two milestones in.
            gated.ApplyLegacyUnlockLocks(new[] { AbilitySlot.Special1, AbilitySlot.MovementAbility });
            AssertThat(gated.IsAbilityUnlocked(AbilitySlot.MovementAbility)).IsTrue();
            AssertThat(gated.IsAbilityUnlocked(AbilitySlot.Special1)).IsTrue();
            AssertThat(gated.IsAbilityUnlocked(AbilitySlot.Special2)).IsFalse();
            AssertThat(gated.IsAbilityUnlocked(AbilitySlot.Ultimate)).IsFalse();

            // No gate installed at all — the Fighter/Holodeck/Drill shape.
            AssertThat(ungated.LegacyAbilityLocksActive).IsFalse();
            foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
                AssertThat(ungated.IsAbilityUnlocked(slot)).IsTrue();
            }

            // Clearing the gate restores the full kit.
            gated.ApplyLegacyUnlockLocks(null);
            AssertThat(gated.LegacyAbilityLocksActive).IsFalse();
            AssertThat(gated.IsAbilityUnlocked(AbilitySlot.Ultimate)).IsTrue();
        } finally {
            Free(gated);
            Free(ungated);
        }
    }

    [TestCase]
    public void TheFighterHolodeckAndDrillPathsAlwaysBuildTheFullKit() {
        WithFreshCampaignSlot(save => {
            // Story path: the gate installs and every gated slot is dormant.
            PlayerController story = CharacterFactory.CreateCharacter("einstein");
            try {
                AssertThat(story.LegacyAbilityLocksActive)
                    .OverrideFailureMessage("A Story spawn on an active save must carry the gate.")
                    .IsTrue();
                AssertThat(story.IsAbilityUnlocked(AbilitySlot.Ultimate)).IsFalse();
            } finally {
                Free(story);
            }

            // applyStoryProgression: false is the isolation seam every
            // competitive path travels.
            PlayerController fighter = CharacterFactory.CreateCharacter(
                "einstein", 0, applyStoryProgression: false);
            try {
                AssertThat(fighter.LegacyAbilityLocksActive)
                    .OverrideFailureMessage(
                        "Fighter Mode, the Holodeck, the Drills and the Mirror clone must never be gated.")
                    .IsFalse();
                foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
                    AssertThat(fighter.IsAbilityUnlocked(slot)).IsTrue();
                }
            } finally {
                Free(fighter);
            }

            // And a save that has finished Level 3 builds a full Story kit too.
            save.CompletedLevels.AddRange(new[] {
                "level_00_tutorial", "level_01_florence", "level_02_orleans", "level_03_chicago"
            });
            PlayerController veteran = CharacterFactory.CreateCharacter("einstein");
            try {
                AssertThat(veteran.IsAbilityUnlocked(AbilitySlot.Ultimate)).IsTrue();
            } finally {
                Free(veteran);
            }
        });
    }

    [TestCase]
    public void TheMilestoneGrantWritesTheSaveIdempotentlyAndSurvivesARestart() {
        WithFreshCampaignSlot(save => {
            StoryManager story = StoryManager.Instance;
            AssertObject(story).IsNotNull();

            story.GrantLegacyUnlockMilestoneForTests("level_01_florence");
            List<string> granted = save.UnlockedLegacyAbilities["einstein"];
            AssertThat(granted.Contains("movement")).IsTrue();
            AssertThat(story.LastLegacyUnlockSlot).IsEqual(AbilitySlot.MovementAbility);

            // Replaying the level never double-grants, and never replays the beat.
            story.GrantLegacyUnlockMilestoneForTests("level_01_florence");
            AssertThat(granted.FindAll(key => key == "movement").Count).IsEqual(1);
            AssertThat(story.LastLegacyUnlockSlot.HasValue)
                .OverrideFailureMessage("A re-completion must not replay the Resonance Restored beat.")
                .IsFalse();

            // A level that grants nothing clears the beat without touching the list.
            story.GrantLegacyUnlockMilestoneForTests("level_09_berlin");
            AssertThat(granted.Contains("movement")).IsTrue();
            AssertThat(story.LastLegacyUnlockSlot.HasValue).IsFalse();

            // Restart Level clears the attempt registries; the earned kit is not
            // one of them.
            save.ActivatedCheckpointIDs.Clear();
            save.DestroyedExtractorIDs.Clear();
            save.LastCheckpointID = "";
            save.Normalize();
            AssertThat(save.UnlockedLegacyAbilities["einstein"].Contains("movement"))
                .OverrideFailureMessage("Restarting a level must never un-earn an ability.")
                .IsTrue();
            AssertThat(CharacterFactory.ResolveStoryUnlockedSlots("einstein")
                .Contains(AbilitySlot.MovementAbility)).IsTrue();
        });
    }

    [TestCase]
    public void TheUltimateBeatPlaysOnLevelThreesResultsLikeEveryOtherUnlock() {
        // Package 12 W7 (design §2 Level 4A "Results and economy", Low-item
        // decision 2026-09-26): the Package 11 deferral to 4A entry is retired.
        // GR-SCHED: the Ultimate now arrives with Level 3 (the schedule moved one level earlier).
        WithFreshCampaignSlot(save => {
            StoryManager story = StoryManager.Instance;

            story.GrantLegacyUnlockMilestoneForTests("level_02_orleans");
            AssertThat(story.ShouldPlayResonanceRestoredOnResults)
                .OverrideFailureMessage("Special 2's beat plays on Level 2's own results.")
                .IsTrue();

            story.GrantLegacyUnlockMilestoneForTests("level_03_chicago");
            AssertThat(story.LastLegacyUnlockSlot).IsEqual(AbilitySlot.Ultimate);
            AssertThat(story.ShouldPlayResonanceRestoredOnResults)
                .OverrideFailureMessage(
                    "The Ultimate's Resonance Restored beat plays on Level 3's results screen.")
                .IsTrue();
            AssertThat(save.UnlockedLegacyAbilities["einstein"].Contains("ultimate")).IsTrue();

            // A re-completion of Level 3 never replays it.
            story.GrantLegacyUnlockMilestoneForTests("level_03_chicago");
            AssertThat(story.ShouldPlayResonanceRestoredOnResults).IsFalse();
        });
    }

    [TestCase]
    public void LevelFourIsTheFirstFullKitLevelAndARetiredFourARecordGrantsNothing() {
        // Package 13 W2 (S27): Level 4A and its flavour Nexus moment are gone.
        // GR-SCHED: Chicago's completion (Level 3) restores the last slot, so the
        // route's next level, Paris (Level 4), is the first played with the full
        // kit, and neither it nor the Titanic grants a milestone of its own.
        AssertThat(StoryManager.CampaignRoute[StoryManager.RouteIndexOf(CampaignLevel.Chicago) + 1])
            .IsEqual(CampaignLevel.Paris);
        var throughChicago = new List<string> {
            "level_00_tutorial", "level_01_florence", "level_02_orleans", "level_03_chicago"
        };
        var throughParis = new List<string>(throughChicago) { "level_04_paris" };
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            AssertThat(LegacyUnlockSchedule.IsUnlocked(slot, throughChicago))
                .OverrideFailureMessage($"{slot} must be restored before Paris.")
                .IsTrue();
        }
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_04_paris", out _)).IsFalse();
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_05_titanic", out _)).IsFalse();

        // A v7 save that completed its hero's 4A keeps the ID as dead data: on
        // its own it restores nothing, and it never adds a key to the backfill.
        var deadOnly = new List<string> { "level_04a_einstein" };
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            AssertThat(LegacyUnlockSchedule.IsUnlocked(slot, deadOnly)).IsFalse();
        }
        var withDead = new List<string>(throughParis) { "level_04a_einstein" };
        AssertThat(LegacyUnlockSchedule.UnlockedKeysFor(withDead).Count)
            .IsEqual(LegacyUnlockSchedule.UnlockedKeysFor(throughParis).Count);

        // The 4A shared dialogue set (and its Nexus moment) was deleted.
        AssertThat(ResourceLoader.Exists("res://resources/Dialogue/level_04a_shared_dialogue.tres")).IsFalse();
    }

    // === Helpers =======================================================

    /// <summary>
    /// A controller with no scene around it: enough to exercise the gate, which
    /// is plain state plus a published event.
    /// </summary>
    private static PlayerController BuildBareController() {
        var player = new PlayerController { PlayerIndex = 0 };
        return player;
    }

    private static void Free(PlayerController player) {
        if (player == null || !GodotObject.IsInstanceValid(player)) return;
        player.GetParent()?.RemoveChild(player);
        player.Free();
    }

    /// <summary>
    /// Runs <paramref name="body"/> against a scratch story slot holding a fresh
    /// einstein campaign, restoring the real slot and session afterwards.
    /// </summary>
    private static void WithFreshCampaignSlot(System.Action<StorySaveData> body) {
        const int scratchSlot = 2;
        SaveManager saveManager = SaveManager.Instance;
        GameManager gameManager = GameManager.Instance;
        StorySaveData original = saveManager.SaveSlots[scratchSlot];
        SessionData originalSession = gameManager.CurrentSession;
        try {
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            saveManager.SaveSlots[scratchSlot] = save;
            SessionData session = gameManager.CurrentSession;
            session.ActiveSaveSlot = scratchSlot;
            session.SelectedCharacterID = "einstein";
            gameManager.CurrentSession = session;
            body(save);
        } finally {
            saveManager.SaveSlots[scratchSlot] = original;
            gameManager.CurrentSession = originalSession;
        }
    }
}
