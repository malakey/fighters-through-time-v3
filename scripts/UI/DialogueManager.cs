using Godot;
using System;

namespace FTT.UI {

    public struct DialogueEntry {
        public string SpeakerName;
        public Texture2D SpeakerPortrait;
        public string DialogueText;
    }

    [GlobalClass]
    public partial class DialogueSequenceData : Resource {
        [Export] public string DialogueID = "";
        [Export] public string[] SpeakerNames;
        [Export(PropertyHint.MultilineText)] public string[] DialogueTexts;
        [Export] public bool PausesGameplay = true;
        [Export] public bool AutoAdvance;
        [Export] public float AutoAdvanceDelay = 2.0f;
    }

    public partial class DialogueManager : CanvasLayer {
        public static DialogueManager Instance { get; private set; }

        private PanelContainer _dialoguePanel;
        private Label _speakerLabel;
        private RichTextLabel _textLabel;
        private DialogueSequenceData _currentSequence;
        private int _currentIndex;
        private bool _isActive;

        public override void _Ready() {
            Instance = this;
            Visible = false;
            SetupUI();
            FTT.Core.EventBus.Instance.OnDialogueTriggered += OnDialogueTriggered;
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null)
                FTT.Core.EventBus.Instance.OnDialogueTriggered -= OnDialogueTriggered;
        }

        private void SetupUI() {
            Layer = 90;
            _dialoguePanel = new PanelContainer();
            _dialoguePanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _dialoguePanel.OffsetTop = -200;
            AddChild(_dialoguePanel);

            var vbox = new VBoxContainer();
            _dialoguePanel.AddChild(vbox);

            _speakerLabel = new Label();
            _speakerLabel.AddThemeColorOverride("font_color", new Color(0, 0.9f, 0.9f));
            vbox.AddChild(_speakerLabel);

            _textLabel = new RichTextLabel();
            _textLabel.CustomMinimumSize = new Vector2(0, 120);
            _textLabel.BbcodeEnabled = true;
            vbox.AddChild(_textLabel);
        }

        private void OnDialogueTriggered(string dialogueID) {
            // Would load the dialogue sequence and start playback
        }

        public void StartSequence(DialogueSequenceData sequence) {
            _currentSequence = sequence;
            _currentIndex = 0;
            _isActive = true;
            Visible = true;
            ShowCurrentLine();
            if (sequence.PausesGameplay) GetTree().Paused = true;
        }

        private void ShowCurrentLine() {
            if (_currentSequence == null) return;
            if (_currentIndex >= (_currentSequence.SpeakerNames?.Length ?? 0)) {
                EndSequence();
                return;
            }
            _speakerLabel.Text = _currentSequence.SpeakerNames[_currentIndex];
            _textLabel.Text = _currentSequence.DialogueTexts?[_currentIndex] ?? "";
        }

        public void AdvanceLine() {
            if (!_isActive) return;
            _currentIndex++;
            ShowCurrentLine();
        }

        private void EndSequence() {
            _isActive = false;
            Visible = false;
            GetTree().Paused = false;
            FTT.Core.EventBus.Instance?.RaiseDialogueComplete(_currentSequence?.DialogueID ?? "");
            _currentSequence = null;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (!_isActive) return;
            if (@event.IsActionPressed(FTT.Core.InputManager.Actions.Interact) || @event.IsActionPressed(FTT.Core.InputManager.Actions.BasicAttack)) {
                AdvanceLine();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
