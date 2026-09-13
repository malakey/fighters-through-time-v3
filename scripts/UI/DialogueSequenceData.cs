using Godot;

namespace FTT.UI {

    /// <summary>
    /// One authored dialogue sequence. All visible copy is stored as translation
    /// keys; the speaker key "speaker_player" is resolved at runtime to the
    /// selected campaign character's localized name.
    /// </summary>
    [GlobalClass]
    public partial class DialogueSequenceData : Resource {
        [Export] public string DialogueID = "";
        [Export] public string[] SpeakerNameKeys = System.Array.Empty<string>();
        [Export] public string[] LineKeys = System.Array.Empty<string>();
        [Export] public string[] EmotionKeys = System.Array.Empty<string>();
        [Export] public Texture2D[] SpeakerPortraits = System.Array.Empty<Texture2D>();
        [Export] public bool PausesGameplay = true;
        [Export] public bool AutoAdvance;
        [Export] public float AutoAdvanceDelay = 2.0f;

        // === Package 11 A5 (V7.5 N03 hero-conditional variants, plan §2.10) ==

        /// <summary>
        /// Non-empty on a hero-conditional variant: the campaign character ID
        /// this sequence is written for. A variant's <see cref="DialogueID"/> is
        /// <c>&lt;baseID&gt;@&lt;heroID&gt;</c> (e.g. <c>level_01.entrance@leonardo</c>)
        /// so the two IDs are <b>distinct strings</b> — that is what stops F10's
        /// seen/skip bookkeeping from substituting one variant for another
        /// across slots. Empty means the default sequence every other hero gets.
        ///
        /// <para>Selection compares the <i>saved campaign hero ID</i> against the
        /// level's authored central-legend ID — never a localized name.</para>
        /// </summary>
        [Export] public string HeroConditionCharacterID = "";

        /// <summary>The variant separator. Never appears in a base dialogue ID.</summary>
        public const char VariantSeparator = '@';

        /// <summary>Builds the canonical variant ID for a base sequence and a hero.</summary>
        public static string VariantID(string baseID, string heroID) =>
            string.IsNullOrWhiteSpace(heroID) ? baseID : $"{baseID}{VariantSeparator}{heroID}";

        /// <summary>True when <paramref name="dialogueID"/> already names a variant.</summary>
        public static bool IsVariantID(string dialogueID) =>
            !string.IsNullOrEmpty(dialogueID) && dialogueID.IndexOf(VariantSeparator) > 0;

        /// <summary>The base ID of any dialogue ID, variant or not.</summary>
        public static string BaseIDOf(string dialogueID) {
            if (string.IsNullOrEmpty(dialogueID)) return "";
            int separator = dialogueID.IndexOf(VariantSeparator);
            return separator > 0 ? dialogueID[..separator] : dialogueID;
        }

        /// <summary>This sequence's base ID — its own ID with any variant suffix removed.</summary>
        public string BaseDialogueID => BaseIDOf(DialogueID);

        public int LineCount => Mathf.Min(
            SpeakerNameKeys?.Length ?? 0,
            LineKeys?.Length ?? 0);

        public string GetSpeakerKey(int index) =>
            SpeakerNameKeys != null && index >= 0 && index < SpeakerNameKeys.Length ? SpeakerNameKeys[index] : "";

        public string GetLineKey(int index) =>
            LineKeys != null && index >= 0 && index < LineKeys.Length ? LineKeys[index] : "";

        public string GetEmotionKey(int index) =>
            EmotionKeys != null && index >= 0 && index < EmotionKeys.Length ? EmotionKeys[index] : "";

        public Texture2D GetPortrait(int index) =>
            SpeakerPortraits != null && index >= 0 && index < SpeakerPortraits.Length ? SpeakerPortraits[index] : null;
    }
}
