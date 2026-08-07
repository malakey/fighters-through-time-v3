using Godot;

namespace FTT.UI {

    /// <summary>
    /// A level- or hub-scoped collection of dialogue sequences. Each campaign
    /// scene registers one set with the <see cref="DialogueManager"/> so event
    /// triggers can start sequences by stable dialogue ID.
    /// </summary>
    [GlobalClass]
    public partial class DialogueSetData : Resource {
        [Export] public string DialogueSetID = "";
        [Export] public DialogueSequenceData[] Sequences = System.Array.Empty<DialogueSequenceData>();

        public DialogueSequenceData Find(string dialogueID) {
            if (Sequences == null || string.IsNullOrWhiteSpace(dialogueID)) return null;
            foreach (DialogueSequenceData sequence in Sequences) {
                if (sequence != null && sequence.DialogueID == dialogueID) return sequence;
            }
            return null;
        }
    }
}
