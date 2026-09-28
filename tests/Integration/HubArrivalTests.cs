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

    [TestCase]
    public void EverySequenceTheSelectorCanNameIsAuthoredInTheHubSet() {
        var set = AuthoredResources.Load<DialogueSetData>(HubDialoguePath);
        AssertObject(set).IsNotNull();
        var names = new List<string>();
        foreach (HubPeriod period in System.Enum.GetValues<HubPeriod>()) {
            names.Add(HubDialogueSelector.SarahSequenceFor(period));
            string okafor = HubDialogueSelector.OkaforSequenceFor(period);
            if (!string.IsNullOrEmpty(okafor)) names.Add(okafor);
        }
        foreach (string id in names) {
            AssertObject(set.Find(id))
                .OverrideFailureMessage($"The selector names '{id}', which hub_dialogue.tres does not author.")
                .IsNotNull();
        }
        // M14 on the crew too: Okafor's Act II era sweep never reports the active hero missing.
        DialogueSequenceData mozartSweep = set.FindSequence("hub.okafor_act2", "mozart");
        AssertString(mozartSweep.DialogueID).IsEqual("hub.okafor_act2@mozart");
        AssertString(set.FindSequence("hub.okafor_act2", "einstein").DialogueID).IsEqual("hub.okafor_act2");
    }
}
