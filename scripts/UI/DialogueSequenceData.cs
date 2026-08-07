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
