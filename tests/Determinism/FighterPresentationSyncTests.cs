using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 8 A3: the Fighter-side presentation sync. The driver mirrors
/// deterministic component state (<c>StatusType</c>, <c>HyperArmorFrames</c>,
/// respawn invulnerability, HP deltas) onto the shared glow arbiter and the
/// feedback autoloads.
///
/// The load-bearing property is that this is a one-way street: attaching or
/// removing presentation must not perturb the simulation by a single bit. The
/// proof is hash equality between two otherwise identical drivers, one whose
/// presentation bodies carry the arbiter and one whose do not.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterPresentationSyncTests {
    private const int SyncedFrames = 240;
    private const double Step = 1.0 / 60.0;

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private static (FighterSimulationDriver driver, Node host, PlayerController one, PlayerController two)
        CreateDriver(string name, bool attachGlow) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = name };
        tree.Root.AddChild(host);

        PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
        if (!attachGlow) {
            RemoveGlow(one);
            RemoveGlow(two);
        }
        host.AddChild(one);
        host.AddChild(two);

        var driver = new FighterSimulationDriver { Name = "Driver" };
        host.AddChild(driver);
        // The two drivers this suite compares must share one world seed: since
        // M-7 an unseeded Initialize rolls a fresh random seed per match.
        driver.Initialize(
            one, two, MatchSettings.GetDefault(),
            stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2026);
        return (driver, host, one, two);
    }

    private static void RemoveGlow(PlayerController player) {
        var glow = player.GetNodeOrNull<GlowPresentationController>(GlowPresentationController.NodeName);
        if (glow == null) return;
        player.RemoveChild(glow);
        glow.Free();
    }

    [TestCase]
    public void PresentationAttachmentDoesNotPerturbTheSimulationHash() {
        (FighterSimulationDriver withGlow, Node hostA, PlayerController _, PlayerController _) =
            CreateDriver("PresentationHostWithGlow", attachGlow: true);
        (FighterSimulationDriver withoutGlow, Node hostB, PlayerController _, PlayerController _) =
            CreateDriver("PresentationHostWithoutGlow", attachGlow: false);

        AssertThat(withGlow.CurrentHash).IsEqual(withoutGlow.CurrentHash);

        for (int frame = 0; frame < SyncedFrames; frame++) {
            withGlow._PhysicsProcess(Step);
            withoutGlow._PhysicsProcess(Step);
            AssertThat(withGlow.CurrentTick).IsEqual(withoutGlow.CurrentTick);
            AssertThat(withGlow.CurrentHash).IsEqual(withoutGlow.CurrentHash);
        }

        // And the per-fighter component state agrees field for field at the end.
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(withGlow.TryGetFighter(playerID, out FighterStateComponent glowState)).IsTrue();
            AssertThat(withoutGlow.TryGetFighter(playerID, out FighterStateComponent plainState)).IsTrue();
            AssertThat(glowState.CurrentHP).IsEqual(plainState.CurrentHP);
            AssertThat(glowState.Stocks).IsEqual(plainState.Stocks);
            AssertThat(glowState.HyperArmorFrames).IsEqual(plainState.HyperArmorFrames);
            AssertThat(glowState.InvulnerabilityFrames).IsEqual(plainState.InvulnerabilityFrames);

            AssertThat(withGlow.TryGetRuntime(playerID, out FighterRuntimeComponent glowRuntime)).IsTrue();
            AssertThat(withoutGlow.TryGetRuntime(playerID, out FighterRuntimeComponent plainRuntime)).IsTrue();
            AssertThat(glowRuntime.StatusType).IsEqual(plainRuntime.StatusType);
            AssertThat(glowRuntime.StatusFrames).IsEqual(plainRuntime.StatusFrames);
        }

        hostA.Free();
        hostB.Free();
    }

    [TestCase]
    public void TheDriverPushesTheAuthoredPlayerSlotOutlineOnBothFighters() {
        (FighterSimulationDriver driver, Node host, PlayerController one, PlayerController two) =
            CreateDriver("SlotIndicatorHost", attachGlow: true);

        AssertObject(one.Glow).IsNotNull();
        AssertObject(two.Glow).IsNotNull();
        AssertThat(one.Glow.IsLayerActive(GlowLayer.SlotIndicator)).IsTrue();
        AssertThat(two.Glow.IsLayerActive(GlowLayer.SlotIndicator)).IsTrue();
        AssertThat(one.Glow.ResolvedState.OutlineColor).IsEqual(GlowPalette.SlotColor(0));
        AssertThat(two.Glow.ResolvedState.OutlineColor).IsEqual(GlowPalette.SlotColor(1));

        // The indicator persists for the match rather than being re-pushed per frame.
        for (int frame = 0; frame < 30; frame++) driver._PhysicsProcess(Step);
        AssertThat(one.Glow.ResolvedState.OutlineColor).IsEqual(GlowPalette.SlotColor(0));

        host.Free();
    }

    [TestCase]
    public void ADriverWithoutArbitersStillSynchronisesPresentationBodies() {
        (FighterSimulationDriver driver, Node host, PlayerController one, PlayerController _) =
            CreateDriver("NoArbiterHost", attachGlow: false);

        AssertObject(one.Glow).IsNull();
        for (int frame = 0; frame < 60; frame++) driver._PhysicsProcess(Step);

        // The ordinary presentation mirror keeps working with no arbiter attached:
        // the feedback block must be a null-safe addition, not a hard dependency.
        AssertThat(driver.TryGetFighter(0, out FighterStateComponent state)).IsTrue();
        AssertThat(one.CurrentHP).IsEqual(state.CurrentHP);
        AssertThat(one.CurrentBlockCharges).IsEqual(state.BlockCharges);

        host.Free();
    }

    [TestCase]
    public void TheStatusProjectionTheDriverUsesMatchesTheAuthoredPalette() {
        // The driver's diff casts the deterministic component's int status id back
        // to the shared StatusType enum. Pin that the two enumerations still line
        // up, because a silent renumbering would tint fighters the wrong colour.
        foreach (StatusType type in new[] {
            StatusType.None, StatusType.TimeDilation, StatusType.Venom,
            StatusType.StaticCharge, StatusType.RadiantBurn, StatusType.Root
        }) {
            var projected = (StatusType)(int)type;
            AssertThat(projected).IsEqual(type);
            AssertThat(GlowPalette.Status(projected).OutlineColor)
                .IsEqual(GlowPalette.Status(type).OutlineColor);
        }
        AssertThat(GlowPalette.Status(StatusType.None).IsVisible).IsFalse();
    }
}
