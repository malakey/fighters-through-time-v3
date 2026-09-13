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

        /// <summary>
        /// Package 11 (V7.5 Mystery Thread, N03): the campaign character ID this
        /// sequence is authored for. Empty means the default sequence that plays for
        /// every hero. A variant's <see cref="DialogueID"/> is
        /// <c>&lt;baseID&gt;@&lt;heroID&gt;</c> - for example
        /// <c>level_01.entrance@leonardo</c> - and selection is from the saved
        /// campaign hero ID against this field, never from a localized name.
        /// Variant IDs are deliberately distinct strings so the seen/skip rules
        /// cannot substitute one branch for the other across save slots.
        /// <para>A6 authors the content against this field; A5 owns the lookup
        /// (<c>DialogueSetData.FindSequence</c>) and the manager routing.</para>
        /// </summary>
        [Export] public string HeroConditionCharacterID = "";

        [Export] public string[] SpeakerNameKeys = System.Array.Empty<string>();
        [Export] public string[] LineKeys = System.Array.Empty<string>();
        [Export] public string[] EmotionKeys = System.Array.Empty<string>();
        [Export] public Texture2D[] SpeakerPortraits = System.Array.Empty<Texture2D>();
        [Export] public bool PausesGameplay = true;
        [Export] public bool AutoAdvance;
        [Export] public float AutoAdvanceDelay = 2.0f;

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
