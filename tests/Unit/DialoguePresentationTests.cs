using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B3. The dialogue box's presentation contract: the glass panel, the
/// per-emotion portrait treatment, the in/out animation, and the chirp pitch
/// plumbing.
///
/// <para>Every case here drives the real authored scene and a real authored
/// dialogue set (<c>level_08.postboss</c>, whose four lines happen to cover an
/// injured/neutral/confused/determined emotion run and alternate between an NPC
/// speaker and the player). Constructing throwaway
/// <see cref="DialogueSequenceData"/> resources would test the manager against
/// content that does not exist.</para>
///
/// <para><b>Every case restores <see cref="SceneTree.Paused"/> in a finally
/// block.</b> The chosen sequence is authored <c>PausesGameplay = true</c>, and
/// a leaked pause does not fail this suite — it hangs the entire session
/// (CLAUDE.md failure signature 4).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DialoguePresentationTests {

    private const string DialogueSetPath = "res://resources/Dialogue/level_08_dialogue.tres";
    private const string SequenceID = "level_08.postboss";

    // === The glass treatment ===

    [TestCase]
    public void TheSharedThemeCarriesTheDialogueGlassVariation() {
        var theme = ResourceLoader.Load<Theme>(UIPalette.ThemePath);
        AssertObject(theme).IsNotNull();

        AssertThat(theme.HasStylebox("panel", DialogueTheme.GlassPanelVariation))
            .OverrideFailureMessage(
                $"The shared theme must define {DialogueTheme.GlassPanelVariation}/styles/panel.")
            .IsTrue();
        AssertString(theme.GetTypeVariationBase(DialogueTheme.GlassPanelVariation).ToString())
            .OverrideFailureMessage("The variation must be based on PanelContainer to apply to the box.")
            .IsEqual("PanelContainer");

        var glass = theme.GetStylebox("panel", DialogueTheme.GlassPanelVariation) as StyleBoxFlat;
        AssertObject(glass).IsNotNull();

        // Translucency is the whole point of the treatment: a fully opaque box
        // is the old panel with rounder corners.
        AssertThat(glass.BgColor.A < 1f)
            .OverrideFailureMessage($"The glass fill must be translucent; alpha is {glass.BgColor.A}.")
            .IsTrue();
        AssertThat(glass.BgColor.IsEqualApprox(DialogueTheme.GlassBackground)).IsTrue();
        AssertThat(glass.BorderColor.IsEqualApprox(DialogueTheme.GlassBorder)).IsTrue();
        AssertThat(glass.ShadowColor.IsEqualApprox(DialogueTheme.GlassShadow)).IsTrue();
        AssertThat(glass.ShadowSize).IsEqual(DialogueTheme.GlassShadowSize);
        AssertThat(glass.CornerRadiusTopLeft).IsEqual(DialogueTheme.GlassCornerRadius);
        AssertThat(glass.BorderWidthTop).IsEqual(DialogueTheme.GlassBorderWidth);

        // And it must be a heavier read than the utility panel, or the box
        // would be indistinguishable from a settings dialog.
        var utility = theme.GetStylebox("panel", "PanelContainer") as StyleBoxFlat;
        AssertThat(glass.CornerRadiusTopLeft > utility.CornerRadiusTopLeft).IsTrue();
    }

    [TestCase]
    public void TheAuthoredSceneAdoptsTheThemeAndFramesItsPortrait() {
        DialogueManager dialogue = Attach();
        try {
            var panel = dialogue.GetNodeOrNull<PanelContainer>("Panel");
            AssertObject(panel).IsNotNull();
            AssertString(panel.ThemeTypeVariation.ToString())
                .IsEqual(DialogueTheme.GlassPanelVariation);
            AssertObject(panel.Theme)
                .OverrideFailureMessage("The dialogue panel must adopt the shared theme.").IsNotNull();

            var frame = dialogue.GetNodeOrNull<PanelContainer>("Panel/Layout/PortraitFrame");
            AssertObject(frame)
                .OverrideFailureMessage("The portrait needs a frame node to carry its emotion colour.")
                .IsNotNull();
            AssertObject(dialogue.GetNodeOrNull<TextureRect>("Panel/Layout/PortraitFrame/Portrait"))
                .IsNotNull();

            // The literal "(emotion)" label is gone; the frame replaced it.
            AssertObject(dialogue.GetNodeOrNull<Label>("Panel/Layout/Content/SpeakerRow/EmotionLabel"))
                .OverrideFailureMessage("The stage-direction emotion label should no longer exist.")
                .IsNull();

            var style = frame.GetThemeStylebox("panel") as StyleBoxFlat;
            AssertObject(style).IsNotNull();
            AssertThat(style.BorderColor.IsEqualApprox(
                DialogueEmotionTreatment.Resolve(DialogueEmotion.Neutral).FrameColor)).IsTrue();
        } finally {
            Teardown(dialogue);
        }
    }

    // === Emotion presentation ===

    [TestCase]
    public void ThePortraitFrameAndTintFollowEachLinesAuthoredEmotion() {
        TranslationServer.SetLocale("en");
        DialogueManager dialogue = Attach();
        try {
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();

            var portrait = dialogue.GetNode<TextureRect>("Panel/Layout/PortraitFrame/Portrait");
            var frame = dialogue.GetNode<PanelContainer>("Panel/Layout/PortraitFrame");
            var style = frame.GetThemeStylebox("panel") as StyleBoxFlat;

            // Line 0 is authored emotion_injured.
            AssertThat(dialogue.ActiveEmotion.Emotion).IsEqual(DialogueEmotion.Injured);
            AssertThat(style.BorderColor.IsEqualApprox(UIPalette.BossRed)).IsTrue();
            AssertThat(style.BorderWidthTop).IsEqual(DialogueEmotionTreatment.UrgentFrameWidth);
            AssertThat(portrait.SelfModulate.IsEqualApprox(
                DialogueEmotionTreatment.Resolve(DialogueEmotion.Injured).PortraitTint)).IsTrue();
            AssertString(portrait.TooltipText)
                .OverrideFailureMessage(
                    "The localized emotion must survive as the portrait tooltip - it is the only " +
                    "non-colour channel left for it, and it keeps the emotion_* keys referenced.")
                .IsEqual(TranslationServer.Translate("emotion_injured").ToString());

            dialogue.AdvanceLine();
            AssertThat(dialogue.ActiveEmotion.Emotion).IsEqual(DialogueEmotion.Neutral);
            AssertThat(portrait.SelfModulate.IsEqualApprox(Colors.White)).IsTrue();
            AssertThat(style.BorderWidthTop).IsEqual(DialogueEmotionTreatment.CalmFrameWidth);

            dialogue.AdvanceLine();
            AssertThat(dialogue.ActiveEmotion.Emotion).IsEqual(DialogueEmotion.Confused);
            AssertThat(style.BorderColor.IsEqualApprox(UIPalette.SlateDim)).IsTrue();
        } finally {
            Teardown(dialogue);
        }
    }

    /// <summary>
    /// V7.3 UI-scale pass: the speaker and continue-hint labels ride theme
    /// Label variations (HeadingLabel / SmallLabel) and the text body rides
    /// the theme default, with no font-size overrides, so the box follows the
    /// accessibility UI scale.
    /// </summary>
    [TestCase]
    public void TheBoxLabelsUseThemeVariationsWithNoFontSizeOverrides() {
        DialogueManager dialogue = Attach();
        try {
            var speaker = dialogue.GetNode<Label>("Panel/Layout/Content/SpeakerRow/SpeakerLabel");
            AssertThat(speaker.ThemeTypeVariation.ToString()).IsEqual(UIPalette.HeadingLabelVariation);
            AssertThat(speaker.HasThemeFontSizeOverride("font_size")).IsFalse();

            var hint = dialogue.GetNode<Label>("Panel/Layout/Content/ContinueHint");
            AssertThat(hint.ThemeTypeVariation.ToString()).IsEqual(UIPalette.SmallLabelVariation);
            AssertThat(hint.HasThemeFontSizeOverride("font_size")).IsFalse();

            var text = dialogue.GetNode<RichTextLabel>("Panel/Layout/Content/TextLabel");
            AssertThat(text.HasThemeFontSizeOverride("normal_font_size"))
                .OverrideFailureMessage("The dialogue body must ride the theme default font size.")
                .IsFalse();
        } finally {
            Teardown(dialogue);
        }
    }

    // === Chirp plumbing ===

    [TestCase]
    public void ChirpPitchFollowsTheLockedCharacterAndGoesNeutralForEveryOtherSpeaker() {
        DialogueManager dialogue = Attach();
        SessionData session = GameManager.Instance.CurrentSession;
        string originalCharacter = session.SelectedCharacterID;
        try {
            session.SelectedCharacterID = "lincoln";
            GameManager.Instance.CurrentSession = session;

            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();

            // Line 0 is speaker_cleopatra: an NPC, so the neutral pitch.
            AssertThat(dialogue.ActiveChirpPitch).IsEqual(DialogueManager.NeutralChirpPitch);

            // Line 1 is speaker_player: the locked character's authored pitch.
            dialogue.AdvanceLine();
            var lincoln = FTT.Core.AuthoredResources.Load<FTT.Characters.CharacterData>(
                "res://resources/Characters/lincoln_data.tres");
            AssertThat(dialogue.ActiveChirpPitch)
                .OverrideFailureMessage(
                    "A speaker_player line must chirp at the locked character's authored pitch.")
                .IsEqual(lincoln.DialogueChirpPitch);
            AssertThat(dialogue.ActiveChirpPitch != DialogueManager.NeutralChirpPitch)
                .OverrideFailureMessage("Lincoln's authored pitch is the neutral pitch; the test proves nothing.")
                .IsTrue();

            dialogue.AdvanceLine();
            AssertThat(dialogue.ActiveChirpPitch).IsEqual(DialogueManager.NeutralChirpPitch);
        } finally {
            Teardown(dialogue);
            SessionData restore = GameManager.Instance.CurrentSession;
            restore.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession = restore;
        }
    }

    [TestCase]
    public void TheRevealDrivesTheLabelAndTheContinueHintWaitsForTheLineToLand() {
        DialogueManager dialogue = Attach();
        try {
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();

            var label = dialogue.GetNode<RichTextLabel>("Panel/Layout/Content/TextLabel");
            var hint = dialogue.GetNode<Label>("Panel/Layout/Content/ContinueHint");
            AssertThat(dialogue.Reveal.TotalCharacters > 0)
                .OverrideFailureMessage("The authored line must have text to reveal.").IsTrue();
            AssertThat(hint.Visible)
                .OverrideFailureMessage("The continue hint must wait for the reveal.").IsFalse();

            dialogue._Process(0.25);
            AssertThat(label.VisibleCharacters > 0).IsTrue();
            AssertThat(label.VisibleCharacters).IsEqual(dialogue.Reveal.VisibleCharacters);

            // Run it out. The clamp means a long line takes several steps.
            for (int step = 0; step < 200 && !dialogue.Reveal.IsComplete; step++) dialogue._Process(0.25);

            AssertThat(dialogue.Reveal.IsComplete).IsTrue();
            AssertThat(label.VisibleCharacters)
                .OverrideFailureMessage("A completed line must show every character (-1 = all).")
                .IsEqual(-1);
            AssertThat(hint.Visible).IsTrue();
        } finally {
            Teardown(dialogue);
        }
    }

    // === Box animation ===

    [TestCase]
    public void TheBoxSlidesAndFadesInOverTheAuthoredDurationAndBackOutOnCompletion() {
        DialogueManager dialogue = Attach();
        var tree = (SceneTree)Engine.GetMainLoop();
        try {
            var panel = dialogue.GetNode<PanelContainer>("Panel");
            AssertThat(dialogue.BoxAnimationProgress).IsEqual(0f);
            AssertThat(panel.Modulate.A).IsEqual(0f);
            AssertThat(dialogue.Offset.Y).IsEqual(DialogueTheme.BoxSlideDistance);

            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            AssertThat(dialogue.Visible)
                .OverrideFailureMessage("The layer must be visible the moment the box starts opening.")
                .IsTrue();

            dialogue.AdvanceBoxAnimation(DialogueTheme.BoxAnimationSeconds * 0.5f);
            AssertThat(Mathf.IsEqualApprox(dialogue.BoxAnimationProgress, 0.5f)).IsTrue();
            AssertThat(dialogue.IsBoxAnimating).IsTrue();
            AssertThat(panel.Modulate.A > 0f && panel.Modulate.A < 1f).IsTrue();
            AssertThat(dialogue.Offset.Y > 0f && dialogue.Offset.Y < DialogueTheme.BoxSlideDistance)
                .OverrideFailureMessage("The box should be part way through its slide.").IsTrue();

            dialogue.AdvanceBoxAnimation(DialogueTheme.BoxAnimationSeconds);
            AssertThat(dialogue.BoxAnimationProgress).IsEqual(1f);
            AssertThat(dialogue.IsBoxAnimating).IsFalse();
            AssertThat(panel.Modulate.A).IsEqual(1f);
            AssertThat(dialogue.Offset.Y).IsEqual(0f);

            // Walk off the end of the sequence; the box closes rather than
            // vanishing on the same frame.
            for (int step = 0; step < 10 && dialogue.IsSequenceActive; step++) dialogue.AdvanceLine();
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(dialogue.Visible)
                .OverrideFailureMessage("The box must still be on screen while it animates out.").IsTrue();

            dialogue.AdvanceBoxAnimation(DialogueTheme.BoxAnimationSeconds);
            AssertThat(dialogue.BoxAnimationProgress).IsEqual(0f);
            AssertThat(dialogue.Visible)
                .OverrideFailureMessage("Once closed the layer must hide itself.").IsFalse();
        } finally {
            Teardown(dialogue);
            tree.Paused = false;
        }
    }

    /// <summary>
    /// The animation must not gate the pause. Deferring <c>SceneTree.Paused</c>
    /// until the box finished opening would leave a window in which gameplay
    /// still ran under a dialogue box that is already on screen.
    /// </summary>
    [TestCase]
    public void TheGameplayPauseIsTakenOnTheStartFrameNotWhenTheBoxFinishesOpening() {
        var tree = (SceneTree)Engine.GetMainLoop();
        DialogueManager dialogue = Attach();
        bool pausedOnStartFrame;
        bool animatingOnStartFrame;
        try {
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            pausedOnStartFrame = tree.Paused;
            animatingOnStartFrame = dialogue.BoxAnimationProgress < 1f;
        } finally {
            Teardown(dialogue);
            tree.Paused = false;
        }

        AssertThat(pausedOnStartFrame).OverrideFailureMessage(
            "level_08.postboss is authored PausesGameplay = true; the pause must land immediately.")
            .IsTrue();
        AssertThat(animatingOnStartFrame).OverrideFailureMessage(
            "The box had already finished opening, so this case proves nothing about ordering.")
            .IsTrue();
    }

    // === V7.3 hold-to-skip (ruling #19) ===

    [TestCase]
    public void FinishingASequenceRecordsItsIDOnTheSaveExactlyOnce() {
        const int scratchSlot = 2;
        var saveManager = FTT.Core.SaveManager.Instance;
        var gameManager = GameManager.Instance;
        FTT.Core.StorySaveData original = saveManager.SaveSlots[scratchSlot];
        int originalSlot = gameManager.CurrentSession.ActiveSaveSlot;
        var tree = (SceneTree)Engine.GetMainLoop();
        DialogueManager dialogue = Attach();
        try {
            var save = new FTT.Core.StorySaveData { SelectedCharacterID = "einstein" };
            saveManager.SaveSlots[scratchSlot] = save;
            SessionData session = gameManager.CurrentSession;
            session.ActiveSaveSlot = scratchSlot;
            gameManager.CurrentSession = session;

            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            for (int run = 0; run < 2; run++) {
                AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
                for (int step = 0; step < 10 && dialogue.IsSequenceActive; step++) dialogue.AdvanceLine();
                AssertThat(dialogue.IsSequenceActive).IsFalse();
            }

            AssertThat(save.ViewedDialogueIDs.Contains(SequenceID))
                .OverrideFailureMessage("The finished sequence must enter save.ViewedDialogueIDs.")
                .IsTrue();
            AssertThat(save.ViewedDialogueIDs.FindAll(id => id == SequenceID).Count)
                .OverrideFailureMessage("The seen-set must deduplicate repeat viewings.")
                .IsEqual(1);
            AssertThat(save.LastViewedDialogueID).IsEqual(SequenceID);
        } finally {
            Teardown(dialogue);
            tree.Paused = false;
            saveManager.SaveSlots[scratchSlot] = original;
            SessionData restore = gameManager.CurrentSession;
            restore.ActiveSaveSlot = originalSlot;
            gameManager.CurrentSession = restore;
        }
    }

    /// <summary>
    /// Rewritten for V7.6 (Package 11 A5): this case used to pin the retired
    /// "completed campaign AND already seen on this slot" gate. What survives is
    /// the part that still matters — the hold is a 0.75 s commitment, a partial
    /// hold does nothing, and the completed hold fast-forwards through
    /// <c>EndSequence</c>, which is the path that hands <c>SceneTree.Paused</c>
    /// back.
    /// </summary>
    [TestCase]
    public void TheHoldIsACommitmentAndItsSkipReleasesTheGameplayPause() {
        var tree = (SceneTree)Engine.GetMainLoop();
        DialogueManager dialogue = Attach();
        WithScratchSlot(save => {
            // Already seen globally, so no confirmation stands in the way.
            SeenGlobally().Add(SequenceID);
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            AssertThat(tree.Paused).IsTrue();
            AssertThat(dialogue.CanHoldToSkip).IsTrue();

            dialogue.AdvanceSkipHold(0.5f);
            AssertThat(dialogue.IsSequenceActive)
                .OverrideFailureMessage("A partial hold must not skip yet.")
                .IsTrue();
            AssertThat(dialogue.SkipHoldProgress > 0f && dialogue.SkipHoldProgress < 1f).IsTrue();

            dialogue.AdvanceSkipHold(0.3f);
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(tree.Paused)
                .OverrideFailureMessage("The skip must release the gameplay pause.")
                .IsFalse();
        }, dialogue);
    }

    // === V7.6 skip model (Package 11 A5, plan §2.10) =====================

    [TestCase]
    public void EverySequenceIsSkippableNowThatTheCompletedCampaignGateIsGone() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(save => {
            // A first playthrough, a never-seen sequence: V7.3 refused this
            // outright. V7.6 allows it — what it costs is one confirmation.
            AssertThat(save.IsCompleted).IsFalse();
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            AssertThat(dialogue.CanHoldToSkip)
                .OverrideFailureMessage("V7.6: every sequence is skippable, first viewing included.")
                .IsTrue();
        }, dialogue);
    }

    [TestCase]
    public void TheFirstSkipOfAnUnseenSequenceOpensExactlyOneConfirmation() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(save => {
            SeenGlobally().Remove(SequenceID);
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();

            dialogue.AdvanceSkipHold(DialogueManager.HoldToSkipSeconds + 0.1f);
            AssertThat(dialogue.IsSkipConfirmOpen)
                .OverrideFailureMessage("A never-seen sequence must ask before it is skipped away.")
                .IsTrue();
            AssertThat(dialogue.IsSequenceActive)
                .OverrideFailureMessage("The sequence is still running behind the modal.")
                .IsTrue();

            // Backing out resumes the scene, and the hold can be retried.
            dialogue.CancelSkip();
            AssertThat(dialogue.IsSkipConfirmOpen).IsFalse();
            AssertThat(dialogue.IsSequenceActive).IsTrue();
            AssertThat(dialogue.SkipHoldProgress).IsEqual(0f);

            dialogue.AdvanceSkipHold(DialogueManager.HoldToSkipSeconds + 0.1f);
            AssertThat(dialogue.IsSkipConfirmOpen).IsTrue();
            dialogue.ConfirmSkip();
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(DialogueManager.HasSeenSequenceGlobally(SequenceID))
                .OverrideFailureMessage("A confirmed skip records the sequence in the GLOBAL set.")
                .IsTrue();
        }, dialogue);
    }

    [TestCase]
    public void ASequenceSeenOnAnotherSlotSkipsWithNoPrompt() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(save => {
            // Watched in a different playthrough: the global set remembers it,
            // and this slot's own ledger is untouched.
            SeenGlobally().Add(SequenceID);
            AssertThat(save.ViewedDialogueIDs.Contains(SequenceID)).IsFalse();

            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            dialogue.AdvanceSkipHold(DialogueManager.HoldToSkipSeconds + 0.1f);

            AssertThat(dialogue.IsSkipConfirmOpen)
                .OverrideFailureMessage("A sequence seen on any slot must skip without asking again.")
                .IsFalse();
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(save.ViewedDialogueIDs.Contains(SequenceID))
                .OverrideFailureMessage(
                    "Seeing it elsewhere suppresses the prompt only — this slot still earns the effects.")
                .IsTrue();
        }, dialogue);
    }

    [TestCase]
    public void EffectsResolveExactlyOnceWhetherTheSceneIsWatchedOrSkipped() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(save => {
            AssertThat(dialogue.RegisterSetFromPath(DialogueSetPath)).IsTrue();
            SeenGlobally().Add(SequenceID);

            // Skipped: the effects apply, exactly as if it had been watched.
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            dialogue.AdvanceSkipHold(DialogueManager.HoldToSkipSeconds + 0.1f);
            AssertThat(dialogue.IsSequenceActive).IsFalse();
            AssertThat(DialogueManager.HasResolvedDialogueEffects(SequenceID)).IsTrue();
            AssertThat(save.ViewedDialogueIDs.FindAll(id => id == SequenceID).Count).IsEqual(1);

            // Watching it again afterwards does not pay a second time.
            AssertThat(dialogue.StartSequence(SequenceID)).IsTrue();
            for (int step = 0; step < 10 && dialogue.IsSequenceActive; step++) dialogue.AdvanceLine();
            AssertThat(save.ViewedDialogueIDs.FindAll(id => id == SequenceID).Count)
                .OverrideFailureMessage("The effect ledger is idempotent per story slot.")
                .IsEqual(1);
            AssertThat(dialogue.ResolveDialogueEffects(SequenceID))
                .OverrideFailureMessage("A repeat resolution must report that it applied nothing.")
                .IsFalse();
        }, dialogue);
    }

    [TestCase]
    public void AHeroVariantIsSelectedBySavedHeroIdAndCaptiveTokensResolve() {
        DialogueManager dialogue = Attach();
        WithScratchSlot(save => {
            // A6 authors the real variants; the mechanism is what is pinned here.
            var baseSequence = new DialogueSequenceData {
                DialogueID = "p11_a5.variant_probe",
                SpeakerNameKeys = new[] { "speaker_player" },
                LineKeys = new[] { "dialogue_continue" },
                EmotionKeys = new[] { "emotion_neutral" },
                PausesGameplay = false
            };
            var variant = new DialogueSequenceData {
                DialogueID = "p11_a5.variant_probe@leonardo",
                HeroConditionCharacterID = "leonardo",
                SpeakerNameKeys = new[] { "speaker_player" },
                LineKeys = new[] { "dialogue_continue" },
                EmotionKeys = new[] { "emotion_neutral" },
                PausesGameplay = false
            };
            var set = new DialogueSetData {
                DialogueSetID = "p11_a5_probe",
                Sequences = new[] { baseSequence, variant }
            };
            dialogue.RegisterSet(set);

            // Selection reads the SAVED hero ID, never a localized name.
            save.SelectedCharacterID = "einstein";
            AssertThat(dialogue.FindSequence("p11_a5.variant_probe").DialogueID)
                .OverrideFailureMessage("A hero with no variant must get the base sequence.")
                .IsEqual("p11_a5.variant_probe");

            save.SelectedCharacterID = "leonardo";
            AssertThat(dialogue.FindSequence("p11_a5.variant_probe").DialogueID)
                .OverrideFailureMessage("Leonardo's authored variant must win for Leonardo.")
                .IsEqual("p11_a5.variant_probe@leonardo");

            // The two IDs stay DISTINCT strings so the seen/skip bookkeeping can
            // never substitute one for the other across slots (N03).
            AssertThat(set.FindSequence("p11_a5.variant_probe@leonardo", "einstein").DialogueID)
                .IsEqual("p11_a5.variant_probe@leonardo");
            AssertThat(DialogueSequenceData.BaseIDOf("p11_a5.variant_probe@leonardo"))
                .IsEqual("p11_a5.variant_probe");

            // Captive tokens resolve to two distinct absent legends.
            save.SelectedCharacterID = "einstein";
            SessionData session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession = session;
            string rendered = DialogueManager.SubstituteCaptiveNames(
                "They took {CaptiveName1}. And {CaptiveName2}.");
            AssertThat(rendered.Contains("{CaptiveName1}") || rendered.Contains("{CaptiveName2}"))
                .OverrideFailureMessage($"Tokens survived substitution: {rendered}")
                .IsFalse();
            (string first, string second) = FTT.Core.CampaignCaptiveRoster.NamedExamplesFor("einstein");
            AssertThat(first == second).IsFalse();
            AssertThat(first == "einstein" || second == "einstein")
                .OverrideFailureMessage("The active hero can never be their own captive.")
                .IsFalse();
        }, dialogue);
    }

    // === Helpers ===

    /// <summary>The global seen-set the V7.6 skip confirmation reads.</summary>
    private static System.Collections.Generic.HashSet<string> SeenGlobally() {
        FTT.Core.GlobalSaveData global = FTT.Core.SaveManager.Instance.GlobalData;
        global.SeenDialogueIDs ??= new System.Collections.Generic.HashSet<string>(
            System.StringComparer.Ordinal);
        return global.SeenDialogueIDs;
    }

    /// <summary>
    /// Runs a case against scratch slot 2 and a scratch global seen-set,
    /// restoring the real save state, the session and the tree pause afterwards.
    /// </summary>
    private static void WithScratchSlot(
        System.Action<FTT.Core.StorySaveData> body, DialogueManager dialogue) {
        const int scratchSlot = 2;
        var saveManager = FTT.Core.SaveManager.Instance;
        var gameManager = GameManager.Instance;
        FTT.Core.StorySaveData original = saveManager.SaveSlots[scratchSlot];
        SessionData originalSession = gameManager.CurrentSession;
        var originalSeen = new System.Collections.Generic.HashSet<string>(
            SeenGlobally(), System.StringComparer.Ordinal);
        var tree = (SceneTree)Engine.GetMainLoop();
        try {
            var save = new FTT.Core.StorySaveData { SelectedCharacterID = "einstein" };
            saveManager.SaveSlots[scratchSlot] = save;
            SessionData session = gameManager.CurrentSession;
            session.ActiveSaveSlot = scratchSlot;
            session.SelectedCharacterID = "einstein";
            gameManager.CurrentSession = session;
            body(save);
        } finally {
            Teardown(dialogue);
            tree.Paused = false;
            saveManager.SaveSlots[scratchSlot] = original;
            gameManager.CurrentSession = originalSession;
            saveManager.GlobalData.SeenDialogueIDs = originalSeen;
        }
    }

    private static DialogueManager Attach() {
        DialogueManager dialogue = DialogueManager.CreateDefault();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(dialogue);
        return dialogue;
    }

    /// <summary>
    /// Frees the manager and clears the tree pause unconditionally. The manager
    /// hands the pause back itself in <c>_ExitTree</c>; the second write is the
    /// belt-and-braces that keeps a regression from wedging the whole session.
    /// </summary>
    private static void Teardown(DialogueManager dialogue) {
        if (dialogue != null && GodotObject.IsInstanceValid(dialogue)) {
            dialogue.GetParent()?.RemoveChild(dialogue);
            dialogue.Free();
        }
        ((SceneTree)Engine.GetMainLoop()).Paused = false;
    }
}
