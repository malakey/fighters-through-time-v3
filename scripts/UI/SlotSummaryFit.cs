using System;

namespace FTT.UI {

    /// <summary>
    /// TG-CLOSE polish. How a main-menu save slot's summary line ("Slot 2: Albert Einstein — LEVEL 2: THE SIEGE OF ORLÉANS —
    /// ORLÉANS 1429") reads whole in its slab whatever the level title and the hero's name: on one line at its role size
    /// when it fits; else shrunk on one line toward the 18 px floor; else, when the slab holds a second line, wrapped onto
    /// two lines at its role size; else with the level named by its short title (the text before its dash,
    /// <see cref="PulpScreenKit.ShortTitle"/>), shrunk as far as the floor if need be. A line still too long at the floor
    /// after all that keeps the floor size and its ellipsis (the last resort; no shipped title and hero reach it). The
    /// sixteen level titles keep their values: their four forms are a question for the user. Plain C#: the caller measures.
    /// </summary>
    public static class SlotSummaryFit {

        /// <summary>The text to show, its font size, and whether it wraps onto a second line.</summary>
        public readonly record struct Choice(string Text, int Size, bool Wrap);

        /// <summary>
        /// Picks how <paramref name="full"/> (or, failing it, <paramref name="shortForm"/>) shows in a line
        /// <paramref name="width"/> wide. <paramref name="measure"/> gives a text's one-line width at a size;
        /// <paramref name="linesAtRole"/> how many lines a text wraps to at the role size in that width;
        /// <paramref name="slabHoldsSecondLine"/> whether the slab has room for one more line at the role size.
        /// </summary>
        public static Choice Choose(string full, string shortForm, int roleSize, int floorSize, float width,
            Func<string, int, float> measure, Func<string, int> linesAtRole, bool slabHoldsSecondLine) {
            full ??= "";
            shortForm = string.IsNullOrEmpty(shortForm) ? full : shortForm;
            int floor = Math.Min(floorSize, roleSize);
            int size = LargestFit(full, roleSize, floor, width, measure);
            if (size > 0) return new Choice(full, size, false);
            if (slabHoldsSecondLine && linesAtRole(full) <= 2) return new Choice(full, roleSize, true);
            size = LargestFit(shortForm, roleSize, floor, width, measure);
            return size > 0 ? new Choice(shortForm, size, false) : new Choice(shortForm, floor, false);
        }

        /// <summary>The largest whole size from <paramref name="roleSize"/> down to <paramref name="floor"/> at which <paramref name="text"/> fits on one line, or 0.</summary>
        public static int LargestFit(string text, int roleSize, int floor, float width, Func<string, int, float> measure) {
            for (int size = roleSize; size >= floor && size > 0; size--) {
                if (measure(text, size) <= width) return size;
            }
            return 0;
        }
    }
}
