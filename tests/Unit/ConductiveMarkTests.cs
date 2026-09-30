using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.6 F07 (Package 11 A1): the Conductive MARK, split out of Static Charge.
/// A mark is a caster-owned combo marker, not a status — it occupies neither
/// status slot, causes no action lock, contributes zero stagger budget, and may
/// remain while the target acts inside a V7.4 armor window. Tesla's finisher now
/// applies BOTH the unchanged 0.4 s Static Charge interrupt AND a 1.5 s mark,
/// 2.5 s with the Story-only <c>tesla_conductive_hold</c> Resonance node; a
/// linked coil fence marks at the baseline and is never extended. Lorentz chains
/// gate on the mark, and T01a clears a dead Tesla's marks though his coils live.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConductiveMarkTests {
    private const float Step = 1f / 60f;

    [TestCase]
    public void TeslaFinisherAuthorsNinetyFrameMarkAlongsideTheUnchangedStaticCharge() {
        BasicStringProfile tesla = BasicComboRules.StringProfileFor("tesla");

        AssertThat(tesla.FinisherStatusType)
            .OverrideFailureMessage("Static Charge stays the finisher's status, unchanged.")
            .IsEqual((int)StatusType.StaticCharge);
        AssertThat(tesla.FinisherStatusFrames)
            .OverrideFailureMessage("The interrupt stays 0.4 s / 24 frames.")
            .IsEqual(24);
        AssertThat(tesla.FinisherMarkType)
            .OverrideFailureMessage("The finisher now also authors a Conductive mark.")
            .IsEqual((int)ComboMarkType.Conductive);
        AssertThat(tesla.FinisherMarkFrames)
            .OverrideFailureMessage("Baseline mark: 90 frames / 1.5 s.")
            .IsEqual(BasicComboRules.ConductiveMarkBaselineFrames);
        AssertThat(BasicComboRules.ConductiveMarkBaselineFrames).IsEqual(90);
        AssertThat(BasicComboRules.ConductiveMarkUpgradedFrames).IsEqual(150);
        // No other character authors a mark: F07 is a Tesla rider.
        foreach (string id in new[] {
            "einstein", "joan", "leonardo", "lincoln",
            "cleopatra", "shakespeare", "mozart", "tubman"
        }) {
            AssertThat(BasicComboRules.StringProfileFor(id).FinisherMarkType)
                .OverrideFailureMessage($"{id} must author no combo mark.")
                .IsEqual((int)ComboMarkType.None);
        }
    }

    [TestCase]
    public void TheStoryNodeExtendsTheFinisherMarkToOneHundredFiftyFramesButNeverTheFence() {
        // Both must enter the tree: PlayerController resolves its authored string
        // profile in _Ready, and an unparented character is still on the template.
        PlayerController baseline = CharacterFactory.CreateCharacter("tesla", applyStoryProgression: false);
        PlayerController upgraded = CharacterFactory.CreateCharacter("tesla", 1, applyStoryProgression: false);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(baseline);
        tree.Root.AddChild(upgraded);
        try {
            AssertThat(baseline.FinisherConductiveMarkFrames())
                .OverrideFailureMessage("Without the node the finisher marks at the baseline.")
                .IsEqual(90);

            upgraded.StoryAbilityPerks.Add(PlayerController.ConductiveHoldPerkKey);
            AssertThat(upgraded.FinisherConductiveMarkFrames())
                .OverrideFailureMessage("tesla_conductive_hold extends the finisher mark to 2.5 s.")
                .IsEqual(150);

            // The cross-mode table is untouched by the Story node — the scaling
            // happens at the application site.
            AssertThat(BasicComboRules.StringProfileFor("tesla").FinisherMarkFrames).IsEqual(90);
            // A linked coil FENCE marks at the baseline and is explicitly not
            // extended by the node.
            AssertThat(BasicComboRules.ConductiveMarkFenceFrames)
                .OverrideFailureMessage("Fence marks stay at 90 frames, node or no node.")
                .IsEqual(90);

            // A non-Tesla character with the node still marks nothing.
            PlayerController einstein = CharacterFactory.CreateCharacter("einstein", applyStoryProgression: false);
            tree.Root.AddChild(einstein);
            einstein.StoryAbilityPerks.Add(PlayerController.ConductiveHoldPerkKey);
            AssertThat(einstein.FinisherConductiveMarkFrames()).IsEqual(0);
            einstein.Free();
        } finally {
            baseline.Free();
            upgraded.Free();
        }
    }

    [TestCase]
    public void AMarkCostsNoStaggerBudgetAndLocksNoActions() {
        EnemyController elite = CreateEnemy("tech_enforcer");
        try {
            elite.Data.StunResistance = 0f;
            EnemyState before = elite.CurrentState;

            elite.ApplyConductiveMark(sourcePlayerID: 0, frames: 90);

            AssertThat(elite.HasConductiveMark).IsTrue();
            AssertThat(elite.StaggerBudgetSeconds)
                .OverrideFailureMessage("A mark must contribute zero stagger budget.")
                .IsEqual(0f);
            AssertThat(elite.CurrentState)
                .OverrideFailureMessage("A mark must not lock the target into any state.")
                .IsEqual(before);
            AssertThat(elite.ControlStatusType)
                .OverrideFailureMessage("A mark must occupy no status slot.")
                .IsEqual(StatusType.None);
            AssertThat(elite.DamageStatusType).IsEqual(StatusType.None);

            // It survives INTO an armor window — "may remain while the target
            // acts in armor" — and is never refused by one.
            elite.ApplyStun(0.9f);
            elite.ApplyStun(0.9f);
            elite.ApplyStun(0.9f);
            AssertThat(elite.IsArmoredRecovery).IsTrue();
            AssertThat(elite.HasConductiveMark)
                .OverrideFailureMessage("Armor must not strip a mark.")
                .IsTrue();
            elite.ApplyConductiveMark(sourcePlayerID: 0, frames: 90);
            AssertThat(elite.HasConductiveMark).IsTrue();
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void AMarkDecaysPerFrameAndIsOnePerTargetPerSource() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.ApplyConductiveMark(sourcePlayerID: 0, frames: 10);
            AssertThat(enemy.ConductiveSourcePlayerID).IsEqual(0);
            AssertThat(enemy.ConductiveFramesRemaining).IsEqual(10);

            // The same source never adds: it takes the LONGER remaining time.
            enemy.ApplyConductiveMark(sourcePlayerID: 0, frames: 4);
            AssertThat(enemy.ConductiveFramesRemaining)
                .OverrideFailureMessage("A shorter same-source mark must not shorten the live one.")
                .IsEqual(10);
            enemy.ApplyConductiveMark(sourcePlayerID: 0, frames: 30);
            AssertThat(enemy.ConductiveFramesRemaining).IsEqual(30);

            // A different source replaces outright — one mark per target.
            enemy.ApplyConductiveMark(sourcePlayerID: 1, frames: 5);
            AssertThat(enemy.ConductiveSourcePlayerID).IsEqual(1);
            AssertThat(enemy.ConductiveFramesRemaining).IsEqual(5);
            AssertThat(enemy.HasConductiveMarkFrom(0)).IsFalse();

            for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.HasConductiveMark)
                .OverrideFailureMessage("The mark must decay on its own frame counter.")
                .IsFalse();
            AssertThat(enemy.ConductiveSourcePlayerID).IsEqual(-1);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void LorentzChainsGateOnTheMarkNotOnStaticCharge() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            // Static Charge alone is NOT chain priming any more: the whole point
            // of F07 is that the chain marker stopped being the action lock.
            enemy.ApplyStatusEffect(StatusType.StaticCharge, 0.4f, 1f);
            AssertThat(enemy.HasStatusEffect(StatusType.StaticCharge)).IsTrue();
            AssertThat(enemy.HasConductiveMarkFrom(0))
                .OverrideFailureMessage("Static Charge must not by itself prime a Lorentz chain.")
                .IsFalse();

            // The mark is the gate, and it is owner-scoped.
            enemy.ApplyConductiveMark(sourcePlayerID: 0, frames: 90);
            AssertThat(enemy.HasConductiveMarkFrom(0)).IsTrue();
            AssertThat(enemy.HasConductiveMarkFrom(1))
                .OverrideFailureMessage("A mark belongs to the Tesla that applied it.")
                .IsFalse();

            // Lorentz Pulse's Root lands in the same control slot the charge
            // occupied, and the mark survives it — the old implementation had to
            // read the status BEFORE the Root replaced it.
            enemy.ApplyStatusEffect(StatusType.Root, 2f, 1f);
            AssertThat(enemy.ControlStatusType).IsEqual(StatusType.Root);
            AssertThat(enemy.HasConductiveMarkFrom(0))
                .OverrideFailureMessage("A control status must never evict the mark.")
                .IsTrue();
        } finally {
            enemy.Free();
        }
    }

    /// <summary>T01a: Tesla's death clears HIS marks even though his coils survive.</summary>
    [TestCase]
    public void TeslaDeathClearsHisOwnMarksAndLeavesAnotherSourceAlone() {
        PlayerController tesla = CharacterFactory.CreateCharacter("tesla", 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(tesla);
        EnemyController marked = CreateEnemy("chrono_slasher");
        EnemyController otherSource = CreateEnemy("chrono_slasher");
        try {
            marked.AddToGroup("Enemies");
            otherSource.AddToGroup("Enemies");
            marked.ApplyConductiveMark(sourcePlayerID: 0, frames: 90);
            otherSource.ApplyConductiveMark(sourcePlayerID: 1, frames: 90);

            tesla.TransitionTo(CharacterState.Dead);

            AssertThat(marked.HasConductiveMark)
                .OverrideFailureMessage("Tesla's death must clear the marks he applied.")
                .IsFalse();
            AssertThat(otherSource.HasConductiveMarkFrom(1))
                .OverrideFailureMessage("Another source's mark must be untouched.")
                .IsTrue();
        } finally {
            marked.Free();
            otherSource.Free();
            tesla.Free();
        }
    }

    private static EnemyController CreateEnemy(string enemyID) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }
}
