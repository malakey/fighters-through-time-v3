using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-POLISH. The Resonance grid at the pulp type sizes: no word breaks mid-word, no chip covers another chip or the
/// edge of its area, every line of a chip (the UNLOCKED / LOCKED status included) is inside the chip, and the prompt
/// bar clears the panel, for every hero's real grid at UI scale 1.0 and 1.4.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceGridFitTests {

    private static readonly string[] Heroes = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman"
    };

    [TestCase]
    public void TheSeparationKeepsEveryChipClearOfEveryOtherEvenOnTheTightestAuthoredLayout() {
        // Mozart's ascending scale: neighbours 0.13 W and 0.12 H apart, closer than one chip.
        var authored = new List<(float X, float Y)> {
            (0.08f, 0.88f), (0.21f, 0.76f), (0.34f, 0.64f), (0.47f, 0.52f), (0.6f, 0.4f),
            (0.73f, 0.28f), (0.26f, 0.3f), (0.6f, 0.12f), (0.9f, 0.12f)
        };
        var placed = ResonanceChipLayout.Separate(authored, 1480f, 540f, 260f, 120f, 14f, out bool resolved);
        AssertThat(resolved).OverrideFailureMessage("the tightest layout must resolve in the base area").IsTrue();
        var problems = new List<string>();
        for (int i = 0; i < placed.Length; i++) {
            for (int j = i + 1; j < placed.Length; j++) {
                float dx = System.Math.Abs(placed[i].X - placed[j].X) * 1480f;
                float dy = System.Math.Abs(placed[i].Y - placed[j].Y) * 540f;
                if (dx < 260f && dy < 120f) problems.Add($"{i}/{j}");
            }
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        // A layout that already clears is left exactly where it was authored.
        var spread = new List<(float X, float Y)> { (0.2f, 0.2f), (0.8f, 0.8f) };
        var kept = ResonanceChipLayout.Separate(spread, 1480f, 540f, 260f, 120f, 14f, out _);
        AssertThat(Mathf.IsEqualApprox(kept[0].X, 0.2f) && Mathf.IsEqualApprox(kept[1].Y, 0.8f)).IsTrue();
    }

    [TestCase]
    public async Task NoChipOverlapsAnotherOrLeavesItsAreaAndNoTextExceedsItsChipAtBothUiScales() {
        if (GameManager.Instance == null || SaveManager.Instance == null) {
            AssertThat("GameManager or SaveManager is missing").IsEqual("");
        }
        SessionData originalSession = GameManager.Instance.CurrentSession;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[0];
        float originalScale = UIPalette.AppliedUiScale;
        Theme holdTheme = UIPalette.LoadTheme(); // keeps the one scaled instance alive for the screens below
        var problems = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                foreach (string hero in Heroes) {
                    SessionData session = originalSession;
                    session.ActiveSaveSlot = 0;
                    session.SelectedCharacterID = hero;
                    GameManager.Instance.CurrentSession = session;
                    SaveManager.Instance.SaveSlots[0] = new StorySaveData { SelectedCharacterID = hero };

                    var host = new Node { Name = "ResonanceFitHost" };
                    ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
                    try {
                        var panel = new ResonanceGridPanel { Name = "ResonanceGridPanel" };
                        host.AddChild(panel);
                        for (int frame = 0; frame < 4; frame++) await Settle();
                        Inspect(panel, hero, scale, problems);
                    } finally {
                        host.GetParent()?.RemoveChild(host);
                        host.Free();
                    }
                }
            }
        } finally {
            UIPalette.ApplyUiScale(originalScale);
            GameManager.Instance.CurrentSession = originalSession;
            SaveManager.Instance.SaveSlots[0] = originalSave;
            System.GC.KeepAlive(holdTheme);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    private static async Task Settle() {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Inspect(ResonanceGridPanel panel, string hero, float scale, List<string> problems) {
        string tag = $"{hero}@{scale}";
        var chips = new List<(string Id, Button Button, Rect2 Rect)>();
        foreach (var pair in panel.Chips) chips.Add((pair.Key, pair.Value, pair.Value.GetRect()));
        if (chips.Count != 9) problems.Add($"{tag}: {chips.Count} chips");
        var area = new Rect2(Vector2.Zero, panel.GridArea);
        Font font = chips.Count > 0 ? chips[0].Button.GetThemeFont("font") : null;
        int fontSize = chips.Count > 0 ? chips[0].Button.GetThemeFontSize("font_size") : 20;
        float lineHeight = font?.GetHeight(fontSize) ?? 24f;
        for (int i = 0; i < chips.Count; i++) {
            Rect2 rect = chips[i].Rect;
            if (!area.Grow(1f).Encloses(rect)) problems.Add($"{tag}: {chips[i].Id} leaves the area");
            for (int j = i + 1; j < chips.Count; j++) {
                if (rect.Intersects(chips[j].Rect)) problems.Add($"{tag}: {chips[i].Id} overlaps {chips[j].Id}");
            }
            // Text: 12 left, 38 right (the tier mark), 8 top and bottom.
            float contentWidth = rect.Size.X - 50f;
            string text = chips[i].Button.Text;
            foreach (string word in text.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)) {
                float width = font == null ? 0f : font.GetStringSize(word, HorizontalAlignment.Left, -1f, fontSize).X;
                if (width > contentWidth) problems.Add($"{tag}: '{word}' breaks inside {chips[i].Id}");
            }
            var paragraph = new TextParagraph();
            if (font != null) paragraph.AddString(text, font, fontSize);
            paragraph.Width = contentWidth;
            paragraph.BreakFlags = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound;
            float needed = paragraph.GetLineCount() * lineHeight + 16f;
            if (needed > rect.Size.Y + 0.5f) problems.Add($"{tag}: text of {chips[i].Id} needs {needed} in {rect.Size.Y}");
        }
        // The two buttons' labels are whole (wrapped, never trimmed).
        foreach (Button button in new[] { panel.RespecButton, panel.Root.GetNode<Button>("Center/Panel/Layout/ButtonRow/CloseButton") }) {
            PulpButtonDecor decor = button.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor");
            StyleBox box = button.GetThemeStylebox("normal");
            float chrome = (box?.GetContentMargin(Side.Top) ?? 0f) + (box?.GetContentMargin(Side.Bottom) ?? 0f);
            float lh = button.GetThemeFont("font").GetHeight(button.GetThemeFontSize("font_size"));
            if (decor == null) { problems.Add($"{tag}: {button.Name} has no decor"); continue; }
            if (decor.LabelIsTrimmed) problems.Add($"{tag}: {button.Name} '{decor.DrawnText}' ends in an ellipsis");
            if (decor.WrappedLineCount * lh + chrome > button.Size.Y + 1f)
                problems.Add($"{tag}: {button.Name} '{decor.DrawnText}' needs {decor.WrappedLineCount} lines of {lh} in {button.Size.Y} (width {button.Size.X}, avail {decor.LabelAvailableWidth})");
        }
        // The prompt bar must clear the panel (compared in screen space, so the panel fit's scale counts).
        var gridPanel = panel.Root.GetNodeOrNull<Control>("Center/Panel");
        var bar = panel.GetNodeOrNull<Control>(PulpScreenKit.PromptBarName);
        if (gridPanel == null || bar == null) {
            problems.Add($"{tag}: panel or prompt bar missing");
            return;
        }
        Rect2 panelRect = gridPanel.GetGlobalTransform() * new Rect2(Vector2.Zero, gridPanel.Size);
        Rect2 barRect = bar.GetGlobalTransform() * new Rect2(Vector2.Zero, bar.Size);
        if (panelRect.Intersects(barRect)) problems.Add($"{tag}: prompt bar {barRect} overlaps panel {panelRect}");
    }
}
