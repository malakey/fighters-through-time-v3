using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// The end-of-campaign chain (Package 5 A1; Package 13 W3 S39/S43). Level 15
    /// calls <see cref="Begin"/> after the Prime Anchor sealing:
    ///
    /// <para>ending dialogue (the release at the Founding, "We've got you. Come
    /// aboard — one last time") -> <b>Epilogue: the Time-Ship Send-off</b>
    /// (<see cref="SendOffDialogueID"/>: Okafor, Wren, Sarah, the hero's
    /// <c>{HeroFarewellLine}</c>, the step through the portal) -> active save
    /// <c>IsCompleted = true</c> (through
    /// <see cref="SaveManager.MarkCampaignCompleted()"/>) + credits roll ->
    /// <b>After-Credits: Homecoming</b> (<see cref="HomecomingDialogueID"/>, once
    /// per campaign, skippable) -> main menu.</para>
    ///
    /// <para><b>The flag is written when the credits BEGIN, not when they end</b>
    /// (Package 5 C1). The credits are the point of no return: the boss is dead,
    /// the anchor is sealed, the ending and the send-off have played, and the
    /// level's completion has already advanced and autosaved the campaign pointer.
    /// Quitting mid-roll, or during the Homecoming, cannot lose it.</para>
    ///
    /// <para>The send-off and the Homecoming are opt-in stages — a caller that
    /// passes neither gets the historical ending -> credits -> menu chain — and
    /// both need a <see cref="DialogueManager"/>, which is given the epilogue set
    /// (<see cref="EpilogueDialoguePath"/>) on start. <see cref="BeginGoHome"/> is
    /// the completed save's hub portal (S39): the send-off and the credits again,
    /// never the Homecoming a second time.</para>
    ///
    /// The save write goes through SaveManager rather than being poked from UI
    /// code, and it happens even if the player skips the credits. Set
    /// <see cref="ReturnToMainMenu"/> to false to keep the tree in place (tests,
    /// or a level that wants to run its own epilogue after the signal).
    /// </summary>
    public partial class CampaignCompletionSequence : Node {
        public const string MainMenuScenePath = "res://scenes/menus/MainMenu.tscn";

        /// <summary>Package 13 W3: the send-off and Homecoming sequences live in their own set.</summary>
        public const string EpilogueDialoguePath = "res://resources/Dialogue/epilogue_dialogue.tres";
        public const string SendOffSequenceID = "epilogue.sendoff";
        public const string HomecomingSequenceID = "epilogue.homecoming";

        /// <summary>Raised once, after the save write and before any scene change.</summary>
        [Signal] public delegate void SequenceFinishedEventHandler();

        /// <summary>Dialogue sequence to play before the credits; empty rolls credits straight away.</summary>
        public string EndingDialogueID { get; set; } = "";

        /// <summary>Package 13 W3 (S39): the send-off played after the ending; empty skips the stage.</summary>
        public string SendOffDialogueID { get; set; } = "";

        /// <summary>Package 13 W3 (S43): the after-credits scene; empty skips the stage.</summary>
        public string HomecomingDialogueID { get; set; } = "";

        /// <summary>The level's dialogue manager (from <c>StorySceneServices.Dialogue</c>).</summary>
        public DialogueManager Dialogue { get; set; }

        public bool ReturnToMainMenu { get; set; } = true;

        public bool IsEndingDialogueActive { get; private set; }
        public bool IsSendOffActive { get; private set; }
        public bool IsHomecomingActive { get; private set; }
        public bool CreditsRolling => Credits != null && IsInstanceValid(Credits) && !Credits.IsFinished;
        public CreditsController Credits { get; private set; }
        public bool IsFinished { get; private set; }
        public bool CampaignMarkedCompleted { get; private set; }

        /// <summary>True once the Homecoming played (or was skipped) in this chain. Test seam.</summary>
        public bool HomecomingPlayed { get; private set; }

        private bool _eventsBound;
        private CanvasLayer _backdrop;

        /// <summary>
        /// Starts the chain as a child of <paramref name="host"/> (normally the
        /// level controller). Returns the node so callers can await
        /// <see cref="SequenceFinished"/>.
        /// </summary>
        public static CampaignCompletionSequence Begin(
            Node host,
            DialogueManager dialogue = null,
            string endingDialogueID = "",
            bool returnToMainMenu = true,
            string sendOffDialogueID = "",
            string homecomingDialogueID = "") {
            if (host == null) return null;
            var sequence = new CampaignCompletionSequence {
                Name = "CampaignCompletionSequence",
                ProcessMode = ProcessModeEnum.Always,
                Dialogue = dialogue,
                EndingDialogueID = endingDialogueID ?? "",
                SendOffDialogueID = sendOffDialogueID ?? "",
                HomecomingDialogueID = homecomingDialogueID ?? "",
                ReturnToMainMenu = returnToMainMenu
            };
            host.AddChild(sequence);
            return sequence;
        }

        /// <summary>
        /// S39 "Go home": the completed save's portal replays the send-off and the
        /// credits, then returns to the main menu. No ending (the release already
        /// happened) and no second Homecoming.
        /// </summary>
        public static CampaignCompletionSequence BeginGoHome(
            Node host, DialogueManager dialogue, bool returnToMainMenu = true) =>
            Begin(host, dialogue, "", returnToMainMenu, SendOffSequenceID, "");

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            if (Dialogue != null && (!string.IsNullOrWhiteSpace(SendOffDialogueID)
                                     || !string.IsNullOrWhiteSpace(HomecomingDialogueID))) {
                if (Dialogue.FindSequence(SendOffSequenceID) == null) Dialogue.RegisterSetFromPath(EpilogueDialoguePath);
            }
            if (!string.IsNullOrWhiteSpace(EndingDialogueID) && Dialogue != null && EventBus.Instance != null) {
                BindEvents();
                IsEndingDialogueActive = Dialogue.StartSequence(EndingDialogueID);
                if (IsEndingDialogueActive) return;
            }
            StartSendOff();
        }

        public override void _ExitTree() => UnbindEvents();

        private void BindEvents() {
            if (_eventsBound || EventBus.Instance == null) return;
            EventBus.Instance.OnDialogueComplete += OnDialogueComplete;
            _eventsBound = true;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (EventBus.Instance != null) EventBus.Instance.OnDialogueComplete -= OnDialogueComplete;
        }

        private void OnDialogueComplete(string dialogueID) {
            if (IsEndingDialogueActive && dialogueID == EndingDialogueID) {
                IsEndingDialogueActive = false;
                StartSendOff();
            } else if (IsSendOffActive && dialogueID == SendOffDialogueID) {
                IsSendOffActive = false;
                RollCredits();
            } else if (IsHomecomingActive && dialogueID == HomecomingDialogueID) {
                IsHomecomingActive = false;
                Finish();
            }
        }

        /// <summary>
        /// S39: the Time-Ship send-off — the hero on the Bridge, mortal, the crew
        /// by the portal — over a placeholder still. Rolls credits straight away
        /// when there is no send-off stage or no dialogue stack.
        /// </summary>
        private void StartSendOff() {
            if (IsFinished) return;
            if (!string.IsNullOrWhiteSpace(SendOffDialogueID) && Dialogue != null && EventBus.Instance != null) {
                ShowBackdrop("epilogue_still_sendoff");
                BindEvents();
                IsSendOffActive = Dialogue.StartSequence(SendOffDialogueID);
                if (IsSendOffActive) return;
            }
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
            Credits.CreditsFinished += OnCreditsFinished;
            AddChild(Credits);
            return Credits;
        }

        private void OnCreditsFinished() {
            if (IsFinished) return;
            if (!TryStartHomecoming()) Finish();
        }

        /// <summary>
        /// S43: After-Credits: Homecoming — once per campaign (the slot's
        /// <see cref="StorySaveData.HomecomingSeen"/>), skippable by the ordinary
        /// hold-to-skip rule. The seen flag is written as it starts, so a quit
        /// during it never replays it.
        /// </summary>
        private bool TryStartHomecoming() {
            if (string.IsNullOrWhiteSpace(HomecomingDialogueID) || Dialogue == null || EventBus.Instance == null) {
                return false;
            }
            StorySaveData save = ActiveSave();
            if (save != null && save.HomecomingSeen) return false;
            if (Credits != null && IsInstanceValid(Credits)) Credits.Visible = false;
            ShowBackdrop("epilogue_still_homecoming");
            BindEvents();
            IsHomecomingActive = Dialogue.StartSequence(HomecomingDialogueID);
            if (!IsHomecomingActive) return false;
            HomecomingPlayed = true;
            MarkHomecomingSeen(save);
            return true;
        }

        private void MarkHomecomingSeen(StorySaveData save) {
            if (save == null || save.HomecomingSeen) return;
            save.HomecomingSeen = true;
            int slot = GameManager.Instance?.CurrentSession.ActiveSaveSlot ?? -1;
            if (slot >= 0) SaveManager.Instance?.SaveStorySlot(slot);
        }

        /// <summary>
        /// A placeholder for the illustrated stills (the Bridge send-off, the
        /// Meridian port): a dark full-screen plate under the dialogue box with a
        /// one-line caption. Production stills are Package 10.
        /// </summary>
        private void ShowBackdrop(string captionKey) {
            if (_backdrop == null || !IsInstanceValid(_backdrop)) {
                _backdrop = new CanvasLayer { Name = "EpilogueBackdrop", Layer = 85, ProcessMode = ProcessModeEnum.Always };
                var plate = new ColorRect {
                    Name = "Plate",
                    Color = UIPalette.NavyDeep,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                plate.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                _backdrop.AddChild(plate);
                var caption = new Label {
                    Name = "Caption",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Position = new Vector2(0, 120),
                    CustomMinimumSize = new Vector2(1920, 40),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                caption.ThemeTypeVariation = UIPalette.HeadingLabelVariation;
                caption.AddThemeColorOverride("font_color", UIPalette.Gold);
                _backdrop.AddChild(caption);
                AddChild(_backdrop);
            }
            if (_backdrop.GetNodeOrNull<Label>("Caption") is Label shownCaption) shownCaption.Text = captionKey;
            _backdrop.Visible = true;
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
            if (IsHomecomingActive) {
                IsHomecomingActive = false;
                Finish();
                return;
            }
            if (Credits != null && IsInstanceValid(Credits) && !Credits.IsFinished) Credits.Skip();
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

        private static StorySaveData ActiveSave() {
            if (GameManager.Instance == null || SaveManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length ? SaveManager.Instance.SaveSlots[slot] : null;
        }
    }
}
