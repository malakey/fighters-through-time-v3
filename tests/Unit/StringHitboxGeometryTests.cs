using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the one grounded basic-string hitbox table (2026-10-11):
/// <c>BasicComboRules.StringBox*Units</c> feeds both the Fighter sim (through
/// <c>FP64.FromDouble</c>, bit-identical to the literals it replaced) and the
/// Story box (at <c>KitMotionRules.StoryPixelsPerUnit</c>), and the Story-only
/// AttackRange Resonance minor moves a chain box's far edge by its full
/// percentage (ledger <c>FIX-REACH-MINOR-FORWARD</c>).
///
/// <para>The vertical offset is deliberately not held to the sim: Story
/// applies hit 2's lift downward (ledger <c>VERIFY-STORY-STRING-HIT2-LIFT</c>),
/// which the template pin below records as the current build.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StringHitboxGeometryTests {

    private const float PixelsPerUnit = KitMotionRules.StoryPixelsPerUnit;

    [TestCase]
    public void StoryFarEdgeIsTheSimFarEdgeAtSixtyPixelsPerUnitForEveryHitAndProfile() {
        var profiles = new List<(string Label, BasicStringProfile Profile)> {
            ("template", BasicComboRules.TemplateStringProfile)
        };
        foreach (string id in CharacterRoster.IDs) {
            profiles.Add((id, BasicComboRules.StringProfileFor(id)));
        }
        AssertThat(profiles.Count > 1).OverrideFailureMessage("The roster is empty.").IsTrue();

        foreach ((string label, BasicStringProfile profile) in profiles) {
            for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                PlayerController.ResolveGroundStringBoxPixels(
                    step, profile, 1f, out Vector2 storySize, out Vector2 storyOffset);
                SimBox(profile, step, facingRight: true, out double centreX, out double halfX, out double halfY);

                double storyFar = storyOffset.X + storySize.X * 0.5;
                double simFar = (centreX + halfX) * PixelsPerUnit;
                AssertThat(System.Math.Abs(storyFar - simFar) < 0.01)
                    .OverrideFailureMessage(
                        $"{label} hit {step + 1}: Story far edge {storyFar:F3} px, sim {simFar:F3} px.")
                    .IsTrue();

                double storyNear = storyOffset.X - storySize.X * 0.5;
                double simNear = (centreX - halfX) * PixelsPerUnit;
                AssertThat(System.Math.Abs(storyNear - simNear) < 0.01)
                    .OverrideFailureMessage(
                        $"{label} hit {step + 1}: Story near edge {storyNear:F3} px, sim {simNear:F3} px.")
                    .IsTrue();
                AssertThat(storyNear >= 0.0)
                    .OverrideFailureMessage($"{label} hit {step + 1}: the box reaches behind the hero.")
                    .IsTrue();

                double simHeight = halfY * 2.0 * PixelsPerUnit;
                AssertThat(System.Math.Abs(storySize.Y - simHeight) < 0.01)
                    .OverrideFailureMessage(
                        $"{label} hit {step + 1}: Story height {storySize.Y:F3} px, sim {simHeight:F3} px.")
                    .IsTrue();
            }
        }
    }

    [TestCase]
    public void TheTemplateBoxesKeepTheirPixelsAndTheSimKeepsItsFixedPointValues() {
        // Story: exactly the pixel table the one canonical table replaced.
        Vector2[] sizes = { new(72f, 60f), new(84f, 72f), new(108f, 84f) };
        Vector2[] offsets = { new(60f, -32f), new(60f, -20f), new(72f, -32f) };
        for (int step = 0; step < BasicComboRules.ComboHits; step++) {
            PlayerController.ResolveGroundStringBoxPixels(
                step, BasicComboRules.TemplateStringProfile, 1f, out Vector2 size, out Vector2 offset);
            AssertThat(size).IsEqual(sizes[step]);
            AssertThat(offset).IsEqual(offsets[step]);
        }

        // Sim: raw-for-raw the fixed-point literals it used before, for every
        // profile and both facings, so no Fighter hash can move.
        FP64[] halfWidths = { FP64.FromDouble(0.6), FP64.FromDouble(0.7), FP64.FromDouble(0.9) };
        FP64[] halfHeights = { FP64.FromDouble(0.5), FP64.FromDouble(0.6), FP64.FromDouble(0.7) };
        FP64[] offsetsX = { FP64.One, FP64.One, FP64.FromDouble(1.2) };
        FP64[] offsetsY = { FP64.Zero, FP64.FromDouble(0.2), FP64.Zero };
        var origin = new FPVector2(FP64.FromDouble(3.25), FP64.FromDouble(-1.5));
        var ids = new List<string>(CharacterRoster.IDs) { null };
        foreach (string id in ids) {
            BasicStringProfile profile = BasicComboRules.StringProfileFor(id);
            FP64 widthScale = FP64.FromInt(profile.ReachWidthPercent) / FP64.FromInt(100);
            FP64 heightScale = FP64.FromInt(profile.ReachHeightPercent) / FP64.FromInt(100);
            foreach (bool facingRight in new[] { true, false }) {
                var attacker = new FighterStateComponent {
                    Position = origin,
                    FacingRight = facingRight ? 1 : 0
                };
                FP64 facing = facingRight ? FP64.One : -FP64.One;
                for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                    FighterCombatSystem.ResolveStringBox(
                        in attacker, in profile, step, out FPVector2 centre, out FPVector2 half);
                    string where = $"{id ?? "template"} hit {step + 1} facing {(facingRight ? "right" : "left")}";
                    AssertThat(centre.x.RawValue)
                        .OverrideFailureMessage($"{where}: centre x moved.")
                        .IsEqual((origin.x + facing * offsetsX[step] * widthScale).RawValue);
                    AssertThat(centre.y.RawValue)
                        .OverrideFailureMessage($"{where}: centre y moved.")
                        .IsEqual((origin.y + offsetsY[step]).RawValue);
                    AssertThat(half.x.RawValue)
                        .OverrideFailureMessage($"{where}: half width moved.")
                        .IsEqual((halfWidths[step] * widthScale).RawValue);
                    AssertThat(half.y.RawValue)
                        .OverrideFailureMessage($"{where}: half height moved.")
                        .IsEqual((halfHeights[step] * heightScale).RawValue);
                }
            }
        }
    }

    [TestCase]
    public void ATenPercentReachNodeMovesTheHitOneFarEdgeByTenPercent() {
        // The three authored AttackRange minors, resolved through the real grid.
        (string Hero, string Node, float Percent)[] nodes = {
            ("lincoln", "lincoln_rail_reach", 0.10f),
            ("shakespeare", "shakespeare_quill_reach", 0.10f),
            ("leonardo", "leonardo_brush_reach", 0.08f)
        };
        StaticBody2D floor = CreateFlatFloor();
        try {
            foreach ((string hero, string node, float percent) in nodes) {
                var save = new StorySaveData { SelectedCharacterID = hero };
                save.GridProgress[hero] = new List<string> { node };
                ResonanceGridData grid = AuthoredResources.Load<ResonanceGridData>(
                    $"res://resources/Resonance/{hero}_grid.tres");
                float multiplier = ResonanceProgression.Resolve(grid, save).AttackRangeMultiplier;
                AssertThat(multiplier)
                    .OverrideFailureMessage($"{node} must resolve to +{percent:P0} attack range.")
                    .IsEqualApprox(1f + percent, 0.0001f);

                (float baseFar, float baseNear) = MeasureHitOne(hero, 1f);
                (float far, float near) = MeasureHitOne(hero, multiplier);
                AssertThat(far)
                    .OverrideFailureMessage(
                        $"{node}: the hit-1 far edge must move from {baseFar:F2} px by {percent:P0}, got {far:F2} px.")
                    .IsEqualApprox(baseFar * (1f + percent), 0.01f);
                AssertThat(near)
                    .OverrideFailureMessage($"{node}: the near edge must scale with the box, not grow backward.")
                    .IsEqualApprox(baseNear * (1f + percent), 0.01f);
                AssertThat(near > 0f)
                    .OverrideFailureMessage($"{node}: the box reaches behind the hero.")
                    .IsTrue();
            }
        } finally {
            floor.Free();
        }
    }

    [TestCase]
    public void TheReachMinorGrowsTheCentredUpAttackAboutItsCentre() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController baseline = CreateGroundedPlayer("lincoln");
        PlayerController reached = null;
        try {
            CollisionShape2D baseShape = SwingToActive(baseline, vertical: -1f);
            AssertThat(baseline.CurrentState).IsEqual(CharacterState.Attacking);
            Vector2 baseSize = ((RectangleShape2D)baseShape.Shape).Size;
            Vector2 basePosition = baseShape.Position;
            ReleasePlayer(baseline);
            baseline = null;

            reached = CreateGroundedPlayer("lincoln");
            reached.StoryAttackRangeMultiplier = 1.1f;
            CollisionShape2D shape = SwingToActive(reached, vertical: -1f);
            AssertThat(((RectangleShape2D)shape.Shape).Size.X).IsEqualApprox(baseSize.X * 1.1f, 0.01f);
            AssertThat(((RectangleShape2D)shape.Shape).Size.Y).IsEqualApprox(baseSize.Y * 1.1f, 0.01f);
            AssertThat(shape.Position)
                .OverrideFailureMessage("The centred Up-Attack box must stay where it is and grow about its centre.")
                .IsEqual(basePosition);
            AssertThat(shape.Position.X).IsEqual(0f);
        } finally {
            if (baseline != null) ReleasePlayer(baseline);
            if (reached != null) ReleasePlayer(reached);
            floor.Free();
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static void SimBox(
        BasicStringProfile profile, int step, bool facingRight,
        out double centreX, out double halfX, out double halfY) {
        var attacker = new FighterStateComponent {
            Position = new FPVector2(FP64.Zero, FP64.Zero),
            FacingRight = facingRight ? 1 : 0
        };
        FighterCombatSystem.ResolveStringBox(
            in attacker, in profile, step, out FPVector2 centre, out FPVector2 half);
        centreX = Units(centre.x);
        halfX = Units(half.x);
        halfY = Units(half.y);
    }

    private static double Units(FP64 value) => (double)value.RawValue / FP64.One.RawValue;

    /// <summary>The live hit-1 box of a grounded hero: its far and near edge, in px ahead of the pivot.</summary>
    private static (float Far, float Near) MeasureHitOne(string characterID, float reachMultiplier) {
        PlayerController player = CreateGroundedPlayer(characterID);
        try {
            player.StoryAttackRangeMultiplier = reachMultiplier;
            CollisionShape2D shape = SwingToActive(player, vertical: 0f);
            AssertThat(player.ComboCounter).IsEqual(0);
            float halfWidth = ((RectangleShape2D)shape.Shape).Size.X * 0.5f;
            return (shape.Position.X + halfWidth, shape.Position.X - halfWidth);
        } finally {
            ReleasePlayer(player);
        }
    }

    /// <summary>Presses attack once and steps the hero until its melee box is live.</summary>
    private static CollisionShape2D SwingToActive(PlayerController player, float vertical) {
        var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
        SendInput(player, GameplayButtons.BasicAttack, vertical);
        for (int frame = 0; frame < 60 && !hitbox.IsActive; frame++) {
            SendInput(player, GameplayButtons.None, vertical);
        }
        AssertThat(hitbox.IsActive).OverrideFailureMessage("The swing never reached its active window.").IsTrue();
        return hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StringHitboxGeometryFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    private static PlayerController CreateGroundedPlayer(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -8f);
            var neutral = new BufferedInputSource();
            neutral.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            InputManager.Instance.SetInputSource(player.PlayerIndex, neutral);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                player._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(player.IsOnFloor())
                .OverrideFailureMessage("The harness player must be standing on the floor.")
                .IsTrue();
            for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
                player._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The harness player must settle to Idle before a test scripts input.")
                .IsEqual(CharacterState.Idle);
            AssertThat(player.IsFacingRight).IsTrue();
            return player;
        } catch {
            ReleasePlayer(player);
            throw;
        }
    }

    private static void ReleasePlayer(PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        player.Free();
    }

    private static void SendInput(PlayerController player, GameplayButtons buttons, float vertical) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, vertical, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
