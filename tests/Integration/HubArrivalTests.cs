using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using HubPeriod = FTT.Environment.HubDialogueSelector.HubPeriod;

namespace FTT.Tests.Integration;

/// <summary>
/// Package 12 W7: the hub's arrival point and crew.
///
/// <para><b>M11.</b> Every hub arrival — a level completion and a Collapse alike —
/// lands on the Bridge in front of the Chronal Repository. The Calibration Bay
/// anchor stays (Level 0's room, the Training Wing) but is no longer the spawn.</para>
///
/// <para><b>GAP-15.</b> Every sequence the act-boundary selector can name is
/// authored in the hub set, Medic Okafor has a post to deliver his, and a slotless
/// hub (no campaign ledger to record into) never auto-plays a set — so it never
/// takes the tree's pause either.</para>
///
/// <para>One <c>[TestSuite]</c> per file; the scene is freed and the pause
/// restored in <c>finally</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HubArrivalTests {

    private const string HubScenePath = "res://scenes/campaign/HubWorld.tscn";
    private const string HubDialoguePath = "res://resources/Dialogue/hub_dialogue.tres";

    [TestCase]
    public void EveryArrivalSpawnsOnTheBridgeAndTheCrewHaveTheirPosts() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Node hub = null;
        try {
            hub = ResourceLoader.Load<PackedScene>(HubScenePath).Instantiate();
            tree.Root.AddChild(hub);

            var bridge = hub.GetNodeOrNull<Marker2D>(HubWorldController.BridgeSpawnName);
            AssertObject(bridge).OverrideFailureMessage("The Bridge arrival anchor is missing.").IsNotNull();
            AssertThat(bridge.Position).IsEqual(HubWorldController.BridgeSpawnPosition);
            AssertObject(hub.GetNodeOrNull<Marker2D>("CalibrationBaySpawn"))
                .OverrideFailureMessage("The Calibration Bay anchor stays for Level 0 and the Training Wing.")
                .IsNotNull();

            var player = hub.GetNodeOrNull<Node2D>("Player");
            AssertObject(player).IsNotNull();
            AssertThat(player.Position).IsEqual(HubWorldController.BridgeSpawnPosition);

            // The Bridge sits between the Repository and the portal (design §3 hub layout).
            var repository = hub.GetNodeOrNull<Node2D>("ChronalRepository");
            var portal = hub.GetNodeOrNull<Node2D>("TemporalPortal");
            AssertThat(bridge.Position.X > repository.Position.X && bridge.Position.X < portal.Position.X)
                .IsTrue();

            AssertObject(hub.GetNodeOrNull<Area2D>("CommanderSarah")).IsNotNull();
            AssertObject(hub.GetNodeOrNull<Area2D>(HubWorldController.OkaforNodeName))
                .OverrideFailureMessage("Medic Okafor needs a post to deliver his act sets.").IsNotNull();

            // No campaign slot: nothing auto-plays, so the tree is never paused.
            AssertThat(tree.Paused).IsEqual(originalPaused);
        } finally {
            if (hub != null && GodotObject.IsInstanceValid(hub)) {
                hub.GetParent()?.RemoveChild(hub);
                hub.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// Package 12 Phase C (the W5/W7 seam on <c>SpawnPlayer</c>): a Holodeck
    /// exit's one-shot session flag lands the player at the console marker, is
    /// consumed on arrival, and the next ordinary arrival is back on the Bridge.
    /// </summary>
    [TestCase]
    public void AHolodeckExitArrivesAtTheConsoleOnceAndTheNextArrivalIsTheBridge() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        SessionData original = GameManager.Instance.CurrentSession;
        Node hub = null;
        try {
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = -1;
            session.ArriveAtHolodeckConsole = true;
            session.ReopenHolodeckConsole = false;
            GameManager.Instance.CurrentSession = session;

            hub = ResourceLoader.Load<PackedScene>(HubScenePath).Instantiate();
            tree.Root.AddChild(hub);
            var console = hub.GetNodeOrNull<Marker2D>(HubWorldController.HolodeckConsoleSpawnName);
            AssertObject(console).OverrideFailureMessage("The Holodeck console arrival marker is missing.").IsNotNull();
            AssertThat(console.Position).IsEqual(HubWorldController.HolodeckConsoleSpawnPosition);
            AssertThat(hub.GetNodeOrNull<Node2D>("Player").Position)
                .IsEqual(HubWorldController.HolodeckConsoleSpawnPosition);
            AssertThat(GameManager.Instance.CurrentSession.ArriveAtHolodeckConsole)
                .OverrideFailureMessage("The arrival flag is one-shot and consumed by the hub.").IsFalse();

            hub.GetParent()?.RemoveChild(hub);
            hub.Free();
            hub = ResourceLoader.Load<PackedScene>(HubScenePath).Instantiate();
            tree.Root.AddChild(hub);
            AssertThat(hub.GetNodeOrNull<Node2D>("Player").Position)
                .IsEqual(HubWorldController.BridgeSpawnPosition);
        } finally {
            if (hub != null && GodotObject.IsInstanceValid(hub)) {
                hub.GetParent()?.RemoveChild(hub);
                hub.Free();
            }
            GameManager.Instance.CurrentSession = original;
            tree.Paused = originalPaused;
        }
    }

    [TestCase]
    public void EverySequenceTheSelectorCanNameIsAuthoredInTheHubSet() {
        var set = AuthoredResources.Load<DialogueSetData>(HubDialoguePath);
        AssertObject(set).IsNotNull();
        var names = new List<string>();
        foreach (HubPeriod period in System.Enum.GetValues<HubPeriod>()) {
            names.Add(HubDialogueSelector.SarahSequenceFor(period));
            string okafor = HubDialogueSelector.OkaforSequenceFor(period);
            if (!string.IsNullOrEmpty(okafor)) names.Add(okafor);
            // Package 13 W3: every per-line beat, the boundary scene and Wren.
            foreach (HubDialogueSelector.HubLine line in HubDialogueSelector.SarahLinesFor(period)) {
                names.Add(line.SequenceID);
            }
            names.Add(HubDialogueSelector.WrenSequenceFor(period));
        }
        names.Add(HubDialogueSelector.ActIBoundary);
        foreach (string id in names) {
            AssertObject(set.Find(id))
                .OverrideFailureMessage($"The selector names '{id}', which hub_dialogue.tres does not author.")
                .IsNotNull();
        }
        // M14 on the crew too: Okafor's Act II era sweep never reports the active hero missing.
        DialogueSequenceData mozartSweep = set.FindSequence("hub.okafor_act2", "mozart");
        AssertString(mozartSweep.DialogueID).IsEqual("hub.okafor_act2@mozart");
        AssertString(set.FindSequence("hub.okafor_act2", "einstein").DialogueID).IsEqual("hub.okafor_act2");
        // ...and the boundary scene carries the same swap (Princeton, 1955).
        AssertString(set.FindSequence(HubDialogueSelector.ActIBoundary, "mozart").DialogueID)
            .IsEqual("hub.act1_boundary@mozart");
    }

    /// <summary>
    /// Package 13 W3 (S09/S11): Chief Engineer Wren has a post in the Training
    /// Wing, and the wordless triage readout stands beside the portal with no
    /// collision (it is a display, never a level select). A slotless hub still
    /// builds it; with no campaign it reads the prologue state.
    /// </summary>
    [TestCase]
    public void WrenHasAPostAndTheTriageReadoutStandsBesideThePortal() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Node hub = null;
        try {
            hub = ResourceLoader.Load<PackedScene>(HubScenePath).Instantiate();
            tree.Root.AddChild(hub);

            var wren = hub.GetNodeOrNull<Area2D>(HubWorldController.WrenNodeName);
            AssertObject(wren).OverrideFailureMessage("Chief Engineer Wren has no post in the hub.").IsNotNull();
            var holodeck = hub.GetNodeOrNull<Node2D>("HolodeckConsole");
            AssertThat(Mathf.Abs(wren.Position.X - holodeck.Position.X) < 400f)
                .OverrideFailureMessage("Wren belongs in the Training Wing, beside the Holodeck.").IsTrue();

            var readout = hub.GetNodeOrNull<HubTriageReadout>(HubTriageReadout.NodeName);
            AssertObject(readout).OverrideFailureMessage("The triage readout is missing.").IsNotNull();
            var portal = hub.GetNodeOrNull<Node2D>("TemporalPortal");
            AssertThat(Mathf.Abs(readout.Position.X - portal.Position.X) < 300f)
                .OverrideFailureMessage("The readout must stand beside the portal.").IsTrue();
            foreach (Node child in readout.FindChildren("*", owned: false)) {
                AssertThat(child is CollisionObject2D)
                    .OverrideFailureMessage("The triage readout must be non-interactive.").IsFalse();
            }
        } finally {
            if (hub != null && GodotObject.IsInstanceValid(hub)) {
                hub.GetParent()?.RemoveChild(hub);
                hub.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }
}
