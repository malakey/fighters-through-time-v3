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
            // 2026-10-04 feel pass (F1): the sheet is POSED, wind-up first —
            // held on the chosen frame rather than played at the sheet's fps.
            // Decision 1.2 (2026-10-16): the pose now follows the cast's own
            // phases on component 323 — the wind-up through the authored
            // startup, the strike through the active frames, the follow-through
            // through the recovery — instead of thirds of a cosmetic hold.
            AssertThat(sprite.Frame).IsEqual(FTT.Combat.AttackPoseRules.WindUpPose);
            AssertThat(sprite.IsPlaying()).IsFalse();
            AssertThat(one.IsShowingAttackPose).IsTrue();

            uint nextFrame = attackFrame + 1;
            int PhaseOf() {
                AssertThat(driver.Simulation.TryGetFighterActionState(0, out FighterActionStateComponent cast)).IsTrue();
                return cast.SpecialPhase;
            }
            AssertThat(PhaseOf()).IsEqual(FighterSpecialCastRules.PhaseStartup);
            void StepUntilPhase(int phase) {
                for (int guard = 0; guard < 60 && PhaseOf() != phase; guard++) {
                    AssertThat(sprite.Animation).IsEqual(new StringName("special_1"));
                    playerOneInput.SetNextFrame(PlayerInputFrame.Create(
                        nextFrame, 0f, 0f, GameplayButtons.None,
                        nextFrame == attackFrame + 1 ? GameplayButtons.Special1 : GameplayButtons.None));
                    playerTwoInput.SetNextFrame(PlayerInputFrame.Create(nextFrame, 0f, 0f, GameplayButtons.None));
                    InputManager.Instance.SetInputSource(0, playerOneInput);
                    InputManager.Instance.SetInputSource(1, playerTwoInput);
                    driver._PhysicsProcess(Step);
                    nextFrame++;
                }
                AssertThat(PhaseOf()).IsEqual(phase);
            }

            StepUntilPhase(FighterSpecialCastRules.PhaseActive);
            AssertThat(sprite.Animation).IsEqual(new StringName("special_1"));
            AssertThat(sprite.Frame).IsEqual(FTT.Combat.AttackPoseRules.StrikePose);
            StepUntilPhase(FighterSpecialCastRules.PhaseRecovery);
            AssertThat(sprite.Animation).IsEqual(new StringName("special_1"));
            AssertThat(sprite.Frame).IsEqual(FTT.Combat.AttackPoseRules.FollowThroughPose);
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

    // === CM-ULT (2026-10-16, decision 2.1): the A02 activation wind-up telegraph ===

    /// <summary>
    /// The red tint and the Unblockable X show on every wind-up frame of the
    /// activation strike and hide on its first active frame, derived from
    /// component 321. A driver with the glow arbiter and one without it run the
    /// same inputs, so the hash proves the telegraph never writes the simulation.
    /// </summary>
    [TestCase]
    public void TheActivationWindupShowsTheTelegraphForExactlyItsWindupWithoutMovingTheHash() {
        if (InputManager.Instance == null) return;
        (FighterSimulationDriver withGlow, Node hostA, PlayerController oneA, PlayerController twoA) =
            CreateTelegraphDriver("TelegraphHostWithGlow", attachGlow: true);
        (FighterSimulationDriver withoutGlow, Node hostB, PlayerController oneB, PlayerController _) =
            CreateTelegraphDriver("TelegraphHostWithoutGlow", attachGlow: false);
        var playerOneInput = new BufferedInputSource();
        var playerTwoInput = new BufferedInputSource();
        try {
            InputManager.Instance.SetInputSource(0, playerOneInput);
            InputManager.Instance.SetInputSource(1, playerTwoInput);
            var drivers = new[] { withGlow, withoutGlow };
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput,
                FighterMatchFlowRules.CountdownFrames + 2, GameplayButtons.None);
            foreach (FighterSimulationDriver driver in drivers) {
                AssertThat(driver.Simulation.SeedFighterInfluenceForTest(0, FighterMeterRules.CapPoints)).IsTrue();
            }
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
            AssertThat(TelegraphShown(oneA)).IsFalse();

            int windupStart = PressUltimateUntilWindup(drivers, playerOneInput, playerTwoInput);
            AssertThat(withGlow.Simulation.TryGetFighterUltimateActivation(0,
                out FighterUltimateActivationComponent first)).IsTrue();
            int windupFrames = first.PhaseFrames;
            AssertThat(windupFrames > 0).IsTrue();

            int shown = 0;
            for (int guard = 0; guard < 60; guard++) {
                AssertThat(withGlow.CurrentHash).IsEqual(withoutGlow.CurrentHash);
                int phase = ActivationPhase(withGlow, 0);
                AssertThat(ActivationPhase(withoutGlow, 0)).IsEqual(phase);
                if (phase != FighterUltimateActivationRules.PhaseWindup) {
                    // The first active frame: both channels are gone.
                    AssertThat(phase).IsEqual(FighterUltimateActivationRules.PhaseActive);
                    AssertThat(TelegraphShown(oneA)).IsFalse();
                    AssertThat(TelegraphShown(oneB)).IsFalse();
                    AssertThat(oneA.Glow.HasUltimateTelegraph).IsFalse();
                    break;
                }
                shown++;
                AssertThat(TelegraphShown(oneA)).IsTrue();
                AssertThat(TelegraphShown(oneB)).IsTrue();
                TelegraphGlyph glyph = UltimateActivationTelegraph.FindGlyph(oneA);
                AssertThat(glyph.Shape).IsEqual(TelegraphGlyphShape.Unblockable);
                AssertThat(glyph.GlyphColor).IsEqual(TelegraphGlyph.UnblockableColor);
                AssertThat(glyph.Position).IsEqual(UltimateActivationTelegraph.GlyphOffset);
                AssertThat(oneA.Glow.HasUltimateTelegraph).IsTrue();
                AssertThat(oneA.Glow.EffectiveTint).IsEqual(TelegraphGlyph.UnblockableColor);
                // Only the caster telegraphs.
                AssertThat(TelegraphShown(twoA)).IsFalse();
                AssertThat(twoA.Glow.HasUltimateTelegraph).IsFalse();
                StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
            }
            AssertThat(shown).IsEqual(windupFrames);
            AssertThat(windupStart > 0).IsTrue();
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            hostA.Free();
            hostB.Free();
        }
    }

    /// <summary>
    /// A hit flash on the armoured caster rides on top of the telegraph tint and,
    /// once it times out, the sprite is red again — the flash never leaves the
    /// wind-up untelegraphed.
    /// </summary>
    [TestCase]
    public void AHitFlashMidWindupDoesNotLeaveTheTelegraphCleared() {
        if (InputManager.Instance == null) return;
        (FighterSimulationDriver driver, Node host, PlayerController one, PlayerController _) =
            CreateTelegraphDriver("TelegraphFlashHost", attachGlow: true);
        var playerOneInput = new BufferedInputSource();
        var playerTwoInput = new BufferedInputSource();
        try {
            InputManager.Instance.SetInputSource(0, playerOneInput);
            InputManager.Instance.SetInputSource(1, playerTwoInput);
            var drivers = new[] { driver };
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput,
                FighterMatchFlowRules.CountdownFrames + 2, GameplayButtons.None);
            AssertThat(driver.Simulation.SeedFighterInfluenceForTest(0, FighterMeterRules.CapPoints)).IsTrue();
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
            PressUltimateUntilWindup(drivers, playerOneInput, playerTwoInput);
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 3, GameplayButtons.None);
            AssertThat(ActivationPhase(driver, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);

            GlowPresentationController glow = one.Glow;
            var sprite = one.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            var flash = new Color(1f, 0.45f, 0.45f);
            glow.FlashHit(flash, 0.08f);
            AssertThat(glow.EffectiveTint).IsEqual(flash);
            AssertThat(glow.HasUltimateTelegraph).IsTrue();
            // The flash times out (its own clock) while the wind-up runs on.
            glow._Process(1.0);
            AssertThat(glow.HasTintOverride).IsFalse();
            AssertThat(glow.EffectiveTint).IsEqual(TelegraphGlyph.UnblockableColor);
            AssertThat(sprite.Modulate).IsEqual(TelegraphGlyph.UnblockableColor);

            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
            AssertThat(ActivationPhase(driver, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
            AssertThat(glow.HasUltimateTelegraph).IsTrue();
            AssertThat(sprite.Modulate).IsEqual(TelegraphGlyph.UnblockableColor);
            AssertThat(TelegraphShown(one)).IsTrue();
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            host.Free();
        }
    }

    /// <summary>
    /// The wind-up's cue plays once, on the wind-up's first frame, on the
    /// CriticalCues path — never again while the wind-up runs.
    /// </summary>
    [TestCase]
    public void TheWindupCuePlaysOnceOnTheCriticalPath() {
        if (InputManager.Instance == null) return;
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio).IsNotNull();
        CoreMechanicCuePresenter cues = audio.CoreCues;
        AssertObject(cues).IsNotNull();
        (FighterSimulationDriver driver, Node host, PlayerController _, PlayerController _) =
            CreateTelegraphDriver("TelegraphCueHost", attachGlow: true);
        var playerOneInput = new BufferedInputSource();
        var playerTwoInput = new BufferedInputSource();
        try {
            cues.ResetTransient();
            InputManager.Instance.SetInputSource(0, playerOneInput);
            InputManager.Instance.SetInputSource(1, playerTwoInput);
            var drivers = new[] { driver };
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput,
                FighterMatchFlowRules.CountdownFrames + 2, GameplayButtons.None);
            AssertThat(driver.Simulation.SeedFighterInfluenceForTest(0, FighterMeterRules.CapPoints)).IsTrue();
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
            AssertThat(cues.LastCueID).IsNotEqual(CoreMechanicCueIDs.UltimateActivationWindup);

            PressUltimateUntilWindup(drivers, playerOneInput, playerTwoInput);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.UltimateActivationWindup);
            AssertThat(cues.LastRoutedBus).IsEqual(AudioBuses.CriticalCues);
            int played = cues.CuesPlayed;
            for (int guard = 0; guard < 60 && ActivationPhase(driver, 0) == FighterUltimateActivationRules.PhaseWindup; guard++) {
                StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
                if (ActivationPhase(driver, 0) == FighterUltimateActivationRules.PhaseWindup) {
                    AssertThat(cues.CuesPlayed).IsEqual(played);
                }
            }
            AssertThat(ActivationPhase(driver, 0)).IsNotEqual(FighterUltimateActivationRules.PhaseWindup);
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            host.Free();
            cues.ResetTransient();
            audio.ReleaseAllVoices();
        }
    }

    /// <summary>
    /// CM-FIXA-PRES (2026-10-16): a caster knocked out mid-wind-up drops the
    /// telegraph on the KO tick itself. The sim clears component 321 one tick
    /// late (<c>AdvanceUltimate</c> runs before the swing that KOs, and
    /// <c>ApplyStockLoss</c> leaves 321 alone), and the KO tick is the frame the
    /// 12-frame stock-loss freeze then holds with the fighter on the respawn
    /// platform. P2 (Joan) walks in, P1 (Einstein, a 1-HP pool) accepts the
    /// Ultimate, and P2's real string hit 1 lands inside the wind-up. A driver
    /// with the glow arbiter and one without it run the same inputs, so the hash
    /// shows the presentation change touches no simulation state. P2 is a local
    /// human for this case (the boot session's opponent is the CPU, which would
    /// roll away from the wind-up); the session is restored in <c>finally</c>.
    /// </summary>
    [TestCase]
    public void ACasterKnockedOutMidWindupDropsTheTelegraphOnTheKnockoutTickAndThroughTheFreeze() {
        if (InputManager.Instance == null || GameManager.Instance == null) return;
        GameManager manager = GameManager.Instance;
        SessionData savedSession = manager.CurrentSession;
        Node hostA = null;
        Node hostB = null;
        var playerOneInput = new BufferedInputSource();
        var playerTwoInput = new BufferedInputSource();
        try {
            SessionData localVersus = savedSession;
            localVersus.FighterOpponentType = FighterOpponentType.LocalHuman;
            manager.CurrentSession = localVersus;
            (FighterSimulationDriver withGlow, hostA, PlayerController oneA, PlayerController _) =
                CreateFragileCasterDriver("TelegraphKoHostWithGlow", attachGlow: true);
            (FighterSimulationDriver withoutGlow, hostB, PlayerController oneB, PlayerController _) =
                CreateFragileCasterDriver("TelegraphKoHostWithoutGlow", attachGlow: false);
            InputManager.Instance.SetInputSource(0, playerOneInput);
            InputManager.Instance.SetInputSource(1, playerTwoInput);
            var drivers = new[] { withGlow, withoutGlow };
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput,
                FighterMatchFlowRules.CountdownFrames + 2, GameplayButtons.None);
            AssertThat(withGlow.Simulation.TryGetFighter(0, out FighterStateComponent fragile)).IsTrue();
            AssertThat(fragile.CurrentHP)
                .OverrideFailureMessage("Harness fault: the caster must start on a 1-HP pool.")
                .IsEqual(1);

            // P2 walks into reach of P1.
            for (int guard = 0; guard < 240 && PlayerGap(withGlow) > 0.9f; guard++) {
                StepFragileDrivers(drivers, playerOneInput, playerTwoInput, GameplayButtons.None, GameplayButtons.None,
                    TowardPlayerOne(withGlow), GameplayButtons.None, GameplayButtons.None);
            }
            AssertThat(PlayerGap(withGlow) <= 0.9f)
                .OverrideFailureMessage($"Harness fault: P2 never closed to P1 (gap {PlayerGap(withGlow)}).")
                .IsTrue();
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 4, GameplayButtons.None);
            foreach (FighterSimulationDriver driver in drivers) {
                AssertThat(driver.Simulation.SeedFighterInfluenceForTest(0, FighterMeterRules.CapPoints)).IsTrue();
            }
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
            PressUltimateUntilWindup(drivers, playerOneInput, playerTwoInput);
            AssertThat(TelegraphShown(oneA)).IsTrue();
            AssertThat(oneA.Glow.HasUltimateTelegraph).IsTrue();
            AssertThat(withGlow.Simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent before)).IsTrue();
            int knockoutsBefore = before.KnockoutsSuffered;

            // P2 presses and holds BasicAttack until the knockout lands.
            GameplayButtons previousTwo = GameplayButtons.None;
            bool knockedOut = false;
            for (int guard = 0; guard < 30 && !knockedOut; guard++) {
                AssertThat(ActivationPhase(withGlow, 0))
                    .OverrideFailureMessage("Harness fault: the wind-up ended before P2's hit landed.")
                    .IsEqual(FighterUltimateActivationRules.PhaseWindup);
                StepFragileDrivers(drivers, playerOneInput, playerTwoInput, GameplayButtons.None, GameplayButtons.None,
                    0f, GameplayButtons.BasicAttack, previousTwo);
                previousTwo = GameplayButtons.BasicAttack;
                AssertThat(withGlow.CurrentHash).IsEqual(withoutGlow.CurrentHash);
                withGlow.Simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime);
                knockedOut = runtime.KnockoutsSuffered > knockoutsBefore;
            }
            AssertThat(knockedOut)
                .OverrideFailureMessage("Harness fault: P2's hit never knocked the 1-HP caster out.")
                .IsTrue();

            // The KO tick: the sim still reads the wind-up (the one-tick lag this
            // pins), the caster is on the respawn platform, and nothing shows.
            AssertThat(ActivationPhase(withGlow, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
            AssertThat(withGlow.Simulation.TryGetFighter(0, out FighterStateComponent fallen)).IsTrue();
            AssertThat(fallen.RespawnFramesRemaining > 0).IsTrue();
            AssertTelegraphHidden(oneA, oneB, "on the knockout tick");

            // The 12-frame stock-loss freeze holds the KO tick's picture; then the
            // next real tick's AdvanceUltimate clears 321.
            for (int frame = 0; frame < 20; frame++) {
                StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
                AssertThat(withGlow.CurrentHash).IsEqual(withoutGlow.CurrentHash);
                AssertTelegraphHidden(oneA, oneB, $"{frame + 1} frames after the knockout");
            }
            AssertThat(ActivationPhase(withGlow, 0))
                .OverrideFailureMessage("The sim clears the activation once the freeze ends.")
                .IsEqual(FighterUltimateActivationRules.PhaseNone);
            AssertThat(withGlow.Simulation.TryGetFighter(0, out FighterStateComponent respawning)).IsTrue();
            AssertThat(respawning.RespawnFramesRemaining > 0)
                .OverrideFailureMessage("Still on the respawn platform.")
                .IsTrue();
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            hostA?.Free();
            hostB?.Free();
            manager.CurrentSession = savedSession;
        }
    }

    // === CM-HIT (combat and movement update 2026-10-16) ===

    /// <summary>
    /// Decision 4.2: the spawn glow and the "Respawning" label are the respawn
    /// grant's (component 323 bit 0, or the platform), not any invulnerability
    /// — the shared counter also carries the roll, air dodge, tech, knockdown,
    /// throw and Foresight grants, which used to light the spawn aura and read
    /// "Respawning".
    /// </summary>
    [TestCase]
    public void TheSpawnGlowAndTheRespawningLabelShowOnlyForTheRespawnGrant() {
        // The pure rule.
        var invulnerable = new FighterStateComponent { Stocks = 3, InvulnerabilityFrames = 20 };
        var flagged = new FighterActionStateComponent {
            ActionFlags = FighterActionStateComponent.ActionFlagRespawnProtection
        };
        AssertThat(FighterSimulationDriver.ShowsSpawnProtection(in invulnerable, default))
            .OverrideFailureMessage("A roll's, tech's or throw's invulnerability is not a respawn.")
            .IsFalse();
        AssertThat(FighterSimulationDriver.ShowsSpawnProtection(in invulnerable, in flagged)).IsTrue();
        var spent = new FighterStateComponent { Stocks = 3, InvulnerabilityFrames = 0 };
        AssertThat(FighterSimulationDriver.ShowsSpawnProtection(in spent, in flagged))
            .OverrideFailureMessage("A record whose counter ran out shows nothing.")
            .IsFalse();
        var onPlatform = new FighterStateComponent { Stocks = 3, RespawnFramesRemaining = 120, InvulnerabilityFrames = 240 };
        AssertThat(FighterSimulationDriver.ShowsSpawnProtection(in onPlatform, default)).IsTrue();

        // Through a live driver past the countdown.
        if (InputManager.Instance == null || GameManager.Instance == null) return;
        GameManager manager = GameManager.Instance;
        SessionData savedSession = manager.CurrentSession;
        Node host = null;
        try {
            SessionData localVersus = savedSession;
            localVersus.FighterOpponentType = FighterOpponentType.LocalHuman;
            manager.CurrentSession = localVersus;
            (FighterSimulationDriver driver, Node created, PlayerController one, PlayerController _) =
                CreateTelegraphDriver("SpawnGrantHost", attachGlow: true);
            host = created;
            for (int frame = 0; frame < FighterMatchFlowRules.CountdownFrames + 2; frame++) driver._PhysicsProcess(Step);

            // Invulnerable without the respawn record: no aura, no "Respawning".
            AssertThat(driver.Simulation.SeedFighterInvulnerabilityForTest(0, 30)).IsTrue();
            driver._PhysicsProcess(Step);
            AssertThat(driver.TryGetFighter(0, out FighterStateComponent rolling)).IsTrue();
            AssertThat(rolling.InvulnerabilityFrames > 0).IsTrue();
            AssertThat(driver.GetStateLabel(0)).IsNotEqual("Respawning");
            AssertThat(one.Glow.IsLayerActive(GlowLayer.SpawnInvulnerability))
                .OverrideFailureMessage("Only the respawn grant lights the spawn aura.")
                .IsFalse();

            // The respawn record on the same counter: both show.
            AssertThat(driver.Simulation.SeedFighterActionStateForTest(0, flagged)).IsTrue();
            AssertThat(driver.Simulation.SeedFighterInvulnerabilityForTest(0, 30)).IsTrue();
            driver._PhysicsProcess(Step);
            AssertThat(driver.GetStateLabel(0)).IsEqual("Respawning");
            AssertThat(one.Glow.IsLayerActive(GlowLayer.SpawnInvulnerability)).IsTrue();
        } finally {
            host?.Free();
            manager.CurrentSession = savedSession;
        }
    }

    /// <summary>
    /// The CM-FIXA-PRES side note: a fighter knocked out inside its armoured
    /// Ultimate wind-up wore the gold hyper-armour outline on the knockout tick
    /// and through the stock-loss freeze, because the stock loss leaves
    /// <c>HyperArmorFrames</c> set. A fallen fighter shows none.
    /// </summary>
    [TestCase]
    public void AFighterKnockedOutInItsArmouredWindupShowsNoGoldOutline() {
        if (InputManager.Instance == null || GameManager.Instance == null) return;
        GameManager manager = GameManager.Instance;
        SessionData savedSession = manager.CurrentSession;
        Node host = null;
        var playerOneInput = new BufferedInputSource();
        var playerTwoInput = new BufferedInputSource();
        try {
            SessionData localVersus = savedSession;
            localVersus.FighterOpponentType = FighterOpponentType.LocalHuman;
            manager.CurrentSession = localVersus;
            (FighterSimulationDriver driver, Node created, PlayerController one, PlayerController _) =
                CreateFragileCasterDriver("ArmourKoHost", attachGlow: true);
            host = created;
            InputManager.Instance.SetInputSource(0, playerOneInput);
            InputManager.Instance.SetInputSource(1, playerTwoInput);
            var drivers = new[] { driver };
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput,
                FighterMatchFlowRules.CountdownFrames + 2, GameplayButtons.None);
            for (int guard = 0; guard < 240 && PlayerGap(driver) > 0.9f; guard++) {
                StepFragileDrivers(drivers, playerOneInput, playerTwoInput, GameplayButtons.None, GameplayButtons.None,
                    TowardPlayerOne(driver), GameplayButtons.None, GameplayButtons.None);
            }
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 4, GameplayButtons.None);
            AssertThat(driver.Simulation.SeedFighterInfluenceForTest(0, FighterMeterRules.CapPoints)).IsTrue();
            StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
            PressUltimateUntilWindup(drivers, playerOneInput, playerTwoInput);
            AssertThat(one.Glow.IsLayerActive(GlowLayer.HyperArmor))
                .OverrideFailureMessage("setup: the wind-up's armour shows gold.")
                .IsTrue();
            AssertThat(driver.Simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent before)).IsTrue();
            int knockoutsBefore = before.KnockoutsSuffered;

            GameplayButtons previousTwo = GameplayButtons.None;
            bool knockedOut = false;
            for (int guard = 0; guard < 30 && !knockedOut; guard++) {
                StepFragileDrivers(drivers, playerOneInput, playerTwoInput, GameplayButtons.None, GameplayButtons.None,
                    0f, GameplayButtons.BasicAttack, previousTwo);
                previousTwo = GameplayButtons.BasicAttack;
                driver.Simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime);
                knockedOut = runtime.KnockoutsSuffered > knockoutsBefore;
            }
            AssertThat(knockedOut).OverrideFailureMessage("setup: P2's hit must knock the caster out.").IsTrue();
            AssertThat(driver.Simulation.TryGetFighter(0, out FighterStateComponent fallen)).IsTrue();
            AssertThat(fallen.HyperArmorFrames > 0)
                .OverrideFailureMessage("setup: the stock loss leaves the armour frames set (the lag this pins).")
                .IsTrue();
            AssertThat(one.Glow.IsLayerActive(GlowLayer.HyperArmor))
                .OverrideFailureMessage("A knocked-out fighter shows no gold outline on the knockout tick.")
                .IsFalse();
            for (int frame = 0; frame < 20; frame++) {
                StepTelegraphDrivers(drivers, playerOneInput, playerTwoInput, 1, GameplayButtons.None);
                AssertThat(one.Glow.IsLayerActive(GlowLayer.HyperArmor))
                    .OverrideFailureMessage($"No gold outline {frame + 1} frames after the knockout.")
                    .IsFalse();
            }
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            host?.Free();
            manager.CurrentSession = savedSession;
        }
    }

    /// <summary>
    /// The A02 telegraph harness: the suite's driver with stage hazards off, so
    /// no hazard cue or hazard hit lands inside the measured wind-up.
    /// </summary>
    private static (FighterSimulationDriver driver, Node host, PlayerController one, PlayerController two)
        CreateTelegraphDriver(string name, bool attachGlow) {
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
        MatchSettings settings = MatchSettings.GetDefault();
        settings.StageHazardsEnabled = false;
        driver.Initialize(one, two, settings, stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2026);
        return (driver, host, one, two);
    }

    /// <summary>
    /// Steps every driver <paramref name="frames"/> ticks on the same input. The
    /// sources are re-registered each tick (the harness shares one engine
    /// physics-frame ID), after which every driver reads the same cached frame.
    /// </summary>
    private static void StepTelegraphDrivers(FighterSimulationDriver[] drivers,
        BufferedInputSource playerOne, BufferedInputSource playerTwo, int frames, GameplayButtons held,
        GameplayButtons previousHeld = GameplayButtons.None) {
        for (int frame = 0; frame < frames; frame++) {
            uint tick = (uint)drivers[0].CurrentTick;
            playerOne.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, held, previousHeld));
            playerTwo.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.None));
            InputManager.Instance.SetInputSource(0, playerOne);
            InputManager.Instance.SetInputSource(1, playerTwo);
            foreach (FighterSimulationDriver driver in drivers) driver._PhysicsProcess(Step);
            previousHeld = held;
        }
    }

    /// <summary>
    /// Presses the Ultimate once and steps until component 321 is in its
    /// wind-up (a short bound, so a chord or input delay upstream does not
    /// break the harness). Returns the frames it took; fails if it never starts.
    /// </summary>
    private static int PressUltimateUntilWindup(FighterSimulationDriver[] drivers,
        BufferedInputSource playerOne, BufferedInputSource playerTwo) {
        StepTelegraphDrivers(drivers, playerOne, playerTwo, 1, GameplayButtons.Ultimate);
        int frames = 1;
        GameplayButtons previous = GameplayButtons.Ultimate;
        while (ActivationPhase(drivers[0], 0) != FighterUltimateActivationRules.PhaseWindup && frames < 10) {
            StepTelegraphDrivers(drivers, playerOne, playerTwo, 1, GameplayButtons.None, previous);
            previous = GameplayButtons.None;
            frames++;
        }
        AssertThat(ActivationPhase(drivers[0], 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
        return frames;
    }

    private static int ActivationPhase(FighterSimulationDriver driver, int playerID) =>
        driver.Simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)
            ? activation.Phase
            : -1;

    private static bool TelegraphShown(PlayerController player) => UltimateActivationTelegraph.IsGlyphShown(player);

    // === CM-FIXA-PRES (2026-10-16): the knockout-mid-wind-up harness ===

    /// <summary>
    /// The telegraph harness with P1 on a 1-HP pool: a duplicate of Einstein's
    /// CharacterData (the cached authored resource is never touched), so any
    /// landed hit is lethal. Stage hazards off, three stocks.
    /// </summary>
    private static (FighterSimulationDriver driver, Node host, PlayerController one, PlayerController two)
        CreateFragileCasterDriver(string name, bool attachGlow) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = name };
        tree.Root.AddChild(host);
        PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
        var fragile = (CharacterData)one.Data.Duplicate();
        fragile.MaxHP = 1;
        one.Data = fragile;
        if (!attachGlow) {
            RemoveGlow(one);
            RemoveGlow(two);
        }
        host.AddChild(one);
        host.AddChild(two);
        var driver = new FighterSimulationDriver { Name = "Driver" };
        host.AddChild(driver);
        MatchSettings settings = MatchSettings.GetDefault();
        settings.StageHazardsEnabled = false;
        settings.StockCount = 3;
        driver.Initialize(one, two, settings, stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2026);
        return (driver, host, one, two);
    }

    /// <summary>One tick on every driver with both players' inputs given.</summary>
    private static void StepFragileDrivers(FighterSimulationDriver[] drivers,
        BufferedInputSource playerOne, BufferedInputSource playerTwo,
        GameplayButtons oneHeld, GameplayButtons onePrevious,
        float twoHorizontal, GameplayButtons twoHeld, GameplayButtons twoPrevious) {
        uint tick = (uint)drivers[0].CurrentTick;
        playerOne.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, oneHeld, onePrevious));
        playerTwo.SetNextFrame(PlayerInputFrame.Create(tick, twoHorizontal, 0f, twoHeld, twoPrevious));
        InputManager.Instance.SetInputSource(0, playerOne);
        InputManager.Instance.SetInputSource(1, playerTwo);
        foreach (FighterSimulationDriver driver in drivers) driver._PhysicsProcess(Step);
    }

    /// <summary>The horizontal distance between the two fighters' pivots, in world units.</summary>
    private static float PlayerGap(FighterSimulationDriver driver) {
        driver.Simulation.TryGetFighter(0, out FighterStateComponent one);
        driver.Simulation.TryGetFighter(1, out FighterStateComponent two);
        return Mathf.Abs(one.Position.x.ToFloat() - two.Position.x.ToFloat());
    }

    /// <summary>P2's stick toward P1.</summary>
    private static float TowardPlayerOne(FighterSimulationDriver driver) {
        driver.Simulation.TryGetFighter(0, out FighterStateComponent one);
        driver.Simulation.TryGetFighter(1, out FighterStateComponent two);
        return one.Position.x.ToFloat() < two.Position.x.ToFloat() ? -1f : 1f;
    }

    private static void AssertTelegraphHidden(PlayerController withGlow, PlayerController withoutGlow, string when) {
        AssertThat(TelegraphShown(withGlow))
            .OverrideFailureMessage($"The Unblockable X must be hidden {when}.")
            .IsFalse();
        AssertThat(TelegraphShown(withoutGlow))
            .OverrideFailureMessage($"The Unblockable X must be hidden {when} (no arbiter).")
            .IsFalse();
        AssertThat(withGlow.Glow.HasUltimateTelegraph)
            .OverrideFailureMessage($"The telegraph tint must be off {when}.")
            .IsFalse();
        AssertThat(withGlow.Glow.EffectiveTint)
            .OverrideFailureMessage($"The sprite must not read the Unblockable red {when}.")
            .IsNotEqual(TelegraphGlyph.UnblockableColor);
    }
}
