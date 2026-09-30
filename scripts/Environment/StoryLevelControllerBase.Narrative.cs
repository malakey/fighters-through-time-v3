using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Package 13 W4 (design §16, the Mystery Thread absence beats — S23/S26/S35/S37).
    ///
    /// <para>An absence beat is a 1–2 line sequence that plays where the era's
    /// missing figure should be standing: the banner with no one holding it
    /// (Level 2), the coat on the hook (3), Desmoulins' empty table (4), Pliny's
    /// galleys (6), the empty throne (8), the prompt-book that stops (10), the
    /// silent telegraph (11). When the saved hero IS that figure the set's
    /// <c>&lt;baseID&gt;@&lt;heroID&gt;</c> variant plays the N03 recognition
    /// line instead — <c>DialogueSetData.FindSequence</c> does the swap, so a level
    /// only authors where the beat fires.</para>
    ///
    /// <para>A level opts in by calling <see cref="BuildAbsenceTrigger"/> from its
    /// <c>BuildLevel</c>. The beat plays once per scene instance; a sequence that
    /// could not start (another one was on screen) is retried on the next
    /// crossing rather than lost.</para>
    ///
    /// <para>A separate partial so the shared base file stays untouched by this
    /// workstream beyond the variant-ID fix in <c>HandleDialogueComplete</c>.</para>
    /// </summary>
    public abstract partial class StoryLevelControllerBase {

        /// <summary>The absence sequence ID, e.g. "level_02.absence".</summary>
        public virtual string AbsenceDialogueID => $"{DialoguePrefix}.absence";

        /// <summary>True once the absence (or recognition) beat has started.</summary>
        public bool AbsenceBeatShown { get; private set; }

        /// <summary>The trigger built by <see cref="BuildAbsenceTrigger"/>, or null. Test seam.</summary>
        public Area2D AbsenceTrigger { get; private set; }

        /// <summary>Places the one-shot absence trigger at <paramref name="position"/>.</summary>
        protected Area2D BuildAbsenceTrigger(Vector2 position, Vector2? size = null) {
            AbsenceTrigger = BuildWaveTrigger("AbsenceTrigger", position, () => PlayAbsenceBeat(), size);
            return AbsenceTrigger;
        }

        /// <summary>
        /// Starts the absence beat if it has not played. Returns true when it started.
        /// Public so tests (and a level that wants a scripted cue) can drive it.
        /// </summary>
        public bool PlayAbsenceBeat() {
            if (AbsenceBeatShown) return false;
            if (!StartDialogue(AbsenceDialogueID)) return false;
            AbsenceBeatShown = true;
            return true;
        }
    }
}
