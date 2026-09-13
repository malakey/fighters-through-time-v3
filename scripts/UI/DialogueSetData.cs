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

        /// <summary>
        /// Package 11 A5 (plan §2.10 item 1). Hero-conditional lookup: prefers
        /// the <c>&lt;baseID&gt;@&lt;heroID&gt;</c> variant when this set carries one for
        /// the active campaign hero, and falls back to the base sequence
        /// otherwise. It never falls back to <i>another</i> hero's variant — a
        /// variant is written for one legend and reads wrong for anyone else.
        ///
        /// <para>Passing a full variant ID resolves it exactly, so a caller that
        /// already knows which variant it wants (a test, a scripted beat) is not
        /// re-routed by the active session.</para>
        /// </summary>
        public DialogueSequenceData FindSequence(string baseID, string activeHeroID) {
            if (Sequences == null || string.IsNullOrWhiteSpace(baseID)) return null;
            if (DialogueSequenceData.IsVariantID(baseID)) return Find(baseID);

            if (!string.IsNullOrWhiteSpace(activeHeroID)) {
                string variantID = DialogueSequenceData.VariantID(baseID, activeHeroID);
                foreach (DialogueSequenceData sequence in Sequences) {
                    if (sequence == null) continue;
                    if (sequence.DialogueID == variantID) return sequence;
                    if (sequence.BaseDialogueID == baseID
                        && sequence.HeroConditionCharacterID == activeHeroID) return sequence;
                }
            }

            foreach (DialogueSequenceData sequence in Sequences) {
                if (sequence == null) continue;
                if (sequence.DialogueID != baseID) continue;
                if (!string.IsNullOrWhiteSpace(sequence.HeroConditionCharacterID)) continue;
                return sequence;
            }
            return Find(baseID);
        }
    }
}
