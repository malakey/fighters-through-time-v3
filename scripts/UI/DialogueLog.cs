using System.Collections.Generic;

namespace FTT.UI {

    /// <summary>
    /// Package 12 W6 (G10, design Section 7 "Dialogue Log"). The last
    /// <see cref="Capacity"/> dialogue lines of the <b>current session</b> —
    /// speaker, portrait and text — so a line that went by too fast, or a scene
    /// that was skipped, can still be read.
    ///
    /// <para><b>Session-only and never saved.</b> This is a static in-memory ring
    /// with no engine dependency and no persistence hook: nothing in the save
    /// payloads references it, so it cannot leak into a save by accident. It is
    /// presentation-only — reading the log changes nothing in the game.</para>
    ///
    /// <para><b>Fed by both paths.</b> <see cref="DialogueManager"/> appends each
    /// line as it is shown, and on a confirmed skip appends every line the player
    /// never saw — both through <c>EndSequence</c>, the one place the completion
    /// path and the confirmed-skip path already meet.</para>
    /// </summary>
    public static class DialogueLog {
        /// <summary>Lines kept (design: "the last 100 dialogue lines").</summary>
        public const int Capacity = 100;

        /// <summary>One logged line. Strings are resolved at log time (the player's locale then).</summary>
        public readonly record struct Entry(string SequenceID, string Speaker, string Text, string PortraitPath, bool FromSkip);

        private static readonly LinkedList<Entry> Lines = new();

        /// <summary>Oldest first.</summary>
        public static IReadOnlyCollection<Entry> Entries => Lines;

        public static int Count => Lines.Count;

        /// <summary>Raised after every append so an open log view can repaint.</summary>
        public static event System.Action Changed;

        public static void Append(Entry entry) {
            Lines.AddLast(entry);
            while (Lines.Count > Capacity) Lines.RemoveFirst();
            Changed?.Invoke();
        }

        /// <summary>Empties the log. Tests only — a real session never clears it.</summary>
        public static void Clear() {
            Lines.Clear();
            Changed?.Invoke();
        }
    }
}
