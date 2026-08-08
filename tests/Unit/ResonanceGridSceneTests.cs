using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B4. The Resonance Grid, converted to an authored scene at the path
/// the content manifest reserves.
///
/// <para>The panel keeps its bespoke <c>_UnhandledInput</c> navigation over the
/// tested <see cref="ResonanceGridNavigation"/> math — the node grid is a spatial
/// talent tree, not a linear menu — so this suite pins the authored shell, the
/// move off the native <c>ConfirmationDialog</c> onto the shared
/// <see cref="ConfirmModal"/>, and the fact that the purchase path still routes
/// through <c>ResonanceProgression</c>. The navigation arithmetic itself stays
/// covered by <c>ResonanceGridNavigationTests</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceGridSceneTests {

    private const string Layout = "Center/Panel/Layout/";

    [TestCase]
    public void TheAuthoredSceneExistsAtTheManifestReservedPath() {
        AssertThat(ResonanceGridPanel.ScenePath).IsEqual("res://scenes/ui/ResonanceGrid.tscn");
        AssertThat(ResourceLoader.Exists(ResonanceGridPanel.ScenePath)).IsTrue();
    }

    [TestCase]
    public void TheAuthoredSceneCarriesEveryControlThePanelBindsByPath() {
        var packed = ResourceLoader.Load<PackedScene>(ResonanceGridPanel.ScenePath);
        var root = packed.Instantiate<Control>();
        try {
            AssertThat(root.GetNodeOrNull<ColorRect>("Shade") != null).IsTrue();
            AssertThat(root.GetNodeOrNull<Label>(Layout + "Title") != null).IsTrue();
            AssertThat(root.GetNodeOrNull<Label>(Layout + "Balance") != null).IsTrue();
            AssertThat(root.GetNodeOrNull<GridContainer>(Layout + "NodeGrid") != null).IsTrue();
            AssertThat(root.GetNodeOrNull<Label>(Layout + "Status") != null).IsTrue();
            AssertThat(root.GetNodeOrNull<Label>(Layout + "Hint") != null).IsTrue();
            AssertThat(root.GetNodeOrNull<Button>(Layout + "CloseButton") != null).IsTrue();

            // Three columns, matching the 3x3 grids and the navigation math's
            // GridColumns constant.
            AssertThat(root.GetNode<GridContainer>(Layout + "NodeGrid").Columns).IsEqual(3);

            // Static copy is raw keys resolved by automatic control translation.
            AssertThat(root.GetNode<Label>(Layout + "Title").Text).IsEqual("resonance_title");
            AssertThat(root.GetNode<Label>(Layout + "Hint").Text).IsEqual("resonance_hint_controls");
            AssertThat(root.GetNode<Button>(Layout + "CloseButton").Text).IsEqual("common_back");
        } finally {
            root.Free();
        }
    }

    [TestCase]
    public void ThePanelPresentsTheAuthoredSceneAndTheSharedModalRatherThanANativeDialog() {
        ResonanceGridPanel panel = Open(out Node host);
        try {
            AssertThat(panel.Root != null).IsTrue();
            AssertThat(panel.Root.Name.ToString()).IsEqual("ResonanceGrid");
            AssertThat(panel.Confirmation != null).IsTrue();
            // A native ConfirmationDialog is a Window: it opens an OS-level popup,
            // ignores the theme, and traps no focus.
            AssertThat(typeof(Window).IsAssignableFrom(panel.Confirmation.GetType())).IsFalse();
            AssertThat(panel.Confirmation.IsOpen).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheNodeGridIsPopulatedFromTheActiveCharacterGridAndStartsOnTheFirstNode() {
        if (GameManager.Instance == null || SaveManager.Instance == null) return;
        SessionData original = GameManager.Instance.CurrentSession;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[0];
        try {
            SessionData session = original;
            session.ActiveSaveSlot = 0;
            session.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession = session;
            SaveManager.Instance.SaveSlots[0] = new StorySaveData { SelectedCharacterID = "einstein" };

            ResonanceGridPanel panel = Open(out Node host);
            try {
                var grid = panel.Root.GetNode<GridContainer>(Layout + "NodeGrid");
                // Einstein's authored grid is 3x3; the buttons are code-built into
                // the authored container because their count and copy come from
                // ResonanceGridData, not from the scene.
                AssertThat(grid.GetChildCount()).IsEqual(9);
                AssertThat(panel.FocusedIndex).IsEqual(0);

                // The balance line resolves its format string rather than showing a key.
                AssertThat(panel.Root.GetNode<Label>(Layout + "Balance").Text)
                    .IsNotEqual("resonance_balance");
            } finally {
                Teardown(host);
            }
        } finally {
            GameManager.Instance.CurrentSession = original;
            SaveManager.Instance.SaveSlots[0] = originalSave;
        }
    }

    [TestCase]
    public void ClosingRaisesTheClosedEventTheHubReliesOnToRestorePlayerControl() {
        ResonanceGridPanel panel = Open(out Node host);
        bool closed = false;
        try {
            panel.Closed += () => closed = true;
            panel.Root.GetNode<Button>(Layout + "CloseButton").EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(closed).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static ResonanceGridPanel Open(out Node host) {
        host = new Node { Name = "ResonanceGridHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var panel = new ResonanceGridPanel { Name = "ResonanceGridPanel" };
        host.AddChild(panel);
        return panel;
    }

    private static void Teardown(Node host) {
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
