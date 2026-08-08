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

            _speakerLabel = new Label { Name = "SpeakerLabel" };
            _speakerLabel.AddThemeFontSizeOverride("font_size", UIPalette.HeadingFontSize);
            _speakerLabel.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            speakerRow.AddChild(_speakerLabel);

            _textLabel = new RichTextLabel {
                Name = "TextLabel",
                BbcodeEnabled = true,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _textLabel.AddThemeFontSizeOverride("normal_font_size", 20);
            content.AddChild(_textLabel);

            _continueHint = new Label {
                Name = "ContinueHint",
                HorizontalAlignment = HorizontalAlignment.Right,
                Visible = false
            };
            _continueHint.AddThemeFontSizeOverride("font_size", 12);
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

        public DialogueSequenceData FindSequence(string dialogueID) {
            foreach (DialogueSetData set in _registeredSets) {
                DialogueSequenceData sequence = set.Find(dialogueID);
                if (sequence != null) return sequence;
            }
            return null;
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

            _textLabel.Text = Tr(_currentSequence.GetLineKey(_currentIndex));
            // The pacing model reads punctuation, so it needs the text the player
            // will actually see - BBCode markup stripped.
            Reveal.Begin(_textLabel.GetParsedText());
            _textLabel.VisibleCharacters = Reveal.IsComplete ? -1 : 0;
            _autoAdvanceTimer = _currentSequence.AutoAdvanceDelay;
            if (_continueHint != null) {
                _continueHint.Text = Tr("dialogue_continue");
                _continueHint.Visible = Reveal.IsComplete;
            }
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
            BeginBoxAnimation(opening: false);
            ReleaseGameplayPause();
            RecordLastViewedDialogue(completedID);
            FTT.Core.EventBus.Instance?.RaiseDialogueComplete(completedID);
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
            var saveManager = FTT.Core.SaveManager.Instance;
            var gameManager = FTT.Core.GameManager.Instance;
            if (saveManager == null || gameManager == null) return;
            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= saveManager.SaveSlots.Length) return;
            var save = saveManager.SaveSlots[slot];
            if (save != null) save.LastViewedDialogueID = dialogueID;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (!_isActive) return;
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
