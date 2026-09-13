using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// F12 Option A — the shared Level 4A checkpoint and route contract
/// (<c>docs/design-contracts/LEGACY_CHECKPOINTS.md</c>, Package 11 A12). Driven
/// against the Einstein exemplar, but every assertion here is about
/// <see cref="LegacyLevelControllerBase"/>, so the eight B-wave variants inherit it.
///
/// <para>What it pins: <b>exactly two</b> checkpoints with authored roles Entry and
/// PreBoss, both enabled on Easy, Normal and Hard, with no Middle; the required route
/// order, where PreBoss physically cannot be struck until every mandatory objective
/// is complete; that neither Entry nor the Eraser trigger can lock the Integrity
/// clock; that the one Restoration Font sits after the Eraser and before PreBoss; and
/// that role — never the numeric ID suffix — is what answers "is this checkpoint
/// enabled".</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LegacyCheckpointContractTests {

    private const string ScenePath = "res://scenes/campaign/Level_04A_einstein.tscn";

    [TestCase]
    public void EachVariantAuthorsExactlyTwoCheckpointRolesEntryAndPreBoss() {
        using var fixture = new LegacyFixture();
        Level04AEinsteinController level = fixture.Level;

        IReadOnlyDictionary<string, string> roles = level.CheckpointRoles;
        AssertThat(roles.Count)
            .OverrideFailureMessage("F12 Option A: exactly two checkpoints, no Middle.").IsEqual(2);
        AssertString(roles[level.EntryCheckpointID]).IsEqual(LegacyCheckpointRoles.Entry);
        AssertString(roles[level.PreBossCheckpointID]).IsEqual(LegacyCheckpointRoles.PreBoss);

        // The authored default IDs from the contract table.
        AssertString(level.EntryCheckpointID).IsEqual("level_04a_einstein_checkpoint_0");
        AssertString(level.PreBossCheckpointID).IsEqual("level_04a_einstein_checkpoint_1");

        // No third anchor exists anywhere in the scene.
        var found = new List<string>();
        CollectCheckpoints(level, found);
        AssertThat(found.Count)
            .OverrideFailureMessage($"Expected two fractures, found {found.Count}: {string.Join(", ", found)}")
            .IsEqual(2);
        foreach (string id in found) {
            AssertThat(roles.ContainsKey(id))
                .OverrideFailureMessage($"Checkpoint '{id}' has no authored role.").IsTrue();
        }
    }

    [TestCase]
    public void BothRolesAreEnabledOnEveryDifficultyAndTheHardMiddleRuleCannotDisablePreBoss() {
        using var fixture = new LegacyFixture();
        Level04AEinsteinController level = fixture.Level;

        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            AssertThat(level.IsCheckpointEnabled(level.EntryCheckpointID, difficulty))
                .OverrideFailureMessage($"Entry must be enabled on {difficulty}.").IsTrue();
            AssertThat(level.IsCheckpointEnabled(level.PreBossCheckpointID, difficulty))
                .OverrideFailureMessage(
                    $"PreBoss must be enabled on {difficulty}: Hard's middle-inactive rule applies " +
                    "to shared Acts I-II levels, and 4A has no Middle to disable.").IsTrue();
        }

        // The trap the contract calls out by name: 4A's PreBoss ID ends "_1", which
        // is a MIDDLE suffix on a shared level. Enablement reads the role, so the
        // suffix cannot mislead it.
        AssertThat(level.PreBossCheckpointID.EndsWith("_checkpoint_1", StringComparison.Ordinal)).IsTrue();
        AssertString(level.CheckpointRoles[level.PreBossCheckpointID])
            .IsEqual(LegacyCheckpointRoles.PreBoss);
        AssertThat(level.IsCheckpointEnabled("level_04a_einstein_checkpoint_2", Difficulty.Hard))
            .OverrideFailureMessage("There is no third 4A anchor to enable.").IsFalse();
    }

    [TestCase]
    public void EntrySelfActivatesAndPreBossMustBeStruck() {
        using var fixture = new LegacyFixture();
        Level04AEinsteinController level = fixture.Level;

        AssertObject(level.EntryCheckpoint).IsNotNull();
        AssertObject(level.PreBossCheckpoint).IsNotNull();
        AssertThat(level.EntryCheckpoint.SelfActivating)
            .OverrideFailureMessage("Entry self-activates once on fresh entry.").IsTrue();
        AssertThat(level.PreBossCheckpoint.SelfActivating)
            .OverrideFailureMessage("PreBoss is struck, never walked through.").IsFalse();

        // Both register a respawn anchor with the level manager.
        foreach (string id in new[] { level.EntryCheckpointID, level.PreBossCheckpointID }) {
            AssertThat(level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered with the LevelManager.")
                .IsTrue();
        }
    }

    [TestCase]
    public void PreBossCannotBeStruckUntilEveryMandatoryObjectiveIsComplete() {
        using var fixture = new LegacyFixture();
        Level04AEinsteinController level = fixture.Level;

        // Four kit gates plus the Eraser encounter.
        AssertThat(level.KitGates.Count).IsEqual(4);
        AssertThat(level.MandatoryObjectivesRemaining).IsEqual(5);
        AssertThat(level.CanActivatePreBoss).IsFalse();
        AssertThat(level.PreBossStrikeArmed)
            .OverrideFailureMessage("The PreBoss fracture must be inert while objectives remain.")
            .IsFalse();

        // Resolve the four gates: still barred, because the Eraser is outstanding.
        foreach (LegacyKitGate gate in level.KitGates) gate.ForceResolve();
        AssertThat(level.MandatoryObjectivesRemaining).IsEqual(1);
        AssertThat(level.CanActivatePreBoss).IsFalse();
        AssertThat(level.PreBossStrikeArmed).IsFalse();

        // Clear the Eraser: the route opens and the fracture answers a strike.
        level.EraserDebut.MarkClearedForTest();
        AssertThat(level.MandatoryObjectivesRemaining).IsEqual(0);
        AssertThat(level.CanActivatePreBoss).IsTrue();
        AssertThat(level.PreBossStrikeArmed)
            .OverrideFailureMessage("A complete approach must arm the PreBoss fracture.").IsTrue();
    }

    [TestCase]
    public void NeitherEntryNorTheEraserTriggerLocksTheIntegrityClock() {
        using var fixture = new LegacyFixture();
        Level04AEinsteinController level = fixture.Level;

        // The clock lock is the PreBoss ROLE's privilege. Entry carries the Entry
        // role and the Eraser trigger carries no role at all, so neither can ever
        // be mistaken for the pre-boss transaction.
        AssertString(level.CheckpointRoles[level.EntryCheckpointID]).IsEqual(LegacyCheckpointRoles.Entry);
        AssertThat(level.CheckpointRoles.ContainsKey(level.EraserDebutTriggerID))
            .OverrideFailureMessage("The Eraser route trigger is not a checkpoint and holds no role.")
            .IsFalse();
        AssertString(level.EraserDebutTriggerID).IsEqual("level_04a_einstein_eraser_debut");
        AssertThat(typeof(CheckpointTrigger).IsAssignableFrom(level.EraserDebut.GetType())).IsFalse();
    }

    [TestCase]
    public void TheSingleRestorationFontSitsAfterTheEraserAndBeforePreBoss() {
        using var fixture = new LegacyFixture();
        Level04AEinsteinController level = fixture.Level;

        var fonts = new List<RestorationFont>();
        CollectFonts(level, fonts);
        AssertThat(fonts.Count)
            .OverrideFailureMessage("4A retains exactly one Restoration Font.").IsEqual(1);
        AssertObject(level.Font).IsNotNull();
        AssertString(level.Font.FontID).IsEqual("level_04a_einstein_font");

        // Route order along the level's x axis: Entry < Eraser < Font < PreBoss < boss.
        float entryX = level.EntryCheckpoint.Position.X;
        float eraserX = level.EraserDebut.Position.X;
        float fontX = level.Font.Position.X;
        float preBossX = level.PreBossCheckpoint.Position.X;

        AssertThat(entryX < eraserX).OverrideFailureMessage("The Eraser debut sits after Entry.").IsTrue();
        AssertThat(eraserX < fontX)
            .OverrideFailureMessage("F12: the Font sits on the late approach AFTER the Eraser.").IsTrue();
        AssertThat(fontX < preBossX)
            .OverrideFailureMessage("The Font sits BEFORE PreBoss; it is a healing object, not an anchor.")
            .IsTrue();

        // It grants no anchor: the Font is not registered as a checkpoint.
        AssertThat(level.Levels.TryGetCheckpointPosition(level.Font.FontID, out Vector2 _)).IsFalse();
    }

    /// <summary>
    /// Both code-built interactables need a real collision shape. An Area2D without one
    /// never fires <c>BodyEntered</c>, so the prompt never appears and the prop is
    /// silently uninteractable — the Font unusable and the Nexus gate unreachable, with
    /// no error anywhere.
    /// </summary>
    [TestCase]
    public void TheFontAndTheNexusSourceCarryInteractionAreasWithRealCollisionShapes() {
        using var fixture = new LegacyFixture();
        Level04AEinsteinController level = fixture.Level;

        foreach ((string label, Node2D prop) in new (string, Node2D)[] {
            ("Restoration Font", level.Font), ("Nexus source", level.NexusSource)
        }) {
            var area = prop.GetNodeOrNull<InteractionArea>("Interaction");
            AssertObject(area).OverrideFailureMessage($"{label} has no InteractionArea.").IsNotNull();
            AssertString(area.TargetPath.ToString()).IsEqual("..");

            CollisionShape2D shape = null;
            foreach (Node child in area.GetChildren()) {
                if (child is CollisionShape2D found) shape = found;
            }
            AssertObject(shape)
                .OverrideFailureMessage($"{label}'s InteractionArea has no CollisionShape2D.")
                .IsNotNull();
            AssertObject(shape.Shape)
                .OverrideFailureMessage($"{label}'s interaction shape is unassigned.").IsNotNull();
            AssertThat(area.CollisionMask & CollisionLayers.Player)
                .OverrideFailureMessage($"{label}'s InteractionArea does not watch the Player layer.")
                .IsNotEqual(0u);
        }
    }

    [TestCase]
    public void ReconstructionAtPreBossCompletesTheApproachAndRestoresADisabledNexusSource() {
        using var fixture = new LegacyFixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = ScenePath,
            LastCheckpointID = Level04AEinsteinController.Checkpoint1,
            CurrentHP = 70,
            CurrentUltimateMeter = 25f
        });
        Level04AEinsteinController level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual(Level04AEinsteinController.Checkpoint1);

        AssertThat(level.MandatoryObjectivesRemaining)
            .OverrideFailureMessage(
                "After PreBoss the saved baseline treats every required approach objective as complete.")
            .IsEqual(0);
        foreach (LegacyKitGate gate in level.KitGates) AssertThat(gate.IsResolved).IsTrue();
        AssertThat(level.EraserDebut.EncounterCleared).IsTrue();
        AssertThat(level.EraserDebut.RewardClaimed)
            .OverrideFailureMessage("Already-collected rewards remain claimed.").IsTrue();

        // F10: the source restores disabled for a solved gate, and never armed.
        AssertThat(level.NexusSource.IsArmed).IsFalse();
        AssertThat(level.NexusSource.IsSolved).IsTrue();
        AssertThat(level.PreBossStrikeArmed).IsTrue();
    }

    // === Helpers ===

    private static void CollectCheckpoints(Node node, List<string> found) {
        foreach (Node child in node.GetChildren()) {
            if (child is CheckpointTrigger checkpoint) found.Add(checkpoint.CheckpointID);
            CollectCheckpoints(child, found);
        }
    }

    private static void CollectFonts(Node node, List<RestorationFont> found) {
        foreach (Node child in node.GetChildren()) {
            if (child is RestorationFont font) found.Add(font);
            CollectFonts(child, found);
        }
    }

    /// <summary>
    /// Instantiates the Einstein 4A scene in the runner tree against an optional
    /// scratch save, restoring every shared singleton it touches on dispose.
    /// </summary>
    private sealed class LegacyFixture : IDisposable {
        private const int ScratchSlot = 2;

        public readonly Level04AEinsteinController Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;

        public LegacyFixture(StorySaveData save = null) {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            SessionData session = GameManager.Instance.CurrentSession;
            _originalSlot = session.ActiveSaveSlot;
            _originalCharacter = session.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            session.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            session.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession = session;
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level04AEinsteinController>();
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = _originalSlot;
            session.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession = session;
        }
    }
}
