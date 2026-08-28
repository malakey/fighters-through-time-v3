using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.2 Story healing loop ("surviving must beat dying") and its
/// neighbors: the difficulty-scaled healing table, the Restoration Font
/// channel (interrupt refunds the use; the spent state lives on
/// StoryManager), the Chronal Feast populate-by-difficulty rule, the
/// Extractor's idle-cycle discharge (hits no longer trigger it), and the
/// Restart Level wallet rule (all level earnings cleared).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHealingLoopTests {

    [TestCase]
    public void TheHealingTableMatchesTheUnifiedDifficultyScaling() {
        AssertThat(StoryDifficultyTuning.GetCheckpointMendingFraction(Difficulty.Easy)).IsEqual(1.0f);
        AssertThat(StoryDifficultyTuning.GetCheckpointMendingFraction(Difficulty.Normal)).IsEqual(0.5f);
        AssertThat(StoryDifficultyTuning.GetCheckpointMendingFraction(Difficulty.Hard)).IsEqual(0.25f);
        AssertThat(StoryDifficultyTuning.GetRestorationFontUses(Difficulty.Easy)).IsEqual(2);
        AssertThat(StoryDifficultyTuning.GetRestorationFontUses(Difficulty.Normal)).IsEqual(1);
        AssertThat(StoryDifficultyTuning.GetRestorationFontUses(Difficulty.Hard)).IsEqual(1);
        AssertThat(StoryDifficultyTuning.GetRestorationFontFraction(Difficulty.Normal)).IsEqual(0.5f);
        AssertThat(StoryDifficultyTuning.GetRestorationFontFraction(Difficulty.Hard)).IsEqual(0.25f);
        AssertThat(StoryDifficultyTuning.GetChronalFeastFraction(Difficulty.Easy)).IsEqual(0.5f);
        AssertThat(StoryDifficultyTuning.GetChronalFeastFraction(Difficulty.Normal)).IsEqual(0.35f);
        AssertThat(StoryDifficultyTuning.GetChronalFeastFraction(Difficulty.Hard)).IsEqual(0.2f);
        AssertThat(StoryDifficultyTuning.GetChronalFeastCount(Difficulty.Easy)).IsEqual(3);
        AssertThat(StoryDifficultyTuning.GetChronalFeastCount(Difficulty.Normal)).IsEqual(2);
        AssertThat(StoryDifficultyTuning.GetChronalFeastCount(Difficulty.Hard)).IsEqual(1);
        AssertThat(StoryDifficultyTuning.ScaleHeal(100, 0.35f)).IsEqual(35);
    }

    [TestCase]
    public void TheRestorationFontChannelHealsOnceAndAnInterruptRefundsTheUse() {
        StoryManager.Instance?.ClearRestorationFonts();
        // V7.3 range anchor: the channeler must stay put, so the harness gives
        // it a floor to stand on (an unfloored player falls out of the 90 px
        // channel range within half a second of pumping).
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        var font = new RestorationFont { Name = "TestFont", FontID = "test_font" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(font);
        try {
            player.ApplyDamage(player.MaximumHP / 2);
            int woundedHP = player.CurrentHP;

            // Interrupted channel: damage mid-hold cancels and refunds the use.
            HoldInteract(player, frames: 1);
            font.Interact(player);
            AssertThat(font.IsChanneling).IsTrue();
            PumpChannel(player, font, frames: 30);
            player.ApplyDamage(1);
            PumpChannel(player, font, frames: 5);
            AssertThat(font.IsChanneling).IsFalse();
            AssertThat(font.UsesRemaining)
                .OverrideFailureMessage("An interrupted channel must refund the use.")
                .IsEqual(StoryDifficultyTuning.GetRestorationFontUses(
                    StoryDifficultyTuning.CurrentStoryDifficulty));

            // A full channel: 1.5 s hold, then the restore lands over 2 s.
            int beforeChannel = player.CurrentHP;
            font.Interact(player);
            AssertThat(font.IsChanneling).IsTrue();
            PumpChannel(player, font, frames: 95);
            AssertThat(font.IsChanneling).IsFalse();
            PumpChannel(player, font, frames: 130);
            int expectedHeal = StoryDifficultyTuning.ScaleHeal(
                player.MaximumHP,
                StoryDifficultyTuning.GetRestorationFontFraction(
                    StoryDifficultyTuning.CurrentStoryDifficulty));
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("The completed channel restores the authored fraction over 2 s.")
                .IsEqual(Mathf.Min(player.MaximumHP, beforeChannel + expectedHeal));

            // Normal difficulty has exactly one use: the font is now spent, and
            // the spent state lives on StoryManager (it survives a scene swap).
            AssertThat(font.UsesRemaining).IsEqual(
                StoryDifficultyTuning.GetRestorationFontUses(
                    StoryDifficultyTuning.CurrentStoryDifficulty) - 1);
            AssertThat(woundedHP < player.CurrentHP).IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            StoryManager.Instance?.ClearRestorationFonts();
            player.Free();
            font.Free();
            floor.Free();
        }
    }

    /// <summary>
    /// V7.3: the channel is anchored — walking (or being knocked) beyond
    /// <see cref="RestorationFont.ChannelRangePixels"/> interrupts it, and as
    /// with every interrupt the use is refunded (never consumed).
    /// </summary>
    [TestCase]
    public void LeavingTheChannelRangeInterruptsAndRefundsTheUse() {
        StoryManager.Instance?.ClearRestorationFonts();
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        var font = new RestorationFont { Name = "RangeFont", FontID = "range_font" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(font);
        try {
            player.ApplyDamage(player.MaximumHP / 2);
            HoldInteract(player, frames: 1);
            font.Interact(player);
            AssertThat(font.IsChanneling).IsTrue();

            // Drifting inside the range does not interrupt.
            player.GlobalPosition = font.GlobalPosition + new Vector2(font.ChannelRangePixels - 30f, 0f);
            PumpChannel(player, font, frames: 10);
            AssertThat(font.IsChanneling).IsTrue();

            // Leaving the range does, and the use is refunded.
            player.GlobalPosition = font.GlobalPosition + new Vector2(font.ChannelRangePixels + 60f, 0f);
            PumpChannel(player, font, frames: 2);
            AssertThat(font.IsChanneling).IsFalse();
            AssertThat(font.UsesRemaining)
                .OverrideFailureMessage("A range interrupt must refund the use.")
                .IsEqual(StoryDifficultyTuning.GetRestorationFontUses(
                    StoryDifficultyTuning.CurrentStoryDifficulty));
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            StoryManager.Instance?.ClearRestorationFonts();
            player.Free();
            font.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void ChronalFeastAnchorsPopulateByDifficultyAndHealOnce() {
        // Anchor 2 on Normal (count 2) removes itself; anchor 0 populates.
        var overflow = new ChronalFeast { Name = "FeastOverflow", AnchorIndex = 2 };
        var populated = new ChronalFeast { Name = "FeastPopulated", AnchorIndex = 0 };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(overflow);
        tree.Root.AddChild(populated);
        PlayerController player = CreatePlayer();
        try {
            AssertThat(overflow.IsQueuedForDeletion())
                .OverrideFailureMessage("An anchor past the difficulty's count must not populate.")
                .IsTrue();
            AssertThat(populated.IsQueuedForDeletion()).IsFalse();

            player.ApplyDamage(player.MaximumHP / 2);
            int wounded = player.CurrentHP;
            AssertThat(populated.TryConsume(player)).IsTrue();
            int expected = StoryDifficultyTuning.ScaleHeal(
                player.MaximumHP,
                StoryDifficultyTuning.GetChronalFeastFraction(
                    StoryDifficultyTuning.CurrentStoryDifficulty));
            AssertThat(player.CurrentHP).IsEqual(Mathf.Min(player.MaximumHP, wounded + expected));
            AssertThat(populated.TryConsume(player))
                .OverrideFailureMessage("A feast never respawns within an attempt.")
                .IsFalse();
        } finally {
            player.Free();
            if (!overflow.IsQueuedForDeletion()) overflow.Free();
            if (!populated.IsQueuedForDeletion()) populated.Free();
        }
    }

    [TestCase]
    public void TheExtractorDischargesOnItsIdleCycleAndNeverPerHit() {
        var extractor = new ChronalExtractor { Name = "TestExtractor", ObjectID = "test_extractor" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(extractor);
        try {
            AssertThat(extractor.IsTelegraphing).IsFalse();

            // Attacks are free: damage never triggers a discharge (the V6
            // per-hit punishment is the recorded defect this replaces).
            extractor.TakeEnvironmentDamage(10f);
            extractor.TakeEnvironmentDamage(10f);
            AssertThat(extractor.DischargeCount).IsEqual(0);

            // The idle cycle: ~4 s safe -> ~2.5 s telegraph -> one burst.
            PumpExtractor(extractor, seconds: 4.1f);
            AssertThat(extractor.IsTelegraphing)
                .OverrideFailureMessage("The safe window must roll into the visible charge-up.")
                .IsTrue();
            AssertThat(extractor.DischargeCount).IsEqual(0);
            PumpExtractor(extractor, seconds: 2.6f);
            AssertThat(extractor.IsTelegraphing).IsFalse();
            AssertThat(extractor.DischargeCount).IsEqual(1);
        } finally {
            extractor.Free();
        }
    }

    [TestCase]
    public void FontUsesPersistThroughTheSaveAndClearOnRestart() {
        // V7.3: the font's spent state survives a quit-and-resume — it rides
        // the checkpoint save's attempt block — and resets only on a full
        // Restart Level or fresh entry (ClearLevelAttemptState).
        StoryManager story = StoryManager.Instance;
        try {
            story.ClearLevelAttemptState();
            story.RecordFontUse("Orleans:persist_font");
            story.RecordFontUse("Orleans:persist_font");
            AssertThat(story.GetFontUsesConsumed("Orleans:persist_font")).IsEqual(2);

            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            AssertThat(save.FontUsesConsumed["Orleans:persist_font"]).IsEqual(2);

            // The resume path restores the spent state (dying never refills a font).
            story.ClearRestorationFonts();
            AssertThat(story.GetFontUsesConsumed("Orleans:persist_font")).IsEqual(0);
            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.GetFontUsesConsumed("Orleans:persist_font"))
                .OverrideFailureMessage("A mid-level resume must restore consumed font uses.")
                .IsEqual(2);

            // Restart Level clears the whole attempt: the font refills.
            story.ClearLevelAttemptState();
            AssertThat(story.GetFontUsesConsumed("Orleans:persist_font")).IsEqual(0);
        } finally {
            story.ClearLevelAttemptState();
        }
    }

    [TestCase]
    public void RestartLevelClearsEveryPointOfDustEarnedThisLevel() {
        AssertThat(PauseMenu.CalculateRestartWallet(walletDust: 120, earnedThisLevel: 120)).IsEqual(0);
        // Residue that predates the level survives a restart.
        AssertThat(PauseMenu.CalculateRestartWallet(walletDust: 150, earnedThisLevel: 120)).IsEqual(30);
        AssertThat(PauseMenu.CalculateRestartWallet(walletDust: 80, earnedThisLevel: 120)).IsEqual(0);
        AssertThat(PauseMenu.CalculateRestartWallet(walletDust: 0, earnedThisLevel: 0)).IsEqual(0);
    }

    // ---- Harness -------------------------------------------------------------

    private static void HoldInteract(PlayerController player, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Interact));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
    }

    /// <summary>Pumps the player (input refresh) and the font in lockstep.</summary>
    private static void PumpChannel(PlayerController player, RestorationFont font, int frames) {
        for (int frame = 0; frame < frames; frame++) {
            player._PhysicsProcess(1.0 / 60.0);
            font._PhysicsProcess(1.0 / 60.0);
        }
    }

    private static void PumpExtractor(ChronalExtractor extractor, float seconds) {
        int frames = Mathf.CeilToInt(seconds * 60f);
        for (int frame = 0; frame < frames; frame++) extractor._PhysicsProcess(1.0 / 60.0);
    }

    /// <summary>Environment floor with its top at y = 0 so a channeling player
    /// (feet on the node origin) stands still while a test drives frames.</summary>
    private static StaticBody2D CreateFloor() {
        var floor = new StaticBody2D {
            Name = "HealingLoopFloor",
            CollisionLayer = FTT.Core.CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    private static PlayerController CreatePlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        // Interact must be held through the whole channel.
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Interact));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        return player;
    }
}
