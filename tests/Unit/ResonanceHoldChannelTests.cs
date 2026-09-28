using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W8 — M16: the Act III Resonance Hold stand-ins are a 180-frame
/// held-Interact channel, not a machine to smash. No HP, no discharge; the
/// channel resets on damage, range, release, Time Freeze and the Post-Landing
/// Hold; completion is an Extractor destruction in every bookkeeping sense
/// (drain factor, the F05 allocation as a physical pickup, the destroyed
/// registry) exactly once, while Integrity keeps draining throughout.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceHoldChannelTests {

    private const string NodeID = "level_13_extractor_0";

    [TestCase]
    public void AFullHeldChannelCompletesTheNodeOnceThroughTheExtractorPath() {
        using var fixture = new HoldFixture();
        ResonanceHoldNode node = fixture.Node;
        PlayerController player = fixture.Player;

        AssertThat(node.CanInteract(player)).IsTrue();
        node.Interact(player);
        AssertThat(node.IsChanneling).IsTrue();
        fixture.Pump(ResonanceHoldNode.ChannelFrames - 1);
        AssertThat(node.IsDestroyed)
            .OverrideFailureMessage("179 held frames must not complete a 180-frame channel.")
            .IsFalse();
        AssertThat(node.ChannelRadial.Visible).IsTrue();
        AssertThat(node.ChannelProgress > 0.99f).IsTrue();

        fixture.Pump(1);
        AssertThat(node.IsDestroyed).IsTrue();
        AssertThat(node.IsChanneling).IsFalse();
        AssertThat(node.ChannelRadial.Visible).IsFalse();
        AssertThat(StoryManager.Instance.IsExtractorDestroyed(NodeID)).IsTrue();
        AssertThat(StoryManager.Instance.LivingExtractorCount)
            .OverrideFailureMessage("Completion removes the node's +0.2 from the drain factor.")
            .IsEqual(0);

        List<ChronalDustPickup> pickups = fixture.Pickups();
        AssertThat(pickups.Count).OverrideFailureMessage("Exactly one F05 pickup spawns.").IsEqual(1);
        int award = LevelRewardDirectory.Current.Award(NodeID);
        AssertThat(award > 0).IsTrue();
        AssertThat(pickups[0].DustAmount).IsEqual(award);
        AssertString(pickups[0].SourceID).IsEqual(NodeID);
        AssertThat(pickups[0].Source).IsEqual(DustAwardSource.Extractor);

        // A completed node cannot be channelled again.
        AssertThat(node.CanInteract(player)).IsFalse();
        node.Interact(player);
        fixture.Pump(ResonanceHoldNode.ChannelFrames + 5);
        AssertThat(fixture.Pickups().Count).IsEqual(1);
    }

    [TestCase]
    public void TheNodeHasNoHpAndNeverDischarges() {
        using var fixture = new HoldFixture();
        ResonanceHoldNode node = fixture.Node;
        AssertThat(node.TakeEnvironmentDamage(9999f))
            .OverrideFailureMessage("A stand-in has no HP: direct damage (e.g. a kit arc) applies nothing.")
            .IsEqual(0);
        AssertThat(node.IsDestroyed).IsFalse();
        var hurtbox = node.GetNodeOrNull<Area2D>("Hurtbox");
        AssertObject(hurtbox).IsNotNull();
        AssertThat(hurtbox.Monitorable).OverrideFailureMessage("Nothing can strike the node.").IsFalse();

        // Ten seconds of idle ticking: the Extractor cycle would have telegraphed
        // and discharged at least once.
        for (int frame = 0; frame < 600; frame++) node._PhysicsProcess(1.0 / 60.0);
        AssertThat(node.DischargeCount).IsEqual(0);
        AssertThat(node.IsTelegraphing).IsFalse();
        AssertThat(fixture.Player.CurrentHP).IsEqual(fixture.Player.MaximumHP);
    }

    [TestCase]
    public void ReleasingLeavingRangeOrTakingDamageResetsTheChannelToZero() {
        using var fixture = new HoldFixture();
        ResonanceHoldNode node = fixture.Node;
        PlayerController player = fixture.Player;

        // Release.
        node.Interact(player);
        fixture.Pump(60);
        AssertThat(node.ChannelFramesElapsed).IsEqual(60);
        fixture.SetHeld(false);
        fixture.Pump(1);
        AssertThat(node.IsChanneling).IsFalse();
        AssertThat(node.ChannelFramesElapsed).IsEqual(0);

        // Damage.
        fixture.SetHeld(true);
        fixture.Pump(1);
        node.Interact(player);
        fixture.Pump(90);
        player.ApplyDamage(1);
        fixture.Pump(1);
        AssertThat(node.IsChanneling).IsFalse();
        AssertThat(node.ChannelFramesElapsed).IsEqual(0);

        // Range.
        node.Interact(player);
        fixture.Pump(90);
        player.GlobalPosition = node.GlobalPosition + new Vector2(node.ChannelRangePixels + 80f, -40f);
        fixture.Pump(1);
        AssertThat(node.IsChanneling).IsFalse();
        AssertThat(node.IsDestroyed).IsFalse();
        AssertThat(fixture.Pickups().Count).IsEqual(0);
    }

    [TestCase]
    public void TimeFreezeAndThePostLandingHoldRefuseAndResetTheChannel() {
        using var fixture = new HoldFixture();
        ResonanceHoldNode node = fixture.Node;
        PlayerController player = fixture.Player;

        player.TimeFrozen = true;
        try {
            AssertThat(node.CanInteract(player)).OverrideFailureMessage("No channel during Time Freeze.").IsFalse();
        } finally {
            player.TimeFrozen = false;
        }

        node.Interact(player);
        fixture.Pump(100);
        player.TimeFrozen = true;
        try {
            node.AdvanceChannel();
            AssertThat(node.IsChanneling).IsFalse();
            AssertThat(node.ChannelFramesElapsed).IsEqual(0);
        } finally {
            player.TimeFrozen = false;
        }

        player.BeginRecoveryHold(StoryRecoveryHoldCause.DeathRewind, 60);
        try {
            AssertThat(node.CanInteract(player))
                .OverrideFailureMessage("No channel during the Post-Landing Hold.")
                .IsFalse();
        } finally {
            player.CancelRecoveryHold();
        }
        AssertThat(node.IsDestroyed).IsFalse();
    }

    [TestCase]
    public void EveryVariantHasAHeldInteractPromptThatResolves() {
        var issues = new List<string>();
        foreach (ResonanceHoldVariant variant in System.Enum.GetValues<ResonanceHoldVariant>()) {
            string key = ResonanceHoldNode.ChannelPromptKeyFor(variant);
            if (TranslationServer.Translate(key) == key) issues.Add($"{variant}: '{key}' does not resolve");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // ---- Harness -------------------------------------------------------------

    private sealed class HoldFixture : System.IDisposable {
        public readonly ResonanceHoldNode Node;
        public readonly PlayerController Player;
        private readonly Node2D _host;
        private readonly BufferedInputSource _input = new();
        private readonly CampaignLevel _originalLevel;
        private readonly Difficulty _originalDifficulty;
        private readonly string _originalCharacter;
        private readonly int _originalSlot;

        public HoldFixture() {
            StoryManager story = StoryManager.Instance;
            _originalLevel = story.CurrentLevel;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            // Slotless Level 13, so the node's ObjectID is a ledgered F05 source
            // and nothing here can write a save.
            story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Normal);
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
            story.BeginIntegrityClock(300f, 1);

            var tree = (SceneTree)Engine.GetMainLoop();
            _host = new Node2D { Name = "HoldChannelHost" };
            tree.Root.AddChild(_host);
            var floor = new StaticBody2D { CollisionLayer = CollisionLayers.Environment, CollisionMask = 0 };
            floor.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
                Position = new Vector2(0f, 20f)
            });
            _host.AddChild(floor);

            var packed = ResourceLoader.Load<PackedScene>(StoryLevelControllerBase.ResonanceHoldTemplatePath);
            Node = packed.Instantiate<ResonanceHoldNode>();
            Node.ObjectID = NodeID;
            Node.Variant = ResonanceHoldVariant.SeveredConduit;
            // Close enough that the hero standing on the floor (origin at the
            // feet) stays inside the 130 px channel range: |(90, -72)| ≈ 115.
            Node.Position = new Vector2(90f, -72f);
            _host.AddChild(Node);

            Player = CharacterFactory.CreateCharacter("einstein");
            Player.Position = new Vector2(0f, -40f);
            _host.AddChild(Player);
            SetHeld(true);
            InputManager.Instance.SetInputSource(Player.PlayerIndex, _input);
            Pump(1);
        }

        public void SetHeld(bool held) {
            _input.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, held ? GameplayButtons.Interact : GameplayButtons.None));
            // InputManager caches one frame per physics tick, and a synchronous
            // test never advances the tick: capturing at a foreign tick evicts
            // the cache so the player's next read samples the new frame.
            if (Player != null) {
                uint tick = unchecked((uint)Engine.GetPhysicsFrames() + 7919u);
                InputManager.Instance.CaptureFrame(Player.PlayerIndex, tick);
            }
        }

        public void Pump(int frames) {
            for (int frame = 0; frame < frames; frame++) {
                Player._PhysicsProcess(1.0 / 60.0);
                Node._PhysicsProcess(1.0 / 60.0);
            }
        }

        public List<ChronalDustPickup> Pickups() {
            var list = new List<ChronalDustPickup>();
            Godot.Collections.Array<Node> children = _host.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is ChronalDustPickup pickup && pickup.Visible) list.Add(pickup);
            }
            return list;
        }

        public void Dispose() {
            InputManager.Instance?.ClearInputSource(Player.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(_host);
            _host.GetParent()?.RemoveChild(_host);
            _host.Free();
            StoryManager story = StoryManager.Instance;
            story.StopIntegrityClock();
            story.PrepareDirectLevel(_originalLevel,
                string.IsNullOrEmpty(_originalCharacter) ? "einstein" : _originalCharacter, _originalDifficulty);
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
    }
}
