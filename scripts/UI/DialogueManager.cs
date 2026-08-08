using Godot;
using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// Production dialogue presentation: 30 characters/second typewriter reveal,
    /// confirm-to-complete then confirm-to-advance, placeholder portrait and
    /// emotion treatment, and optional gameplay suspension while the UI layer
    /// stays responsive. Instanced per campaign scene from DialogueBox.tscn.
    /// </summary>
    public partial class DialogueManager : CanvasLayer {
        public static DialogueManager Instance { get; private set; }

        public const string SceneResourcePath = "res://scenes/ui/DialogueBox.tscn";
        public const float CharactersPerSecond = 30f;
        private const string PlayerSpeakerKey = "speaker_player";
        private const string DefaultPortraitPath = "res://assets/placeholders/portrait.svg";

        public bool IsSequenceActive => _isActive;
        public string ActiveDialogueID => _currentSequence?.DialogueID ?? "";

        private PanelContainer _dialoguePanel;
        private TextureRect _portrait;
        private Label _speakerLabel;
        private Label _emotionLabel;
        private RichTextLabel _textLabel;
        private Label _continueHint;
        private Texture2D _defaultPortrait;

        private readonly List<DialogueSetData> _registeredSets = new();
        private DialogueSequenceData _currentSequence;
        private int _currentIndex;
        private bool _isActive;
        private bool _isTyping;
        private float _revealedCharacters;
        private int _lineCharacterTotal;
        private float _autoAdvanceTimer;
        private bool _pausedGameplay;

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
            _isTyping = false;
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
            _portrait = GetNodeOrNull<TextureRect>("Panel/Layout/Portrait");
            _speakerLabel = GetNodeOrNull<Label>("Panel/Layout/Content/SpeakerRow/SpeakerLabel");
            _emotionLabel = GetNodeOrNull<Label>("Panel/Layout/Content/SpeakerRow/EmotionLabel");
            _textLabel = GetNodeOrNull<RichTextLabel>("Panel/Layout/Content/TextLabel");
            _continueHint = GetNodeOrNull<Label>("Panel/Layout/Content/ContinueHint");
        }

        private void BuildFallbackUI() {
            _dialoguePanel = new PanelContainer { Name = "Panel" };
            _dialoguePanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _dialoguePanel.OffsetLeft = 100;
            _dialoguePanel.OffsetRight = -100;
            _dialoguePanel.OffsetTop = -240;
            _dialoguePanel.OffsetBottom = -40;
            AddChild(_dialoguePanel);

            var layout = new HBoxContainer { Name = "Layout" };
            _dialoguePanel.AddChild(layout);

            _portrait = new TextureRect {
                Name = "Portrait",
                CustomMinimumSize = new Vector2(160, 160),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            layout.AddChild(_portrait);

            var content = new VBoxContainer {
                Name = "Content",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            layout.AddChild(content);

            var speakerRow = new HBoxContainer { Name = "SpeakerRow" };
            content.AddChild(speakerRow);

            _speakerLabel = new Label { Name = "SpeakerLabel" };
            _speakerLabel.AddThemeFontSizeOverride("font_size", 22);
            _speakerLabel.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
            speakerRow.AddChild(_speakerLabel);

            _emotionLabel = new Label { Name = "EmotionLabel" };
            _emotionLabel.AddThemeFontSizeOverride("font_size", 14);
            _emotionLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
            speakerRow.AddChild(_emotionLabel);

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
            _continueHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.65f, 0.75f));
            content.AddChild(_continueHint);
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
            if (sequence.PausesGameplay && GetTree() != null && !GetTree().Paused) {
                GetTree().Paused = true;
                _pausedGameplay = true;
            }
            ShowCurrentLine();
        }

        private void ShowCurrentLine() {
            if (_currentSequence == null || _currentIndex >= _currentSequence.LineCount) {
                EndSequence();
                return;
            }

            _speakerLabel.Text = ResolveSpeakerName(_currentSequence.GetSpeakerKey(_currentIndex));
            string emotionKey = _currentSequence.GetEmotionKey(_currentIndex);
            if (_emotionLabel != null) {
                _emotionLabel.Visible = !string.IsNullOrWhiteSpace(emotionKey);
                _emotionLabel.Text = string.IsNullOrWhiteSpace(emotionKey) ? "" : $"({Tr(emotionKey)})";
            }
            if (_portrait != null) _portrait.Texture = _currentSequence.GetPortrait(_currentIndex) ?? _defaultPortrait;

            _textLabel.Text = Tr(_currentSequence.GetLineKey(_currentIndex));
            _lineCharacterTotal = _textLabel.GetTotalCharacterCount();
            _revealedCharacters = 0f;
            _textLabel.VisibleCharacters = 0;
            _isTyping = _lineCharacterTotal > 0;
            _autoAdvanceTimer = _currentSequence.AutoAdvanceDelay;
            if (_continueHint != null) {
                _continueHint.Text = Tr("dialogue_continue");
                _continueHint.Visible = !_isTyping;
            }
        }

        private string ResolveSpeakerName(string speakerKey) {
            if (speakerKey == PlayerSpeakerKey) {
                string characterID = FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID;
                if (!string.IsNullOrWhiteSpace(characterID)) return Tr($"character_{characterID}_name");
            }
            return Tr(speakerKey);
        }

        public override void _Process(double delta) {
            if (!_isActive) return;
            if (_isTyping) {
                _revealedCharacters += CharactersPerSecond * (float)delta;
                int visible = Mathf.Min(_lineCharacterTotal, Mathf.FloorToInt(_revealedCharacters));
                _textLabel.VisibleCharacters = visible;
                if (visible >= _lineCharacterTotal) CompleteLineReveal();
                return;
            }
            if (_currentSequence != null && _currentSequence.AutoAdvance) {
                _autoAdvanceTimer -= (float)delta;
                if (_autoAdvanceTimer <= 0f) AdvanceLine();
            }
        }

        private void CompleteLineReveal() {
            _isTyping = false;
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
            _isTyping = false;
            Visible = false;
            _currentSequence = null;
            ReleaseGameplayPause();
            RecordLastViewedDialogue(completedID);
            FTT.Core.EventBus.Instance?.RaiseDialogueComplete(completedID);
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
            if (_isTyping) {
                _revealedCharacters = _lineCharacterTotal;
                CompleteLineReveal();
            } else {
                AdvanceLine();
            }
            GetViewport().SetInputAsHandled();
        }
    }
}
