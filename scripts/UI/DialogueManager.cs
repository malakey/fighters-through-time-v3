using Godot;
using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// Production dialogue presentation: 30 characters/second typewriter reveal
    /// with punctuation pacing and per-character text chirps, confirm-to-complete
    /// then confirm-to-advance, a glass panel with a per-emotion portrait
    /// treatment, an in/out slide-and-fade, and optional gameplay suspension
    /// while the UI layer stays responsive. Instanced per campaign scene from
    /// DialogueBox.tscn.
    ///
    /// <para>Package 8 B3 added the presentation; the flow contract is
    /// unchanged, including the one dangerous part of it — a
    /// <c>PausesGameplay</c> sequence owns <see cref="SceneTree.Paused"/> and
    /// this manager is the only thing that ever hands it back.</para>
    /// </summary>
    public partial class DialogueManager : CanvasLayer {
        public static DialogueManager Instance { get; private set; }

        public const string SceneResourcePath = "res://scenes/ui/DialogueBox.tscn";

        /// <summary>Design-locked reveal rate. The model owns the pacing; this is the published number.</summary>
        public const float CharactersPerSecond = DialogueRevealModel.CharactersPerSecond;

        private const string PlayerSpeakerKey = "speaker_player";
        private const string DefaultPortraitPath = "res://assets/placeholders/portrait.svg";

        /// <summary>Chirp pitch for narration and any speaker who is not the player's character.</summary>
        public const float NeutralChirpPitch = 1.0f;

        public bool IsSequenceActive => _isActive;
        public string ActiveDialogueID => _currentSequence?.DialogueID ?? "";

        /// <summary>Pitch the current line's chirps are playing at. Test/diagnostic surface.</summary>
        public float ActiveChirpPitch { get; private set; } = NeutralChirpPitch;

        /// <summary>
        /// Box animation progress: 0 fully hidden, 1 fully presented. Driven by
        /// <see cref="AdvanceBoxAnimation"/> from <c>_Process</c>.
        /// </summary>
        public float BoxAnimationProgress { get; private set; }

        /// <summary>True while the box is animating in or out.</summary>
        public bool IsBoxAnimating =>
            BoxAnimationProgress > 0f && BoxAnimationProgress < 1f;

        /// <summary>The reveal model driving the current line. Never null.</summary>
        public DialogueRevealModel Reveal { get; } = new();

        /// <summary>The emotion treatment applied to the portrait for the current line.</summary>
        public DialogueEmotionTreatment.Treatment ActiveEmotion { get; private set; } =
            DialogueEmotionTreatment.Resolve(DialogueEmotion.Neutral);

        // === Hold-to-skip — V7.3 ruling #19, widened by V7.6 (Package 11 A5) =
        // V7.3 allowed the hold only on a completed campaign, and only for a
        // sequence that slot had already seen. V7.6 (plan §2.10 item 3) drops
        // BOTH gates: every sequence is skippable, first viewing included.
        // What replaces them is a single confirmation — the first time a
        // sequence is skipped, a modal says it will not replay — keyed on the
        // GLOBAL GlobalSaveData.SeenDialogueIDs set, so a scene watched in
        // another playthrough never asks again.
        //
        // The gameplay half is ResolveDialogueEffects: a skipped sequence
        // applies exactly the same effects as a watched one, exactly once per
        // story slot. Seeing a scene in another slot suppresses only the
        // confirmation — never another slot's effects.

        /// <summary>Seconds the confirm action must be held to skip a sequence.</summary>
        public const float HoldToSkipSeconds = 0.75f;

        /// <summary>True while the active sequence can be fast-forwarded by a hold.</summary>
        public bool CanHoldToSkip => _isActive && _skipEligible && !IsSkipConfirmOpen;

        /// <summary>True while the first-viewing skip confirmation is on screen.</summary>
        public bool IsSkipConfirmOpen => _skipConfirm != null && _skipConfirm.IsOpen;

        /// <summary>Skip-hold fill, 0..1 — drives the hint's progress affordance.</summary>
        public float SkipHoldProgress =>
            Mathf.Clamp(_skipHoldSeconds / HoldToSkipSeconds, 0f, 1f);

        private bool _skipEligible;
        private float _skipHoldSeconds;
        private Control _skipHintRow;
        private Label _skipHintLabel;
        private ProgressBar _skipProgressBar;
        private ConfirmModal _skipConfirm;

        private PanelContainer _dialoguePanel;
        private PanelContainer _portraitFrame;
        private StyleBoxFlat _portraitFrameStyle;
        private TextureRect _portrait;
        private Label _speakerLabel;
        private RichTextLabel _textLabel;
        private Label _continueHint;
        private Texture2D _defaultPortrait;

        private readonly List<DialogueSetData> _registeredSets = new();
        private DialogueSequenceData _currentSequence;
        private int _currentIndex;
        private bool _isActive;
        private float _autoAdvanceTimer;
        private bool _pausedGameplay;
        private bool _boxOpening;

        private bool IsTyping => _isActive && !Reveal.IsComplete;

        /// <summary>Instantiates the authored DialogueBox scene, or a code-built fallback.</summary>
        public static DialogueManager CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is DialogueManager authored) return authored;
            }
            return new DialogueManager { Name = "DialogueBox" };
        }

        public override void _Ready() {
            Instance = this;
            Layer = 90;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            _defaultPortrait = ResourceLoader.Exists(DefaultPortraitPath)
                ? ResourceLoader.Load<Texture2D>(DefaultPortraitPath)
                : null;
            ResolveOrBuildUI();
            ApplyPresentationTreatment();
            if (FTT.Core.EventBus.Instance != null)
                FTT.Core.EventBus.Instance.OnDialogueTriggered += OnDialogueTriggered;
        }

        public override void _ExitTree() {
            // A sequence with PausesGameplay owns SceneTree.Paused, and this manager
            // is the only thing that ever hands it back. If the manager leaves the
            // tree mid-sequence - scene change, level teardown, quit to menu, a test
            // freeing the host - nothing else releases it and the ENTIRE process
            // stays frozen, including whatever scene loads next. Release it here as
            // well as in EndSequence(). See CLAUDE.md failure signature 4.
            ReleaseGameplayPause();
            _isActive = false;
            _currentSequence = null;
            if (Instance == this) Instance = null;
            if (FTT.Core.EventBus.Instance != null)
                FTT.Core.EventBus.Instance.OnDialogueTriggered -= OnDialogueTriggered;
        }

        /// <summary>
        /// Hands SceneTree.Paused back if - and only if - this manager is the one
        /// that took it. Safe to call repeatedly and outside the tree.
        /// </summary>
        private void ReleaseGameplayPause() {
            if (!_pausedGameplay) return;
            _pausedGameplay = false;
            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = false;
        }

        private void ResolveOrBuildUI() {
            _dialoguePanel = GetNodeOrNull<PanelContainer>("Panel");
            if (_dialoguePanel == null) {
                BuildFallbackUI();
                return;
            }
            _portraitFrame = GetNodeOrNull<PanelContainer>("Panel/Layout/PortraitFrame");
            _portrait = GetNodeOrNull<TextureRect>("Panel/Layout/PortraitFrame/Portrait");
            _speakerLabel = GetNodeOrNull<Label>("Panel/Layout/Content/SpeakerRow/SpeakerLabel");
            _textLabel = GetNodeOrNull<RichTextLabel>("Panel/Layout/Content/TextLabel");
            _continueHint = GetNodeOrNull<Label>("Panel/Layout/Content/ContinueHint");
        }

        private void BuildFallbackUI() {
            _dialoguePanel = new PanelContainer {
                Name = "Panel",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _dialoguePanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _dialoguePanel.OffsetLeft = 100;
            _dialoguePanel.OffsetRight = -100;
            _dialoguePanel.OffsetTop = -240;
            _dialoguePanel.OffsetBottom = -40;
            AddChild(_dialoguePanel);

            var layout = new HBoxContainer { Name = "Layout" };
            _dialoguePanel.AddChild(layout);

            _portraitFrame = new PanelContainer {
                Name = "PortraitFrame",
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            layout.AddChild(_portraitFrame);

            _portrait = new TextureRect {
                Name = "Portrait",
                CustomMinimumSize = new Vector2(DialogueTheme.PortraitSize, DialogueTheme.PortraitSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            _portraitFrame.AddChild(_portrait);

            var content = new VBoxContainer {
                Name = "Content",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            layout.AddChild(content);

            var speakerRow = new HBoxContainer { Name = "SpeakerRow" };
            content.AddChild(speakerRow);

            _speakerLabel = new Label {
                Name = "SpeakerLabel",
                ThemeTypeVariation = UIPalette.HeadingLabelVariation
            };
            _speakerLabel.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            speakerRow.AddChild(_speakerLabel);

            // Body copy rides the theme's default font size (no override) so
            // the accessibility UI scale reaches the dialogue text.
            _textLabel = new RichTextLabel {
                Name = "TextLabel",
                BbcodeEnabled = true,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            content.AddChild(_textLabel);

            _continueHint = new Label {
                Name = "ContinueHint",
                HorizontalAlignment = HorizontalAlignment.Right,
                Visible = false,
                ThemeTypeVariation = UIPalette.SmallLabelVariation
            };
            _continueHint.AddThemeColorOverride("font_color", UIPalette.SlateDim);
            content.AddChild(_continueHint);
        }

        /// <summary>
        /// Adopts the shared theme, applies the glass variation, and installs the
        /// mutable portrait frame style. Idempotent; the authored scene already
        /// carries the theme and variation, the fallback does not.
        /// </summary>
        private void ApplyPresentationTreatment() {
            if (_dialoguePanel == null) return;
            UIPalette.ApplyTheme(_dialoguePanel);
            if (string.IsNullOrEmpty(_dialoguePanel.ThemeTypeVariation.ToString())) {
                _dialoguePanel.ThemeTypeVariation = DialogueTheme.GlassPanelVariation;
            }

            if (_portraitFrame != null) {
                _portraitFrameStyle = DialogueTheme.CreatePortraitFrameStyle();
                _portraitFrame.AddThemeStyleboxOverride("panel", _portraitFrameStyle);
            }
            ApplyEmotion(DialogueEmotionTreatment.Resolve(DialogueEmotion.Neutral), "");
            SetBoxAnimationProgress(0f);
        }

        // === Set registration and event-driven triggering ===

        public void RegisterSet(DialogueSetData set) {
            if (set != null && !_registeredSets.Contains(set)) _registeredSets.Add(set);
        }

        public bool RegisterSetFromPath(string resourcePath) {
            if (string.IsNullOrWhiteSpace(resourcePath) || !ResourceLoader.Exists(resourcePath)) return false;
            var set = FTT.Core.AuthoredResources.Load<DialogueSetData>(resourcePath);
            if (set == null) return false;
            RegisterSet(set);
            return true;
        }

        /// <summary>
        /// Resolves a dialogue ID against the registered sets, preferring the
        /// active campaign hero's authored variant (Package 11 A5, plan §2.10
        /// item 1). Every trigger path — <see cref="StartSequence(string)"/>,
        /// the EventBus trigger, a level controller — lands here, so a hero
        /// variant is picked up wherever the sequence is started from.
        /// </summary>
        public DialogueSequenceData FindSequence(string dialogueID) =>
            FindSequence(dialogueID, ActiveHeroID());

        /// <summary>Explicit-hero overload; the selection seam tests drive.</summary>
        public DialogueSequenceData FindSequence(string baseID, string activeHeroID) {
            foreach (DialogueSetData set in _registeredSets) {
                DialogueSequenceData sequence = set.FindSequence(baseID, activeHeroID);
                if (sequence != null) return sequence;
            }
            return null;
        }

        /// <summary>
        /// The saved campaign hero ID — never a localized name. Falls back to the
        /// session's selected character so a slotless developer launch still
        /// picks the right variant.
        /// </summary>
        private static string ActiveHeroID() {
            FTT.Core.StorySaveData save = ActiveStorySave();
            if (save != null && !string.IsNullOrWhiteSpace(save.SelectedCharacterID)) {
                return save.SelectedCharacterID;
            }
            return FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
        }

        private void OnDialogueTriggered(string dialogueID) {
            DialogueSequenceData sequence = FindSequence(dialogueID);
            if (sequence == null) {
                GD.PushWarning($"Dialogue sequence '{dialogueID}' is not registered in this scene.");
                return;
            }
            StartSequence(sequence);
        }

        // === Playback ===

        public bool StartSequence(string dialogueID) {
            DialogueSequenceData sequence = FindSequence(dialogueID);
            if (sequence == null) return false;
            StartSequence(sequence);
            return true;
        }

        public void StartSequence(DialogueSequenceData sequence) {
            if (sequence == null || sequence.LineCount == 0 || _isActive) return;
            _currentSequence = sequence;
            _currentIndex = 0;
            _isActive = true;
            Visible = true;
            // The pause is taken on the same frame the sequence starts, before the
            // box has finished animating in. Deferring it until the animation
            // completed would leave a ~0.18 s window in which gameplay still ran
            // under an opening dialogue box.
            if (sequence.PausesGameplay && GetTree() != null && !GetTree().Paused) {
                GetTree().Paused = true;
                _pausedGameplay = true;
            }
            // V7.6 (Package 11 A5): EVERY sequence is skippable. The V7.3
            // completed-campaign + already-seen gate is gone; the first skip of
            // a never-seen sequence opens one confirmation instead.
            _skipHoldSeconds = 0f;
            _skipEligible = true;
            RefreshSkipHint();
            BeginBoxAnimation(opening: true);
            ShowCurrentLine();
        }

        private void ShowCurrentLine() {
            if (_currentSequence == null || _currentIndex >= _currentSequence.LineCount) {
                EndSequence();
                return;
            }

            string speakerKey = _currentSequence.GetSpeakerKey(_currentIndex);
            _speakerLabel.Text = ResolveSpeakerName(speakerKey);
            ActiveChirpPitch = ResolveChirpPitch(speakerKey);

            string emotionKey = _currentSequence.GetEmotionKey(_currentIndex);
            ApplyEmotion(DialogueEmotionTreatment.Resolve(emotionKey), emotionKey);
            if (_portrait != null) _portrait.Texture = _currentSequence.GetPortrait(_currentIndex) ?? _defaultPortrait;

            _textLabel.Text = SubstituteCaptiveNames(Tr(_currentSequence.GetLineKey(_currentIndex)));
            // The pacing model reads punctuation, so it needs the text the player
            // will actually see - BBCode markup stripped.
            Reveal.Begin(_textLabel.GetParsedText());
            _textLabel.VisibleCharacters = Reveal.IsComplete ? -1 : 0;
            _autoAdvanceTimer = _currentSequence.AutoAdvanceDelay;
            if (_continueHint != null) {
                _continueHint.Text = BuildContinueHint();
                _continueHint.Visible = Reveal.IsComplete;
            }
        }

        /// <summary>
        /// Audit Low (UI): the hint used to be the hardcoded copy "Press [E] to
        /// continue" — wrong after a remap and for controller players. The key is
        /// now parameterized (<c>dialogue_continue,Press {0} to continue</c>, the
        /// save-notice key+args idiom) and the argument is the *actual* bound
        /// Interact event for the active device, read live from the InputMap via
        /// <see cref="FTT.Core.InputBindingService"/>. Rebuilt on every line show,
        /// so a mid-scene rebind or device change is picked up at the next line.
        /// </summary>
        private string BuildContinueHint() =>
            string.Format(Tr("dialogue_continue"), DescribeInteractBinding());

        /// <summary>The actual bound Interact event for the active device kind.</summary>
        private static string DescribeInteractBinding() {
            List<FTT.Core.InputBindingEvent> events =
                FTT.Core.InputBindingService.CaptureAction(FTT.Core.InputManager.Actions.Interact);
            bool joypad = FTT.Core.InputManager.Instance?.IsJoypadConnected(0) == true;
            FTT.Core.InputDeviceKind preferred = joypad
                ? FTT.Core.InputDeviceKind.Joypad
                : FTT.Core.InputDeviceKind.Keyboard;

            FTT.Core.InputBindingEvent match = null;
            foreach (FTT.Core.InputBindingEvent candidate in events) {
                if (candidate.DeviceKind == preferred) { match = candidate; break; }
            }
            if (match == null && events.Count > 0) match = events[0];

            return FTT.Core.InputBindingService.Describe(match);
        }

        /// <summary>Paints the portrait frame and tint for an emotion, and keeps the localized name as its tooltip.</summary>
        private void ApplyEmotion(DialogueEmotionTreatment.Treatment treatment, string emotionKey) {
            ActiveEmotion = treatment;
            if (_portraitFrameStyle != null) {
                _portraitFrameStyle.BorderColor = treatment.FrameColor;
                _portraitFrameStyle.SetBorderWidthAll(treatment.FrameWidth);
            }
            if (_portrait != null) {
                _portrait.SelfModulate = treatment.PortraitTint;
                // Keeps the emotion_* keys referenced and the information
                // reachable without colour.
                _portrait.TooltipText = string.IsNullOrWhiteSpace(emotionKey) ? "" : Tr(emotionKey);
            }
        }

        // === Package 11 A5: {CaptiveName1} / {CaptiveName2} (plan §2.10 item 2) ===

        /// <summary>First absent-legend token a Mystery Thread line may carry.</summary>
        public const string CaptiveNameToken1 = "{CaptiveName1}";

        /// <summary>Second absent-legend token.</summary>
        public const string CaptiveNameToken2 = "{CaptiveName2}";

        /// <summary>
        /// Substitutes the captive-name tokens against the manifest roster minus
        /// the active hero (<see cref="FTT.Core.CampaignCaptiveRoster"/>). Static
        /// and public so the resolution is provable without a box on screen.
        /// A line with no token is returned untouched.
        /// </summary>
        public static string SubstituteCaptiveNames(string text) {
            if (string.IsNullOrEmpty(text)) return text;
            bool hasFirst = text.Contains(CaptiveNameToken1);
            bool hasSecond = text.Contains(CaptiveNameToken2);
            if (!hasFirst && !hasSecond) return text;
            (string first, string second) =
                FTT.Core.CampaignCaptiveRoster.NamedExamplesFor(ActiveHeroID());
            if (hasFirst) text = text.Replace(CaptiveNameToken1, ResolveCaptiveName(first));
            if (hasSecond) text = text.Replace(CaptiveNameToken2, ResolveCaptiveName(second));
            return text;
        }

        /// <summary>
        /// A captive's spoken name. The <c>captive_name_*</c> family (A6 authors
        /// the rows) is distinct from <c>character_*_name</c> because a legend is
        /// spoken of differently than they are listed — "Da Vinci", not
        /// "Leonardo da Vinci". An unauthored key falls back to the display name
        /// rather than putting a raw key on screen.
        /// </summary>
        private static string ResolveCaptiveName(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return "";
            string key = FTT.Core.CampaignCaptiveRoster.SpokenNameKey(characterID);
            string spoken = TranslationServer.Translate(key).ToString();
            if (spoken != key) return spoken;
            string displayKey = $"character_{characterID}_name";
            string display = TranslationServer.Translate(displayKey).ToString();
            return display == displayKey ? characterID : display;
        }

        private string ResolveSpeakerName(string speakerKey) {
            if (speakerKey == PlayerSpeakerKey) {
                string characterID = FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID;
                if (!string.IsNullOrWhiteSpace(characterID)) return Tr($"character_{characterID}_name");
            }
            return Tr(speakerKey);
        }

        /// <summary>
        /// The player's locked campaign character chirps at its authored
        /// <see cref="FTT.Characters.CharacterData.DialogueChirpPitch"/>; Sarah,
        /// the bosses and narration all chirp neutral. Giving every named NPC its
        /// own pitch would mean inventing a second table of tuning numbers with
        /// no resource behind it, which is exactly what the repository's
        /// "resources own the numbers" rule exists to prevent.
        /// </summary>
        private static float ResolveChirpPitch(string speakerKey) {
            if (speakerKey != PlayerSpeakerKey) return NeutralChirpPitch;
            string characterID = FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID;
            if (string.IsNullOrWhiteSpace(characterID)) return NeutralChirpPitch;
            string path = $"res://resources/Characters/{characterID}_data.tres";
            if (!ResourceLoader.Exists(path)) return NeutralChirpPitch;
            var data = FTT.Core.AuthoredResources.Load<FTT.Characters.CharacterData>(path);
            return data != null && data.DialogueChirpPitch > 0f
                ? data.DialogueChirpPitch
                : NeutralChirpPitch;
        }

        public override void _Process(double delta) {
            AdvanceBoxAnimation((float)delta);
            if (!_isActive) return;
            // The reveal, the auto-advance timer and the hold all suspend while
            // the skip confirmation is up.
            if (IsSkipConfirmOpen) return;
            if (CanHoldToSkip) {
                bool held = Input.IsActionPressed(FTT.Core.InputManager.Actions.Interact)
                    || Input.IsActionPressed("ui_accept");
                if (held) AdvanceSkipHold((float)delta);
                else if (_skipHoldSeconds > 0f) ResetSkipHold();
                // The hold may have fast-forwarded the whole sequence.
                if (!_isActive) return;
            }
            if (IsTyping) {
                int chirps = Reveal.Advance((float)delta);
                for (int index = 0; index < chirps; index++) {
                    FTT.Core.AudioManager.Instance?.PlayChirp(ActiveChirpPitch);
                }
                _textLabel.VisibleCharacters = Reveal.VisibleCharacters;
                if (Reveal.IsComplete) CompleteLineReveal();
                return;
            }
            if (_currentSequence != null && _currentSequence.AutoAdvance) {
                _autoAdvanceTimer -= (float)delta;
                if (_autoAdvanceTimer <= 0f) AdvanceLine();
            }
        }

        private void CompleteLineReveal() {
            _textLabel.VisibleCharacters = -1;
            if (_continueHint != null) _continueHint.Visible = true;
        }

        public void AdvanceLine() {
            if (!_isActive) return;
            _currentIndex++;
            ShowCurrentLine();
        }

        private void EndSequence() {
            string completedID = _currentSequence?.DialogueID ?? "";
            _isActive = false;
            _currentSequence = null;
            _skipEligible = false;
            _skipHoldSeconds = 0f;
            if (_skipHintRow != null) _skipHintRow.Visible = false;
            CloseSkipConfirm();
            BeginBoxAnimation(opening: false);
            ReleaseGameplayPause();
            RecordLastViewedDialogue(completedID);
            // Package 11 A5 (plan §2.10 item 5): effect resolution is no longer
            // welded to "the player watched it to the end". Both the completion
            // path and the confirmed-skip path arrive here, so both apply the
            // same effects, exactly once per story slot.
            ResolveDialogueEffects(completedID);
            FTT.Core.EventBus.Instance?.RaiseDialogueComplete(completedID);
        }

        // === Package 11 A5: the idempotent gameplay-effect ledger ============

        /// <summary>
        /// Marks a sequence's gameplay effects as applied on the active story
        /// slot and returns whether this call is the one that applied them.
        /// Watched and skipped sequences both route here, so a skip can never
        /// cost the player a reward — and a replay can never pay it twice.
        ///
        /// <para>The per-slot <c>StorySaveData.ViewedDialogueIDs</c> list is the
        /// ledger (F10's "story slot effect IDs"); the global
        /// <c>GlobalSaveData.SeenDialogueIDs</c> set it also writes governs the
        /// skip confirmation alone.</para>
        /// </summary>
        public bool ResolveDialogueEffects(string sequenceID) {
            if (string.IsNullOrWhiteSpace(sequenceID)) return false;
            RecordSequenceSeenGlobally(sequenceID);
            FTT.Core.StorySaveData save = ActiveStorySave();
            if (save == null) return false;
            save.ViewedDialogueIDs ??= new List<string>();
            if (save.ViewedDialogueIDs.Contains(sequenceID)) return false;
            save.ViewedDialogueIDs.Add(sequenceID);
            return true;
        }

        /// <summary>True when this slot has already had the sequence's effects applied.</summary>
        public static bool HasResolvedDialogueEffects(string sequenceID) {
            FTT.Core.StorySaveData save = ActiveStorySave();
            return save?.ViewedDialogueIDs != null && save.ViewedDialogueIDs.Contains(sequenceID);
        }

        /// <summary>
        /// True when this sequence has been finished or skip-confirmed on ANY
        /// slot — the gate for the first-viewing confirmation.
        /// </summary>
        public static bool HasSeenSequenceGlobally(string sequenceID) {
            if (string.IsNullOrWhiteSpace(sequenceID)) return false;
            FTT.Core.GlobalSaveData global = FTT.Core.SaveManager.Instance?.GlobalData;
            return global?.SeenDialogueIDs != null && global.SeenDialogueIDs.Contains(sequenceID);
        }

        private static void RecordSequenceSeenGlobally(string sequenceID) {
            if (string.IsNullOrWhiteSpace(sequenceID)) return;
            FTT.Core.GlobalSaveData global = FTT.Core.SaveManager.Instance?.GlobalData;
            if (global == null) return;
            global.SeenDialogueIDs ??= new HashSet<string>(System.StringComparer.Ordinal);
            global.SeenDialogueIDs.Add(sequenceID);
        }

        /// <summary>
        /// Advances the skip hold. Called from <c>_Process</c> while the confirm
        /// action is held; public so tests can drive the 0.75 s window
        /// deterministically. Reaching the threshold either skips outright (a
        /// sequence seen on any slot) or opens the one-time confirmation.
        /// </summary>
        public void AdvanceSkipHold(float delta) {
            if (!CanHoldToSkip) return;
            _skipHoldSeconds += delta;
            if (_skipProgressBar != null) _skipProgressBar.Value = SkipHoldProgress;
            if (_skipHoldSeconds >= HoldToSkipSeconds) CompleteSkipHold();
        }

        /// <summary>
        /// The hold reached its threshold. A sequence already seen on any slot
        /// skips silently; a never-seen one asks once, because skipping a scene
        /// the player has not watched is the only irreversible part of this
        /// feature.
        /// </summary>
        private void CompleteSkipHold() {
            string sequenceID = _currentSequence?.DialogueID ?? "";
            if (HasSeenSequenceGlobally(sequenceID)) {
                EndSequence();
                return;
            }
            OpenSkipConfirm();
        }

        /// <summary>Shows the shared themed confirmation over the box.</summary>
        private void OpenSkipConfirm() {
            ResetSkipHold();
            if (_skipConfirm == null || !IsInstanceValid(_skipConfirm)) {
                _skipConfirm = ConfirmModal.Create(
                    "dialogue_skip_confirm_body",
                    "dialogue_skip_confirm_ok",
                    "dialogue_skip_confirm_cancel",
                    "dialogue_skip_confirm_title");
                _skipConfirm.Confirmed += OnSkipConfirmed;
                _skipConfirm.Cancelled += OnSkipCancelled;
                AddChild(_skipConfirm);
            }
            _skipConfirm.Open();
        }

        private void OnSkipConfirmed() {
            if (!_isActive) return;
            EndSequence();
        }

        private void OnSkipCancelled() => ResetSkipHold();

        private void CloseSkipConfirm() {
            if (_skipConfirm != null && IsInstanceValid(_skipConfirm)) _skipConfirm.Close();
        }

        /// <summary>Test seam: completes the hold without stepping 0.75 s of input.</summary>
        internal void ForceSkipHoldForTests() {
            if (!CanHoldToSkip) return;
            _skipHoldSeconds = HoldToSkipSeconds;
            CompleteSkipHold();
        }

        /// <summary>Test/UI seam: confirms an open skip confirmation.</summary>
        public void ConfirmSkip() => _skipConfirm?.Confirm();

        /// <summary>Test/UI seam: backs out of an open skip confirmation.</summary>
        public void CancelSkip() => _skipConfirm?.Cancel();

        /// <summary>Releasing the confirm action resets the fill.</summary>
        public void ResetSkipHold() {
            _skipHoldSeconds = 0f;
            if (_skipProgressBar != null) _skipProgressBar.Value = 0;
        }

        /// <summary>Shows/hides the "hold to skip" affordance for this sequence.</summary>
        private void RefreshSkipHint() {
            EnsureSkipHintUI();
            if (_skipHintRow == null) return;
            _skipHintRow.Visible = _skipEligible;
            if (_skipProgressBar != null) _skipProgressBar.Value = 0;
            if (_skipEligible && _skipHintLabel != null) {
                _skipHintLabel.Text = string.Format(Tr("dialogue_hold_skip"), DescribeInteractBinding());
            }
        }

        /// <summary>
        /// Builds the hint row (label + fill bar) lazily under the box's Content
        /// column. Code-built in both the authored and fallback trees so the two
        /// stay identical without a scene edit per variant.
        /// </summary>
        private void EnsureSkipHintUI() {
            if (_skipHintRow != null || _textLabel == null) return;
            if (_textLabel.GetParent() is not Control content) return;
            var row = new HBoxContainer {
                Name = "SkipHintRow",
                Visible = false,
                Alignment = BoxContainer.AlignmentMode.End
            };
            row.AddThemeConstantOverride("separation", 8);
            _skipHintLabel = new Label {
                Name = "SkipHintLabel",
                ThemeTypeVariation = UIPalette.SmallLabelVariation
            };
            _skipHintLabel.AddThemeColorOverride("font_color", UIPalette.SlateDim);
            row.AddChild(_skipHintLabel);
            _skipProgressBar = new ProgressBar {
                Name = "SkipHoldProgress",
                MinValue = 0,
                MaxValue = 1,
                Value = 0,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(120, 10),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            row.AddChild(_skipProgressBar);
            content.AddChild(row);
            _skipHintRow = row;
        }

        private static FTT.Core.StorySaveData ActiveStorySave() {
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            if (saveManager == null || gameManager == null) return null;
            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < saveManager.SaveSlots.Length ? saveManager.SaveSlots[slot] : null;
        }

        // === Box in/out animation ===

        /// <summary>
        /// Starts the slide-and-fade. Like A2's crossfades this is a pumped
        /// interpolation rather than a <see cref="Tween"/>: one advance point, no
        /// node churn per sequence, and a seam a test can step deterministically.
        /// </summary>
        private void BeginBoxAnimation(bool opening) {
            _boxOpening = opening;
            if (opening && BoxAnimationProgress <= 0f) SetBoxAnimationProgress(0f);
        }

        /// <summary>Advances the box animation. Public for deterministic stepping in tests.</summary>
        public void AdvanceBoxAnimation(float delta) {
            if (_dialoguePanel == null) return;
            float target = _boxOpening ? 1f : 0f;
            if (Mathf.IsEqualApprox(BoxAnimationProgress, target)) {
                // Closing settles exactly once, then the whole layer goes away.
                if (!_boxOpening && Visible && !_isActive) Visible = false;
                return;
            }
            float step = delta / Mathf.Max(DialogueTheme.BoxAnimationSeconds, 0.0001f);
            float progress = _boxOpening
                ? Mathf.Min(BoxAnimationProgress + step, 1f)
                : Mathf.Max(BoxAnimationProgress - step, 0f);
            SetBoxAnimationProgress(progress);
            if (!_boxOpening && BoxAnimationProgress <= 0f && !_isActive) Visible = false;
        }

        private void SetBoxAnimationProgress(float progress) {
            BoxAnimationProgress = Mathf.Clamp(progress, 0f, 1f);
            if (_dialoguePanel == null) return;
            // Ease-out on the way in so the box arrives rather than slams.
            float eased = 1f - (1f - BoxAnimationProgress) * (1f - BoxAnimationProgress);
            _dialoguePanel.Modulate = new Color(1f, 1f, 1f, BoxAnimationProgress);
            // The slide is applied to the CanvasLayer, not to the panel's
            // Position. The panel is anchored to the bottom of the viewport, and
            // writing Position on an anchored Control rewrites its offsets - the
            // layout then fights the animation on every resize notification.
            // CanvasLayer.Offset shifts the whole layer and touches no layout.
            Offset = new Vector2(Offset.X, DialogueTheme.BoxSlideDistance * (1f - eased));
        }

        private static void RecordLastViewedDialogue(string dialogueID) {
            if (string.IsNullOrWhiteSpace(dialogueID)) return;
            FTT.Core.StorySaveData save = ActiveStorySave();
            if (save == null) return;
            // Package 11 A5: demoted to a PRESENTATION CURSOR only. The
            // seen/effect bookkeeping moved to ResolveDialogueEffects, which
            // both the watched and the skipped path call — writing it from here
            // would have made "the player pressed through every line" the
            // condition for a reward again.
            save.LastViewedDialogueID = dialogueID;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (!_isActive) return;
            // The confirmation owns input while it is up: advancing or
            // completing a line underneath it would fight the focus trap.
            if (IsSkipConfirmOpen) return;
            bool confirm = @event.IsActionPressed(FTT.Core.InputManager.Actions.Interact)
                || @event.IsActionPressed(FTT.Core.InputManager.Actions.BasicAttack)
                || @event.IsActionPressed("ui_accept");
            if (!confirm) return;
            if (IsTyping) {
                Reveal.CompleteImmediately();
                _textLabel.VisibleCharacters = -1;
                CompleteLineReveal();
            } else {
                AdvanceLine();
            }
            GetViewport().SetInputAsHandled();
        }
    }
}
