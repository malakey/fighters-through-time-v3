using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>Which arrow/stick axis walks the chain.</summary>
    public enum FocusChainAxis {
        Vertical,
        Horizontal
    }

    /// <summary>
    /// Package 8 A1. Authors <c>focus_neighbor_*</c> links and initial focus for a
    /// container tree.
    ///
    /// Before this existed the repository authored no focus neighbours at all and
    /// held exactly two <c>GrabFocus()</c> calls, so a controller or keyboard
    /// player could not reliably move through any menu — Godot's automatic
    /// geometric focus resolution is a fallback, not a design, and it breaks the
    /// moment a container nests or a control is hidden.
    ///
    /// The builder is deliberately a utility over live nodes rather than a scene
    /// convention: focus order has to be rebuilt whenever a surface shows or hides
    /// options (a pause menu that gains an exit confirmation, a settings tab that
    /// swaps content), and only the owning script knows when that happened.
    /// </summary>
    public static class FocusChainBuilder {

        /// <summary>
        /// Controls in this group are skipped by <see cref="Collect"/>. Use it for
        /// focusable widgets that must not join the chain — a decorative button, or
        /// one a screen drives programmatically.
        /// </summary>
        public const string SkipGroup = "focus_skip";

        /// <summary>
        /// Collects the focusable controls under <paramref name="root"/> in
        /// depth-first child order, skipping hidden subtrees and
        /// <see cref="SkipGroup"/> members.
        ///
        /// <see cref="ProgressBar"/> is a <see cref="Range"/> but is never
        /// focusable — it is display, not input — so it is excluded explicitly.
        ///
        /// <para><b>Internal children.</b> A <see cref="SpinBox"/> keeps its editable
        /// <see cref="LineEdit"/> as an *internal* child, and this walk uses
        /// <c>GetChild</c>, which excludes internal nodes. The SpinBox itself is not a
        /// focus candidate, so without the explicit splice below a spin box is silently
        /// unreachable by keyboard and controller while looking perfectly fine on
        /// screen — B4 hit exactly that on the character-select rules row and worked
        /// around it with a hand-assembled chain (Package 8 §9). The editor is spliced
        /// in at the spin box's own position so authored reading order is preserved.
        /// A non-editable spin box is display, not input, and is skipped. If another
        /// control type ever hides a focusable widget the same way, extend this branch
        /// rather than hand-assembling a chain at the call site.</para>
        /// </summary>
        public static List<Control> Collect(Node root) {
            var found = new List<Control>();
            if (root != null) CollectInto(root, found);
            return found;
        }

        private static void CollectInto(Node node, List<Control> found) {
            if (node is Control control) {
                // A hidden container hides everything under it; do not chain into
                // a subtree the player cannot see or reach.
                if (!control.Visible) return;
                if (IsFocusCandidate(control) && !control.IsInGroup(SkipGroup)) found.Add(control);
                else if (control is SpinBox spinBox && spinBox.Editable && !spinBox.IsInGroup(SkipGroup)) {
                    LineEdit editor = spinBox.GetLineEdit();
                    if (editor != null) found.Add(editor);
                }
            }

            int count = node.GetChildCount();
            for (int index = 0; index < count; index++) {
                CollectInto(node.GetChild(index), found);
            }
        }

        private static bool IsFocusCandidate(Control control) {
            if (control is ProgressBar) return false;
            return control is BaseButton or Slider or LineEdit or TabBar or ItemList or Tree;
        }

        /// <summary>
        /// Links <paramref name="controls"/> into a focus chain along
        /// <paramref name="axis"/>, and makes every one of them focusable.
        ///
        /// <paramref name="wrap"/> joins the last entry back to the first, which is
        /// what a short vertical menu wants: holding down past the bottom returns to
        /// the top rather than dead-ending. Long lists usually pass false.
        /// </summary>
        public static void Chain(
            IReadOnlyList<Control> controls,
            FocusChainAxis axis = FocusChainAxis.Vertical,
            bool wrap = true) {

            if (controls == null || controls.Count == 0) return;

            for (int index = 0; index < controls.Count; index++) {
                Control current = controls[index];
                if (current == null) continue;
                current.FocusMode = Control.FocusModeEnum.All;

                Control previous = ResolvePrevious(controls, index, wrap);
                Control next = ResolveNext(controls, index, wrap);

                SetNeighbor(current, previous, axis, forward: false);
                SetNeighbor(current, next, axis, forward: true);

                // FocusNext/FocusPrevious drive Tab and the ui_focus_next action,
                // which stay linear regardless of the visual axis.
                current.FocusPrevious = previous != null ? current.GetPathTo(previous) : new NodePath();
                current.FocusNext = next != null ? current.GetPathTo(next) : new NodePath();
            }
        }

        private static Control ResolvePrevious(IReadOnlyList<Control> controls, int index, bool wrap) {
            if (index > 0) return controls[index - 1];
            return wrap && controls.Count > 1 ? controls[^1] : null;
        }

        private static Control ResolveNext(IReadOnlyList<Control> controls, int index, bool wrap) {
            if (index < controls.Count - 1) return controls[index + 1];
            return wrap && controls.Count > 1 ? controls[0] : null;
        }

        private static void SetNeighbor(Control current, Control target, FocusChainAxis axis, bool forward) {
            NodePath path = target != null ? current.GetPathTo(target) : new NodePath();
            if (axis == FocusChainAxis.Vertical) {
                if (forward) current.FocusNeighborBottom = path;
                else current.FocusNeighborTop = path;
            } else {
                if (forward) current.FocusNeighborRight = path;
                else current.FocusNeighborLeft = path;
            }
        }

        /// <summary>
        /// Collects and chains in one call, returning the chain so the caller can
        /// hand it to <see cref="GrabInitialFocus"/> or rebuild it later.
        /// </summary>
        public static List<Control> Build(
            Node root,
            FocusChainAxis axis = FocusChainAxis.Vertical,
            bool wrap = true) {

            List<Control> controls = Collect(root);
            Chain(controls, axis, wrap);
            return controls;
        }

        /// <summary>
        /// Focuses the first control that can actually take it. Returns false when
        /// nothing is eligible — a caller that needs a guaranteed focus target
        /// should treat that as a surface bug, not retry.
        /// </summary>
        public static bool GrabInitialFocus(IReadOnlyList<Control> controls) {
            if (controls == null) return false;
            foreach (Control control in controls) {
                if (!IsFocusable(control)) continue;
                control.GrabFocus();
                return true;
            }
            return false;
        }

        /// <summary>Collects, chains, and focuses the first eligible control.</summary>
        public static List<Control> Apply(
            Node root,
            FocusChainAxis axis = FocusChainAxis.Vertical,
            bool wrap = true,
            bool grabFocus = true) {

            List<Control> controls = Build(root, axis, wrap);
            if (grabFocus) GrabInitialFocus(controls);
            return controls;
        }

        private static bool IsFocusable(Control control) {
            if (control == null || !GodotObject.IsInstanceValid(control)) return false;
            if (!control.IsInsideTree() || !control.IsVisibleInTree()) return false;
            if (control.FocusMode == Control.FocusModeEnum.None) return false;
            return control is not BaseButton button || !button.Disabled;
        }
    }
}
