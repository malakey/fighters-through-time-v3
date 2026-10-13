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
        // D13: a completed channel now commits synchronously (F10), so no slot.
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
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
            StoryManager.Instance?.CancelPendingHealingForRecovery();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
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
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
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
            StoryManager.Instance?.CancelPendingHealingForRecovery();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
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

    // === D13 (2026-10-11): the placed Restoration Font ===================
    //
    // PROVISIONAL-FONT-PLACEMENT: RestorationFont.Create builds the whole
    // wellspring; a completed channel commits its use and pending heal together
    // (F10) and credits the trickle; the channeler's death cancels it without a
    // refund even while the font is frozen; a second Easy use waits for the
    // first restore; the font refuses while its level says so; the commit writes
    // the wallet; and a Timer Collapse cancels a pending heal.

    [TestCase]
    public void ACreatedFontIsAWellspringWithAPromptARadialAndAGlowThatDimsPerUse() {
        StoryManager story = StoryManager.Instance;
        story.ClearRestorationFonts();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        RestorationFont font = RestorationFont.Create("test.font", Vector2.Zero);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(font);
        try {
            AssertString(font.FontID).IsEqual("test.font");
            var area = font.GetNodeOrNull<InteractionArea>("InteractionArea");
            AssertObject(area).IsNotNull();
            AssertObject(area.GetNodeOrNull(area.TargetPath)).IsEqual(font);
            AssertObject(area.GetNodeOrNull<Label>("Prompt")).IsNotNull();
            var glow = font.GetNodeOrNull<Node2D>("Glow");
            AssertObject(glow).IsNotNull();
            AssertObject(font.ChannelRadial).IsNotNull();
            AssertThat(font.ChannelRadial.Visible).IsFalse();
            AssertFloat(font.ChannelRadial.Fraction).IsEqual(0f);
            AssertThat(font.IsInGroup(RestorationFont.Group)).IsTrue();
            AssertThat(font.RefusesWhile == null).IsTrue();
            AssertFloat(glow.Modulate.A).IsEqualApprox(1f, 0.0001f);

            player.ApplyDamage(player.MaximumHP / 2);
            HoldInteract(player, frames: 1);
            font.Interact(player);
            AssertThat(font.IsChanneling).IsTrue();
            PumpChannel(player, font, frames: 95);
            AssertThat(font.IsChanneling).IsFalse();
            int authored = StoryDifficultyTuning.GetRestorationFontUses(StoryDifficultyTuning.CurrentStoryDifficulty);
            AssertFloat(glow.Modulate.A)
                .IsEqualApprox(0.15f + 0.85f * font.UsesRemaining / (float)authored, 0.0001f);
            AssertThat(font.ChannelRadial.Visible).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            story.ClearRestorationFonts();
            story.CancelPendingHealingForRecovery();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            player.Free();
            font.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void AFontChannelCommitsItsUseAndPendingHealTogetherAndCreditsTheTrickle() {
        StoryManager story = StoryManager.Instance;
        story.ClearRestorationFonts();
        story.CancelPendingHealingForRecovery();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        RestorationFont font = RestorationFont.Create("trickle.font", Vector2.Zero);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(font);
        try {
            player.ApplyDamage(player.MaximumHP - 1);
            AssertThat(player.CurrentHP).IsEqual(1);
            int total = StoryDifficultyTuning.ScaleHeal(player.MaximumHP,
                StoryDifficultyTuning.GetRestorationFontFraction(StoryDifficultyTuning.CurrentStoryDifficulty));
            AssertThat(total).IsGreater(0);

            HoldInteract(player, frames: 1);
            font.Interact(player);
            AssertThat(font.IsChanneling).IsTrue();
            // One tick at a time: stop on the tick that ends the channel.
            int hpBefore = player.CurrentHP;
            for (int tick = 0; tick < 120 && font.IsChanneling; tick++) {
                hpBefore = player.CurrentHP;
                PumpChannel(player, font, frames: 1);
            }
            AssertThat(font.IsChanneling).IsFalse();
            // Whatever the commit tick already paid plus what is still owed is
            // exactly the heal the channel committed.
            AssertFloat(story.PendingHealingRemaining + (player.CurrentHP - hpBefore))
                .OverrideFailureMessage("The use and the whole pending heal commit together at channel completion.")
                .IsEqualApprox(total, 0.0001f);
            AssertThat(story.GetFontUsesConsumed($"{story.CurrentLevel}:trickle.font")).IsEqual(1);

            PumpChannel(player, font, frames: 130);
            AssertFloat(story.PendingHealingRemaining)
                .OverrideFailureMessage("Every paid tick credits the pending heal.")
                .IsEqual(0f);
            AssertThat(player.CurrentHP).IsEqual(Mathf.Min(player.MaximumHP, 1 + total));
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            story.ClearLevelAttemptState();
            story.ClearRestorationFonts();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            player.Free();
            font.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void ADeathMidRestoreCancelsTheRemainderWithoutRefundEvenWhileTheFontIsFrozen() {
        StoryManager story = StoryManager.Instance;
        story.ClearRestorationFonts();
        story.CancelPendingHealingForRecovery();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        RestorationFont font = RestorationFont.Create("death.font", Vector2.Zero);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(font);
        try {
            player.ApplyDamage(player.MaximumHP - 1);
            HoldInteract(player, frames: 1);
            font.Interact(player);
            for (int tick = 0; tick < 120 && font.IsChanneling; tick++) PumpChannel(player, font, frames: 1);
            AssertThat(font.IsChanneling).IsFalse();
            PumpChannel(player, font, frames: 10);
            AssertFloat(story.PendingHealingRemaining).IsGreater(0f);

            // The death rewind freezes the font, so its own tick never sees Dead:
            // the EventBus death hook does the cancel (T01a).
            font.SetTimeFrozen(true);
            player.KillPlayerNonHit();
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dead);
            player.CompleteStoryRewind(player.GlobalPosition, 30);
            font.SetTimeFrozen(false);

            PumpChannel(player, font, frames: 130);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A death mid-restore must not keep healing the revived hero after the thaw.")
                .IsEqual(30);
            AssertFloat(story.PendingHealingRemaining).IsEqual(0f);
            AssertThat(story.GetFontUsesConsumed($"{story.CurrentLevel}:death.font"))
                .OverrideFailureMessage("The completed channel's use is not refunded by the death.")
                .IsEqual(1);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            story.ClearLevelAttemptState();
            story.ClearRestorationFonts();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            player.Free();
            font.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void OnEasyTheSecondUseWaitsForTheFirstRestoreAndHealsInFull() {
        StoryManager story = StoryManager.Instance;
        story.ClearRestorationFonts();
        story.CancelPendingHealingForRecovery();
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.Difficulty = Difficulty.Easy;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        RestorationFont font = RestorationFont.Create("easy.font", Vector2.Zero);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(font);
        try {
            AssertThat(font.UsesRemaining).IsEqual(2);
            var area = font.GetNode<InteractionArea>("InteractionArea");
            var prompt = area.GetNode<Label>("Prompt");
            player.ApplyDamage(player.MaximumHP - 1);
            int heal = StoryDifficultyTuning.ScaleHeal(player.MaximumHP, 0.5f);
            area.RegisterPlayer(player);
            AssertThat(prompt.Visible).IsTrue();

            // First use.
            HoldInteract(player, frames: 1);
            AssertThat(area.TryInteract(player)).IsTrue();
            for (int tick = 0; tick < 120 && font.IsChanneling; tick++) PumpChannel(player, font, frames: 1);
            AssertThat(font.IsChanneling).IsFalse();

            // During the restore the second use is refused rather than overwriting it.
            AssertThat(area.TryInteract(player))
                .OverrideFailureMessage("A second channel must wait for the running restore.")
                .IsFalse();
            AssertThat(font.IsChanneling).IsFalse();

            PumpChannel(player, font, frames: 130);
            AssertThat(player.CurrentHP).IsEqual(1 + heal);
            AssertFloat(story.PendingHealingRemaining).IsEqual(0f);
            AssertThat(prompt.Visible)
                .OverrideFailureMessage("The prompt returns for the second use without leaving the area.")
                .IsTrue();

            // Second use.
            AssertThat(area.TryInteract(player)).IsTrue();
            for (int tick = 0; tick < 120 && font.IsChanneling; tick++) PumpChannel(player, font, frames: 1);
            AssertThat(font.IsChanneling).IsFalse();
            PumpChannel(player, font, frames: 130);
            AssertThat(player.CurrentHP).IsEqual(Mathf.Min(player.MaximumHP, 1 + 2 * heal));
            AssertThat(story.GetFontUsesConsumed($"{story.CurrentLevel}:easy.font")).IsEqual(2);
            AssertThat(font.UsesRemaining).IsEqual(0);
            AssertThat(prompt.Visible).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            story.ClearLevelAttemptState();
            story.ClearRestorationFonts();
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            player.Free();
            font.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void TheFontRefusesWhileItsLevelSaysSoShowsItsChannelAndFinishesAPaidRestore() {
        StoryManager story = StoryManager.Instance;
        story.ClearRestorationFonts();
        story.CancelPendingHealingForRecovery();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        RestorationFont font = RestorationFont.Create("refuse.font", Vector2.Zero);
        bool refused = false;
        font.RefusesWhile = () => refused;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(font);
        try {
            int authored = StoryDifficultyTuning.GetRestorationFontUses(StoryDifficultyTuning.CurrentStoryDifficulty);
            player.ApplyDamage(player.MaximumHP / 2);
            HoldInteract(player, frames: 1);

            // Refused: nothing starts.
            refused = true;
            AssertThat(font.CanInteract(player)).IsFalse();
            font.Interact(player);
            AssertThat(font.IsChanneling).IsFalse();

            // The radial shows the channel; a refusal mid-channel interrupts and refunds.
            refused = false;
            font.Interact(player);
            AssertThat(font.IsChanneling).IsTrue();
            PumpChannel(player, font, frames: 30);
            AssertThat(font.ChannelRadial.Visible).IsTrue();
            AssertFloat(font.ChannelRadial.Fraction).IsBetween(0.3f, 0.4f);
            refused = true;
            PumpChannel(player, font, frames: 2);
            AssertThat(font.IsChanneling).IsFalse();
            AssertThat(font.ChannelRadial.Visible).IsFalse();
            AssertThat(font.UsesRemaining)
                .OverrideFailureMessage("A refusal mid-channel refunds the use like any interrupt.")
                .IsEqual(authored);

            // A restore already paid for completes even once the font refuses.
            refused = false;
            int before = player.CurrentHP;
            font.Interact(player);
            for (int tick = 0; tick < 120 && font.IsChanneling; tick++) PumpChannel(player, font, frames: 1);
            AssertThat(font.IsChanneling).IsFalse();
            refused = true;
            PumpChannel(player, font, frames: 130);
            int heal = StoryDifficultyTuning.ScaleHeal(player.MaximumHP,
                StoryDifficultyTuning.GetRestorationFontFraction(StoryDifficultyTuning.CurrentStoryDifficulty));
            AssertThat(player.CurrentHP).IsEqual(Mathf.Min(player.MaximumHP, before + heal));
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            story.ClearLevelAttemptState();
            story.ClearRestorationFonts();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            player.Free();
            font.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void AFontCommitWritesTheWalletBesideTheClaimsItPersists() {
        // VERIFY-CRITICAL-COMMIT-WALLET: the synchronous font commit persists the
        // claims, so it must persist the wallet that paid them too.
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData originalSave = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        int before = story.ChronalDustCollected;
        try {
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            saves.SaveSlots[scratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.ClearLevelAttemptState();

            EventBus.Instance.RaiseChronalDustCollected(7);
            AssertThat(story.ChronalDustCollected).IsEqual(before + 7);
            LevelRewardDirectory.CommitClaim("level_02.font_wallet_probe");

            story.CommitFontChannelCompletion("Orleans:font_wallet", 20f, 2f);
            AssertThat(save.LevelChronalDust)
                .OverrideFailureMessage("The font commit must write the live wallet beside the claims.")
                .IsEqual(before + 7);
            AssertThat(save.ClaimedRewardSourceIDs.Contains("level_02.font_wallet_probe")).IsTrue();
            AssertThat(save.FontUsesConsumed["Orleans:font_wallet"]).IsEqual(1);
        } finally {
            story.SetDust(before);
            story.ClearLevelAttemptState();
            story.CancelPendingHealingForRecovery();
            saves.SaveSlots[scratchSlot] = originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
        }
    }

    [TestCase]
    public void ATimerCollapseCancelsAPendingFontHealWithoutRefundingTheUse() {
        // STORY_PERSISTENCE :77: a recovery replacing HP cancels pending healing
        // without refunding the use. The Anchor Snap already did; the Acts I-II
        // Collapse now does too (a Timer collapse has no death to cancel it).
        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData originalSave = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        int originalWallet = story.ChronalDustCollected;
        story.SuppressSceneLoadsForTesting = true;
        try {
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            saves.SaveSlots[scratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.BeginLevelRun();

            story.CommitFontChannelCompletion("Orleans:font_collapse", 30f, 2f);
            AssertFloat(story.PendingHealingRemaining).IsEqual(30f);

            story.BeginTimelineCollapse("orleans_checkpoint_1", TimelineCollapseCause.Timer);
            AssertFloat(story.PendingHealingRemaining)
                .OverrideFailureMessage("A Timer Collapse replaces HP, so the pending heal is cancelled.")
                .IsEqual(0f);
            AssertThat(saves.SaveSlots[scratchSlot].AttemptState.PendingHealing.IsActive)
                .OverrideFailureMessage("The collapse save must not persist an active pending heal.")
                .IsFalse();
            AssertThat(story.GetFontUsesConsumed("Orleans:font_collapse"))
                .OverrideFailureMessage("The completed channel's use is not refunded by the Collapse.")
                .IsEqual(1);
            AssertThat(story.CurrentAttempt.Status).IsEqual(StoryAttemptStatus.AwaitingHubResume);
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[scratchSlot] = originalSave;
            story.PrepareDirectLevel(originalLevel,
                string.IsNullOrEmpty(originalCharacter) ? "einstein" : originalCharacter, originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.SetDust(originalWallet);
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
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
    // === Package 11 A3b: F10 healing-loop persistence deltas ==============

    [TestCase]
    public void ACheckpointsGrantedBenefitIsRecordedSeparatelyFromItsActivation() {
        // F10 splits "this anchor is activated" from "this anchor has been PAID".
        // A load or a paid recovery re-reads the activation set; neither may pay
        // the Mending heal or the rewind refresh a second time.
        FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
        try {
            story.BeginLevelRun();
            AssertThat(story.HasGrantedCheckpointBenefit("heal_checkpoint_1")).IsFalse();
            AssertThat(story.TryActivateCheckpoint("heal_checkpoint_1")).IsTrue();
            AssertThat(story.HasGrantedCheckpointBenefit("heal_checkpoint_1"))
                .OverrideFailureMessage("The benefit ID commits with the activation.")
                .IsTrue();

            var save = new FTT.Core.StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            story.ClearLevelAttemptState();
            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.HasGrantedCheckpointBenefit("heal_checkpoint_1"))
                .OverrideFailureMessage("A reload cannot re-grant an already-paid benefit.")
                .IsTrue();
            AssertThat(story.TryActivateCheckpoint("heal_checkpoint_1")).IsFalse();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ACompletedFontChannelPersistsItsUseAndItsUncreditedRemainderTogether() {
        FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
        try {
            story.BeginLevelRun();
            // An interrupted channel never reaches the commit, so it costs nothing.
            AssertThat(story.GetFontUsesConsumed("Orleans:font_a")).IsEqual(0);
            AssertThat(story.PendingHealingRemaining).IsEqual(0f);

            story.CommitFontChannelCompletion("Orleans:font_a", healAmount: 30f, durationSeconds: 2f);
            AssertThat(story.GetFontUsesConsumed("Orleans:font_a"))
                .OverrideFailureMessage("The use decrement commits WITH the pending heal.")
                .IsEqual(1);
            AssertThat(story.PendingHealingRemaining).IsEqual(30f);

            // Half of it lands, then the player quits.
            story.CreditPendingHealing(12f);
            var save = new FTT.Core.StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            story.ClearLevelAttemptState();
            story.RestoreAttemptStateFromSave(save);

            AssertThat(story.PendingHealingRemaining)
                .OverrideFailureMessage("Repeated loads resume only the UNCREDITED remainder.")
                .IsEqual(18f);
            AssertThat(story.GetFontUsesConsumed("Orleans:font_a")).IsEqual(1);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void ADeathRecoveryCancelsPendingHealingWithoutRefundingTheSpentUse() {
        FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
        try {
            story.BeginLevelRun();
            story.CommitFontChannelCompletion("Orleans:font_b", healAmount: 40f, durationSeconds: 2f);
            AssertThat(story.PendingHealingRemaining).IsEqual(40f);

            story.CancelPendingHealingForRecovery();
            AssertThat(story.PendingHealingRemaining)
                .OverrideFailureMessage("A recovery that replaces HP cancels the pending heal.")
                .IsEqual(0f);
            AssertThat(story.GetFontUsesConsumed("Orleans:font_b"))
                .OverrideFailureMessage(
                    "The channel completed; the player simply did not live long enough to be " +
                    "paid. The use is NOT refunded.")
                .IsEqual(1);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    /// <summary>
    /// F10 (STORY_PERSISTENCE :77): the death recovery cancels the attempt's
    /// pending heal even when no font is channelling, which is what a reload
    /// mid-restore leaves (the remainder restored, no channeler;
    /// VERIFY-FONT-PENDING-HEAL-RESUME). The use stays spent.
    /// </summary>
    [TestCase]
    public void ADeathCancelsAPendingHealThatNoFontIsTracking() {
        StoryManager story = StoryManager.Instance;
        story.ClearRestorationFonts();
        story.CancelPendingHealingForRecovery();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        var host = new Node2D { Name = "PendingHealDeathHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            story.SetRewinds(3);
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            var manager = new ChronalRewindManager { Name = "RewindManager" };
            host.AddChild(manager);
            for (int frame = 0; frame < 30; frame++) manager._PhysicsProcess(1.0 / 60.0);

            // What a reload mid-restore restores: a spent use and an uncredited
            // remainder, with no font channelling.
            story.CommitFontChannelCompletion("Orleans:font_reloaded", healAmount: 18f, durationSeconds: 2f);
            AssertThat(story.PendingHealingRemaining).IsEqual(18f);

            player.KillPlayerNonHit();
            AssertThat(manager.IsRewinding)
                .OverrideFailureMessage("The death must start the recovery this case is about.")
                .IsTrue();
            AssertThat(story.PendingHealingRemaining)
                .OverrideFailureMessage("A death recovery replaces HP, so it cancels the pending heal.")
                .IsEqual(0f);
            AssertThat(story.GetFontUsesConsumed("Orleans:font_reloaded"))
                .OverrideFailureMessage("The use is not refunded.")
                .IsEqual(1);

            // Finish the recovery so nothing it holds outlives the case.
            for (int tick = 0; tick < 900 && manager.IsRewinding; tick++) manager._PhysicsProcess(1.0 / 60.0);
            AssertThat(manager.IsRewinding).IsFalse();
            for (int tick = 0; tick <= ChronalRewindManager.PostLandingHoldFrames; tick++) {
                manager._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(player.CurrentHP > 0).IsTrue();
            AssertThat(story.PendingHealingRemaining).IsEqual(0f);
        } finally {
            host.Free();
            story.SetIntegrityClockPause(IntegrityClockPause.DeathRewind, false);
            story.SetRewinds(3);
            story.ClearLevelAttemptState();
            story.ClearRestorationFonts();
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
        }
    }

    [TestCase]
    public void AConsumedFeastOrSalveKeepsItsClaimAcrossAReload() {
        FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
        try {
            story.BeginLevelRun();
            AssertThat(story.TryConsumeHealingPickup("l04_salve_02")).IsTrue();
            AssertThat(story.TryConsumeHealingPickup("l04_salve_02"))
                .OverrideFailureMessage("A pickup ID is consumed once per attempt.")
                .IsFalse();

            var save = new FTT.Core.StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            story.ClearLevelAttemptState();
            AssertThat(story.IsHealingPickupConsumed("l04_salve_02")).IsFalse();

            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.IsHealingPickupConsumed("l04_salve_02"))
                .OverrideFailureMessage("A reload restores the claim; it never reissues the pickup.")
                .IsTrue();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

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
