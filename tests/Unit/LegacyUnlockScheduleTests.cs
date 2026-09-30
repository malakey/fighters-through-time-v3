using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A5 — the V7.5 Legacy Unlock Schedule. The hero's kit is earned
/// across Act I: the Movement Ability after Level 1, Special 1 after Level 2,
/// Special 2 after Level 3, and the Ultimate <i>cast</i> after Level 4. The
/// meter itself, Block, Rally, the death rewind and the basics are never
/// locked — Defy History and Level 4's dampening beams depend on the meter
/// existing from Level 0.
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
    public void TheMilestoneTableMatchesTheV75Schedule() {
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.MovementAbility)).IsEqual(1);
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.Special1)).IsEqual(2);
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.Special2)).IsEqual(3);
        AssertThat(LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.Ultimate)).IsEqual(4);

        // Nothing is unlocked at Level 0 beyond what the schedule never gates.
        var none = new List<string>();
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            AssertThat(LegacyUnlockSchedule.IsUnlocked(slot, none))
                .OverrideFailureMessage($"{slot} must be dormant on a fresh campaign.")
                .IsFalse();
        }

        var afterActOne = new List<string> {
            "level_00_tutorial", "level_01_florence", "level_02_orleans",
            "level_03_chicago", "level_04_paris"
        };
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            AssertThat(LegacyUnlockSchedule.IsUnlocked(slot, afterActOne))
                .OverrideFailureMessage($"{slot} must be restored once Act I is complete.")
                .IsTrue();
        }

        // Partial progress restores exactly the earned slots, in order.
        var afterTwo = new List<string> { "level_01_florence", "level_02_orleans" };
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.MovementAbility, afterTwo)).IsTrue();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Special1, afterTwo)).IsTrue();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Special2, afterTwo)).IsFalse();
        AssertThat(LegacyUnlockSchedule.IsUnlocked(AbilitySlot.Ultimate, afterTwo)).IsFalse();
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
        AssertThat(granted).IsEqual(AbilitySlot.Special1);
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_04a_pocahontas", out _)).IsFalse();
        AssertThat(LegacyUnlockSchedule.TryGrantedSlotForLevel("level_00_tutorial", out _)).IsFalse();
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
                    .OverrideFailureMessage($"{slot} must be refused at Level 0.")
                    .IsFalse();
            }

            // Two milestones in.
            gated.ApplyLegacyUnlockLocks(new[] { AbilitySlot.MovementAbility, AbilitySlot.Special1 });
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

            // And a save that has finished Act I builds a full Story kit too.
            save.CompletedLevels.AddRange(new[] {
                "level_01_florence", "level_02_orleans", "level_03_chicago", "level_04_paris"
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

            story.GrantLegacyUnlockMilestoneForTests("level_02_orleans");
            List<string> granted = save.UnlockedLegacyAbilities["einstein"];
            AssertThat(granted.Contains("special1")).IsTrue();
            AssertThat(story.LastLegacyUnlockSlot).IsEqual(AbilitySlot.Special1);

            // Replaying the level never double-grants, and never replays the beat.
            story.GrantLegacyUnlockMilestoneForTests("level_02_orleans");
            AssertThat(granted.FindAll(key => key == "special1").Count).IsEqual(1);
            AssertThat(story.LastLegacyUnlockSlot.HasValue)
                .OverrideFailureMessage("A re-completion must not replay the Resonance Restored beat.")
                .IsFalse();

            // A level that grants nothing clears the beat without touching the list.
            story.GrantLegacyUnlockMilestoneForTests("level_09_berlin");
            AssertThat(granted.Contains("special1")).IsTrue();
            AssertThat(story.LastLegacyUnlockSlot.HasValue).IsFalse();

            // Restart Level clears the attempt registries; the earned kit is not
            // one of them.
            save.ActivatedCheckpointIDs.Clear();
            save.DestroyedExtractorIDs.Clear();
            save.LastCheckpointID = "";
            save.Normalize();
            AssertThat(save.UnlockedLegacyAbilities["einstein"].Contains("special1"))
                .OverrideFailureMessage("Restarting a level must never un-earn an ability.")
                .IsTrue();
            AssertThat(CharacterFactory.ResolveStoryUnlockedSlots("einstein")
                .Contains(AbilitySlot.Special1)).IsTrue();
        });
    }

    [TestCase]
    public void TheUltimateBeatPlaysOnLevelFoursResultsLikeEveryOtherUnlock() {
        // Package 12 W7 (design §2 Level 4A "Results and economy", Low-item
        // decision 2026-09-26): the Package 11 deferral to 4A entry is retired.
        WithFreshCampaignSlot(save => {
            StoryManager story = StoryManager.Instance;

            story.GrantLegacyUnlockMilestoneForTests("level_03_chicago");
            AssertThat(story.ShouldPlayResonanceRestoredOnResults)
                .OverrideFailureMessage("Special 2's beat plays on Level 3's own results.")
                .IsTrue();

            story.GrantLegacyUnlockMilestoneForTests("level_04_paris");
            AssertThat(story.LastLegacyUnlockSlot).IsEqual(AbilitySlot.Ultimate);
            AssertThat(story.ShouldPlayResonanceRestoredOnResults)
                .OverrideFailureMessage(
                    "The Ultimate's Resonance Restored beat plays on Level 4's results screen.")
                .IsTrue();
            AssertThat(save.UnlockedLegacyAbilities["einstein"].Contains("ultimate")).IsTrue();

            // A re-completion of Level 4 never replays it.
            story.GrantLegacyUnlockMilestoneForTests("level_04_paris");
            AssertThat(story.ShouldPlayResonanceRestoredOnResults).IsFalse();
        });
    }

    [TestCase]
    public void LevelFiveIsTheFirstFullKitLevelAndARetiredFourARecordGrantsNothing() {
        // Package 13 W2 (S27): Level 4A and its flavour Nexus moment are gone.
        // Paris's completion restores the last slot, so the route's next level —
        // the Titanic — is the first played with the full kit, and it grants no
        // milestone of its own.
        AssertThat(StoryManager.CampaignRoute[StoryManager.RouteIndexOf(CampaignLevel.Paris) + 1])
            .IsEqual(CampaignLevel.Titanic);
        var throughParis = new List<string> {
            "level_01_florence", "level_02_orleans", "level_03_chicago", "level_04_paris"
        };
        foreach (AbilitySlot slot in LegacyUnlockSchedule.GatedSlots) {
            AssertThat(LegacyUnlockSchedule.IsUnlocked(slot, throughParis))
                .OverrideFailureMessage($"{slot} must be restored before the Titanic.")
                .IsTrue();
        }
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
