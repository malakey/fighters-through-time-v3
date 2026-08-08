using System.Collections.Generic;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A1. Focus authoring behaviour.
///
/// The repository previously authored no <c>focus_neighbor</c> links at all and
/// held two <c>GrabFocus()</c> calls, leaving controller and keyboard players on
/// Godot's geometric fallback — which silently picks the wrong control the moment
/// containers nest or a widget is hidden. These tests pin the parts of the
/// builder a screen actually depends on: order follows the scene tree, wrapping
/// is symmetric, hidden and non-input controls stay out of the chain, and initial
/// focus lands on something the player can actually use.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FocusChainBuilderTests {

    [TestCase]
    public void ChainsVerticalNeighboursInSceneTreeOrder() {
        var root = AutoFree(new VBoxContainer());
        Button first = AddButton(root, "First");
        Button second = AddButton(root, "Second");
        Button third = AddButton(root, "Third");

        List<Control> chain = FocusChainBuilder.Build(root, FocusChainAxis.Vertical, wrap: false);

        AssertThat(chain.Count).IsEqual(3);
        AssertThat(first.FocusNeighborBottom.ToString()).IsEqual(first.GetPathTo(second).ToString());
        AssertThat(second.FocusNeighborTop.ToString()).IsEqual(second.GetPathTo(first).ToString());
        AssertThat(second.FocusNeighborBottom.ToString()).IsEqual(second.GetPathTo(third).ToString());

        // Every chained control becomes focusable, whatever it was before.
        AssertThat(first.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(third.FocusMode).IsEqual(Control.FocusModeEnum.All);
    }

    [TestCase]
    public void WrappingJoinsTheEndsInBothDirections() {
        var root = AutoFree(new VBoxContainer());
        Button first = AddButton(root, "First");
        AddButton(root, "Second");
        Button last = AddButton(root, "Third");

        FocusChainBuilder.Build(root, FocusChainAxis.Vertical, wrap: true);

        // Holding down past the bottom of a short menu returns to the top.
        AssertThat(last.FocusNeighborBottom.ToString()).IsEqual(last.GetPathTo(first).ToString());
        AssertThat(first.FocusNeighborTop.ToString()).IsEqual(first.GetPathTo(last).ToString());
    }

    [TestCase]
    public void ANonWrappingChainDeadEndsAtBothExtremes() {
        var root = AutoFree(new VBoxContainer());
        Button first = AddButton(root, "First");
        Button last = AddButton(root, "Second");

        FocusChainBuilder.Build(root, FocusChainAxis.Vertical, wrap: false);

        AssertThat(first.FocusNeighborTop.IsEmpty).IsTrue();
        AssertThat(last.FocusNeighborBottom.IsEmpty).IsTrue();
        AssertThat(first.FocusPrevious.IsEmpty).IsTrue();
        AssertThat(last.FocusNext.IsEmpty).IsTrue();
    }

    [TestCase]
    public void TheHorizontalAxisUsesLeftAndRightNeighbours() {
        var root = AutoFree(new HBoxContainer());
        Button left = AddButton(root, "Left");
        Button right = AddButton(root, "Right");

        FocusChainBuilder.Build(root, FocusChainAxis.Horizontal, wrap: false);

        AssertThat(left.FocusNeighborRight.ToString()).IsEqual(left.GetPathTo(right).ToString());
        AssertThat(right.FocusNeighborLeft.ToString()).IsEqual(right.GetPathTo(left).ToString());

        // The vertical links stay untouched so a row can sit inside a column.
        AssertThat(left.FocusNeighborBottom.IsEmpty).IsTrue();
        AssertThat(right.FocusNeighborTop.IsEmpty).IsTrue();

        // Tab order is still linear regardless of the visual axis.
        AssertThat(left.FocusNext.ToString()).IsEqual(left.GetPathTo(right).ToString());
    }

    [TestCase]
    public void CollectionSkipsHiddenSubtreesAndNonInputControls() {
        var root = AutoFree(new VBoxContainer());
        Button visible = AddButton(root, "Visible");

        // A ProgressBar is a Range but is display, never a focus target.
        var bar = new ProgressBar { Name = "Bar" };
        root.AddChild(bar);

        var hiddenGroup = new VBoxContainer { Name = "Hidden", Visible = false };
        root.AddChild(hiddenGroup);
        AddButton(hiddenGroup, "Unreachable");

        var slider = new HSlider { Name = "Slider" };
        root.AddChild(slider);

        List<Control> chain = FocusChainBuilder.Collect(root);

        AssertThat(chain.Count).IsEqual(2);
        AssertThat(chain.Contains(visible)).IsTrue();
        AssertThat(chain.Contains(slider)).IsTrue();
        AssertThat(chain.Contains(bar)).IsFalse();
    }

    [TestCase]
    public void ControlsInTheSkipGroupStayOutOfTheChain() {
        var root = AutoFree(new VBoxContainer());
        Button chained = AddButton(root, "Chained");
        Button excluded = AddButton(root, "Excluded");
        excluded.AddToGroup(FocusChainBuilder.SkipGroup);

        List<Control> chain = FocusChainBuilder.Collect(root);

        AssertThat(chain.Count).IsEqual(1);
        AssertThat(chain.Contains(chained)).IsTrue();
        AssertThat(chain.Contains(excluded)).IsFalse();
    }

    [TestCase]
    public void InitialFocusLandsOnTheFirstControlThatCanTakeIt() {
        var host = new Control { Name = "FocusHost" };
        host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddToTree(host);
        try {
            var column = new VBoxContainer();
            host.AddChild(column);

            // Disabled and hidden controls are skipped rather than swallowing focus.
            Button disabled = AddButton(column, "Disabled");
            disabled.Disabled = true;
            Button usable = AddButton(column, "Usable");

            List<Control> chain = FocusChainBuilder.Apply(column);

            AssertThat(FocusChainBuilder.GrabInitialFocus(chain)).IsTrue();
            AssertObject(host.GetViewport().GuiGetFocusOwner()).IsSame(usable);
        } finally {
            host.QueueFree();
        }
    }

    [TestCase]
    public void GrabbingInitialFocusReportsFailureWhenNothingIsEligible() {
        var root = AutoFree(new VBoxContainer());

        // An empty surface must report failure rather than silently doing nothing,
        // so a screen that expected a focus target can be caught.
        AssertThat(FocusChainBuilder.GrabInitialFocus(FocusChainBuilder.Collect(root))).IsFalse();
        AssertThat(FocusChainBuilder.GrabInitialFocus(null)).IsFalse();
    }

    private static Button AddButton(Node parent, string name) {
        var button = new Button { Name = name, Text = name };
        parent.AddChild(button);
        return button;
    }

    private static void AddToTree(Node node) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(node);
    }
}
