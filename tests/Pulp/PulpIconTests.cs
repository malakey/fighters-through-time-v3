using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS. The icon widget: the kit list, the three layers, and the never-throws rule for unknown names.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpIconTests {

    [TestCase]
    public void EveryKitIconNameLoadsAllThreeLayers() {
        AssertThat(PulpIcon.KitNames.Count).IsEqual(26);
        foreach (string name in PulpIcon.KitNames) {
            Texture2D[] layers = PulpIcon.LoadLayers(name);
            if (layers == null) AssertThat($"icon '{name}' did not load its three layers").IsEqual("");
            AssertThat(layers.Length).IsEqual(3);
            foreach (Texture2D layer in layers) AssertObject(layer).IsNotNull();
            AssertThat(PulpIcon.Exists(name)).IsTrue();
        }
    }

    [TestCase]
    public void TheKitListMatchesTheFillFilesOnDisk() {
        using DirAccess dir = DirAccess.Open(PulpIcon.IconDirectory);
        AssertObject(dir).IsNotNull();
        int fills = 0;
        foreach (string file in dir.GetFiles()) {
            if (file.StartsWith("ico_") && file.EndsWith("_fill.png")) {
                fills++;
                string name = file.Substring(4, file.Length - 4 - "_fill.png".Length);
                if (!PulpIcon.Exists(name)) AssertThat($"icon file '{file}' is missing from KitNames").IsEqual("");
            }
        }
        AssertThat(fills).IsEqual(PulpIcon.KitNames.Count);
    }

    [TestCase]
    public void AnUnknownNameIsSafeAndDrawsNothing() {
        AssertThat(PulpIcon.Exists("no_such_icon")).IsFalse();
        AssertObject(PulpIcon.LoadLayers("no_such_icon")).IsNull();
        AssertObject(PulpIcon.LoadLayers(null)).IsNull();
        PulpIcon icon = PulpIcon.Create("no_such_icon", Colors.White, 40f);
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(icon);
        try {
            icon.QueueRedraw();
            AssertThat(icon.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        } finally {
            icon.Free();
        }
    }
}
