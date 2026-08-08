using Godot;

namespace FTT.UI {

    /// <summary>The five authored dialogue emotions (the <c>emotion_*</c> key family).</summary>
    public enum DialogueEmotion {
        Neutral,
        Determined,
        Shocked,
        Confused,
        Injured
    }

    /// <summary>
    /// Package 8 B3. Maps an authored <c>emotion_*</c> key to a portrait
    /// treatment.
    ///
    /// <para>Before this, emotion was rendered as the literal string
    /// "<c>(Determined)</c>" in a label beside the speaker name — stage
    /// direction printed on the page. The emotion now colours the portrait
    /// itself: a frame in the emotion's accent and a tint over the portrait
    /// texture, both drawn from the A1 <see cref="UIPalette"/> so the dialogue
    /// box cannot drift from the rest of the UI.</para>
    ///
    /// <para>The localized emotion string is not thrown away — it becomes the
    /// portrait's tooltip, which keeps the <c>emotion_*</c> keys referenced for
    /// C1's unused-key sweep and leaves the information reachable for a player
    /// who cannot read colour.</para>
    ///
    /// <para>Tints stay close to white on purpose. The portrait is the
    /// character's face; a heavy wash would recolour their skin rather than
    /// suggest a mood, so the tint carries about 10-20% of the accent and the
    /// frame carries the rest of the signal.</para>
    /// </summary>
    public static class DialogueEmotionTreatment {

        /// <summary>One resolved treatment: what to paint on the portrait and its frame.</summary>
        public readonly record struct Treatment(
            DialogueEmotion Emotion,
            Color FrameColor,
            Color PortraitTint,
            int FrameWidth);

        /// <summary>Frame width for the resting emotion.</summary>
        public const int CalmFrameWidth = 2;

        /// <summary>Frame width for a heightened emotion (shocked, injured).</summary>
        public const int UrgentFrameWidth = 3;

        /// <summary>Parses an authored key such as <c>emotion_determined</c>.</summary>
        public static DialogueEmotion Parse(string emotionKey) {
            if (string.IsNullOrWhiteSpace(emotionKey)) return DialogueEmotion.Neutral;
            return emotionKey.Trim().ToLowerInvariant() switch {
                "emotion_determined" => DialogueEmotion.Determined,
                "emotion_shocked" => DialogueEmotion.Shocked,
                "emotion_confused" => DialogueEmotion.Confused,
                "emotion_injured" => DialogueEmotion.Injured,
                // Includes "emotion_neutral" and anything an unshipped set invents:
                // an unknown emotion must present as calm, never as blank.
                _ => DialogueEmotion.Neutral
            };
        }

        /// <summary>The treatment for an authored key. Unknown keys resolve to Neutral.</summary>
        public static Treatment Resolve(string emotionKey) => Resolve(Parse(emotionKey));

        /// <summary>The treatment for a parsed emotion.</summary>
        public static Treatment Resolve(DialogueEmotion emotion) => emotion switch {
            DialogueEmotion.Determined => new Treatment(
                emotion, UIPalette.Gold, new Color(1f, 0.97f, 0.88f), CalmFrameWidth),
            DialogueEmotion.Shocked => new Treatment(
                emotion, UIPalette.Cyan, new Color(0.9f, 1f, 1f), UrgentFrameWidth),
            DialogueEmotion.Confused => new Treatment(
                emotion, UIPalette.SlateDim, new Color(0.86f, 0.88f, 0.94f), CalmFrameWidth),
            DialogueEmotion.Injured => new Treatment(
                emotion, UIPalette.BossRed, new Color(1f, 0.82f, 0.82f), UrgentFrameWidth),
            _ => new Treatment(
                DialogueEmotion.Neutral, UIPalette.CyanDim, Colors.White, CalmFrameWidth)
        };
    }
}
