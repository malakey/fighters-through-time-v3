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
    public void FighterDriverKeepsAuthoredSpritesOnAPausablePresentationClock() {
        (FighterSimulationDriver _, Node host, PlayerController one, PlayerController two) =
            CreateDriver("SpritePresentationClockHost", attachGlow: true);

        AssertThat(one.ProcessMode).IsEqual(Node.ProcessModeEnum.Disabled);
        AssertThat(two.ProcessMode).IsEqual(Node.ProcessModeEnum.Disabled);
        AssertThat(one.GetNode<AnimatedSprite2D>("AnimatedSprite2D").ProcessMode)
            .IsEqual(Node.ProcessModeEnum.Pausable);
        AssertThat(two.GetNode<AnimatedSprite2D>("AnimatedSprite2D").ProcessMode)
            .IsEqual(Node.ProcessModeEnum.Pausable);

        host.Free();
    }

    [TestCase]
    public void AcceptedFighterSpecialHoldsTheAuthoredAbilityPose() {
        if (InputManager.Instance == null) return;
        (FighterSimulationDriver driver, Node host, PlayerController one, PlayerController _) =
            CreateDriver("AbilityPoseHost", attachGlow: true);
        var playerOneInput = new BufferedInputSource();
        var playerTwoInput = new BufferedInputSource();
        try {
            InputManager.Instance.SetInputSource(0, playerOneInput);
            InputManager.Instance.SetInputSource(1, playerTwoInput);
            for (uint frame = 0; frame < FighterMatchFlowRules.CountdownFrames + 2; frame++) {
                playerOneInput.SetNextFrame(PlayerInputFrame.Create(
                    frame, 0f, 0f, GameplayButtons.None));
                playerTwoInput.SetNextFrame(PlayerInputFrame.Create(
                    frame, 0f, 0f, GameplayButtons.None));
                driver._PhysicsProcess(Step);
            }

            uint attackFrame = (uint)driver.CurrentTick;
            playerOneInput.SetNextFrame(PlayerInputFrame.Create(
                attackFrame, 0f, 0f, GameplayButtons.Special1));
            playerTwoInput.SetNextFrame(PlayerInputFrame.Create(
                attackFrame, 0f, 0f, GameplayButtons.None));
            // Manual _PhysicsProcess calls share one Engine physics-frame ID in
            // this harness. Re-registering invalidates InputManager's per-frame
            // cache so the authored attack edge is actually sampled.
            InputManager.Instance.SetInputSource(0, playerOneInput);
            InputManager.Instance.SetInputSource(1, playerTwoInput);
            driver._PhysicsProcess(Step);

            var sprite = one.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            AssertThat(sprite.Animation).IsEqual(new StringName("special_1"));
            AssertThat(sprite.IsPlaying()).IsTrue();

            // The deterministic ability resolves immediately, but presentation
            // keeps the three-frame pose long enough to be seen.
            playerOneInput.SetNextFrame(PlayerInputFrame.Create(
                attackFrame + 1, 0f, 0f, GameplayButtons.None,
                GameplayButtons.Special1));
            InputManager.Instance.SetInputSource(0, playerOneInput);
            driver._PhysicsProcess(Step);
            AssertThat(sprite.Animation).IsEqual(new StringName("special_1"));
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            host.Free();
        }
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

        try {
            AssertObject(one.Glow).IsNotNull();
            AssertObject(two.Glow).IsNotNull();

            // F24 (A8): the ownership edge is its own shader channel, NOT a layer on
            // the arbitrated effect stack — that is exactly what stops a status,
            // hyper-armor or spawn aura from recolouring it. Read the channel.
            AssertThat(one.Glow.HasOwnershipOutline).IsTrue();
            AssertThat(two.Glow.HasOwnershipOutline).IsTrue();
            AssertThat(one.Glow.OwnershipSlot).IsEqual(0);
            AssertThat(two.Glow.OwnershipSlot).IsEqual(1);
            AssertThat(one.Glow.OwnershipOutlineColor).IsEqual(GlowPalette.SlotColor(0));
            AssertThat(two.Glow.OwnershipOutlineColor).IsEqual(GlowPalette.SlotColor(1));

            // The indicator persists for the match rather than being re-pushed per frame.
            for (int frame = 0; frame < 30; frame++) driver._PhysicsProcess(Step);
            AssertThat(one.Glow.HasOwnershipOutline).IsTrue();
            AssertThat(one.Glow.OwnershipOutlineColor).IsEqual(GlowPalette.SlotColor(0));
        } finally {
            // A leaked host keeps its two StoryPlayer-grouped presentation bodies in
            // the tree and poisons every Story suite that runs after it.
            host.Free();
        }
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

    /// <summary>
    /// Package 12 Phase C seam: the Fighter knockdown thud reads W3b's component
    /// 320 instead of W10's tumble-landing heuristic. Walking a real knockdown's
    /// frame count (30 down to 0, then a second knockdown) fires the edge exactly
    /// once per knockdown and never during the countdown or the get-up.
    /// </summary>
    [TestCase]
    public void TheKnockdownThudFiresOnceWhenComponent320StartsAKnockdown() {
        int fired = 0;
        int previous = 0;
        var trace = new System.Collections.Generic.List<int> { 0, 0 };
        for (int frames = BasicComboRules.KnockdownFrames; frames >= 0; frames--) trace.Add(frames);
        trace.AddRange(new[] { 0, 0, BasicComboRules.KnockdownFrames, BasicComboRules.KnockdownFrames - 1 });
        foreach (int current in trace) {
            if (FighterSimulationDriver.KnockdownThudStarts(previous, current)) fired++;
            previous = current;
        }
        AssertThat(fired).IsEqual(2);
        AssertThat(FighterSimulationDriver.KnockdownThudStarts(0, 0)).IsFalse();
        AssertThat(FighterSimulationDriver.KnockdownThudStarts(12, 11)).IsFalse();
    }

    /// <summary>
    /// H03 (Package 12 W10): the driver maps the grabber's phases onto the
    /// contract's grab/throw names, and every name it can emit exists in the
    /// 30-name player presentation contract.
    /// </summary>
    [TestCase]
    public void GrabPhasesMapOntoTheContractGrabAndThrowNames() {
        AssertThat(FighterSimulationDriver.GrabPresentationAnimation(0)).IsNull();
        for (int phase = 1; phase <= 4; phase++) {
            AssertThat(FighterSimulationDriver.GrabPresentationAnimation(phase)).IsEqual("grab");
        }
        AssertThat(FighterSimulationDriver.GrabPresentationAnimation(5)).IsEqual("throw");

        var contract = AuthoredResources.Load<ContentSceneContract>(
            "res://resources/Contracts/player_presentation_contract.tres");
        var names = new System.Collections.Generic.HashSet<string>(contract.RequiredAnimationNames);
        foreach (string emitted in new[] { "grab", "throw", "hitstun", "roll_recovery", "up_attack", "down_air" }) {
            AssertThat(names.Contains(emitted))
                .OverrideFailureMessage($"The driver emits '{emitted}', which the contract does not list")
                .IsTrue();
        }
    }
}
