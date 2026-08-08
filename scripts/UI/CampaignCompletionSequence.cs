using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// The end-of-campaign chain (Package 5 A1). Level 15 calls
    /// <see cref="Begin"/> after the Temporal Core restoration beat:
    ///
    /// <para>ending dialogue -> active save <c>IsCompleted = true</c> (through
    /// <see cref="SaveManager.MarkCampaignCompleted()"/>) + credits roll
    /// -> main menu.</para>
    ///
    /// <para><b>The flag is written when the credits BEGIN, not when they end</b>
    /// (Package 5 C1, closing the window Level 15 flagged). The credits are the
    /// point of no return: the boss is dead, the Temporal Core is in the anchor,
    /// the ending beat has played, and the level's completion has already advanced
    /// and autosaved the campaign pointer. Writing at the end left a player who
    /// quit mid-credits with a save advanced past the finale that still reported
    /// the campaign unfinished.</para>
    ///
    /// The save write goes through SaveManager rather than being poked from UI
    /// code, and it happens even if the player skips the credits. Set
    /// <see cref="ReturnToMainMenu"/> to false to keep the tree in place (tests,
    /// or a level that wants to run its own epilogue after the signal).
    /// </summary>
    public partial class CampaignCompletionSequence : Node {
        public const string MainMenuScenePath = "res://scenes/menus/MainMenu.tscn";

        /// <summary>Raised once, after the save write and before any scene change.</summary>
        [Signal] public delegate void SequenceFinishedEventHandler();

        /// <summary>Dialogue sequence to play before the credits; empty rolls credits straight away.</summary>
        public string EndingDialogueID { get; set; } = "";

        /// <summary>The level's dialogue manager (from <c>StorySceneServices.Dialogue</c>).</summary>
        public DialogueManager Dialogue { get; set; }

        public bool ReturnToMainMenu { get; set; } = true;

        public bool IsEndingDialogueActive { get; private set; }
        public bool CreditsRolling => Credits != null && IsInstanceValid(Credits) && !Credits.IsFinished;
        public CreditsController Credits { get; private set; }
        public bool IsFinished { get; private set; }
        public bool CampaignMarkedCompleted { get; private set; }

        private bool _eventsBound;

        /// <summary>
        /// Starts the chain as a child of <paramref name="host"/> (normally the
        /// level controller). Returns the node so callers can await
        /// <see cref="SequenceFinished"/>.
        /// </summary>
        public static CampaignCompletionSequence Begin(
            Node host,
            DialogueManager dialogue = null,
            string endingDialogueID = "",
            bool returnToMainMenu = true) {
            if (host == null) return null;
            var sequence = new CampaignCompletionSequence {
                Name = "CampaignCompletionSequence",
                ProcessMode = ProcessModeEnum.Always,
                Dialogue = dialogue,
                EndingDialogueID = endingDialogueID ?? "",
                ReturnToMainMenu = returnToMainMenu
            };
            host.AddChild(sequence);
            return sequence;
        }

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            if (!string.IsNullOrWhiteSpace(EndingDialogueID) && Dialogue != null && EventBus.Instance != null) {
                EventBus.Instance.OnDialogueComplete += OnDialogueComplete;
                _eventsBound = true;
                IsEndingDialogueActive = Dialogue.StartSequence(EndingDialogueID);
                if (IsEndingDialogueActive) return;
                UnbindEvents();
            }
            RollCredits();
        }

        public override void _ExitTree() => UnbindEvents();

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (EventBus.Instance != null) EventBus.Instance.OnDialogueComplete -= OnDialogueComplete;
        }

        private void OnDialogueComplete(string dialogueID) {
            if (dialogueID != EndingDialogueID) return;
            IsEndingDialogueActive = false;
            UnbindEvents();
            RollCredits();
        }

        /// <summary>
        /// Adds the credits overlay and finishes the chain when it ends. The
        /// completion flag is written <i>here</i>, before the roll starts, so
        /// quitting during the credits cannot lose it.
        /// </summary>
        public CreditsController RollCredits() {
            if (IsFinished || Credits != null) return Credits;
            MarkCampaignCompleted();
            Credits = CreditsController.CreateDefault();
            Credits.CreditsFinished += Finish;
            AddChild(Credits);
            return Credits;
        }

        /// <summary>
        /// Writes <c>IsCompleted</c> through SaveManager once. A write that fails
        /// (no active slot yet) is retried from <see cref="Finish"/> rather than
        /// being latched off, so the terminal path still has its chance.
        /// </summary>
        private void MarkCampaignCompleted() {
            if (CampaignMarkedCompleted) return;
            CampaignMarkedCompleted = SaveManager.Instance?.MarkCampaignCompleted() == true;
        }

        /// <summary>Skips whatever is on screen and completes the chain.</summary>
        public void SkipToEnd() {
            if (IsFinished) return;
            if (Credits != null && IsInstanceValid(Credits)) Credits.Skip();
            else Finish();
        }

        private void Finish() {
            if (IsFinished) return;
            IsFinished = true;
            UnbindEvents();
            // Normally already written at RollCredits(); this covers the credits-less
            // skip path (SkipToEnd while the ending dialogue is still up) and retries
            // a write that could not find an active slot earlier.
            MarkCampaignCompleted();
            EmitSignal(SignalName.SequenceFinished);
            if (ReturnToMainMenu) GameManager.Instance?.LoadScene(MainMenuScenePath);
        }
    }
}
