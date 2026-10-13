using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Swinging-rope bars are not solid (2026-10-09). <c>PendulumAnchorTemplate</c>'s
/// root is an <see cref="AnimatableBody2D"/> that only carries the swing; it used
/// to sit on the Environment layer with a 16 x 320 rope shape, so heroes landed on
/// the bars, bumped into them and rode them, and a held release floated into the
/// next rope's bar above its grab box (playtest bot game bug 12). The contract
/// now: the rope body is on no layer and scans none, so it blocks no hero, enemy,
/// projectile or ray, and the only thing that touches the hero is the child
/// <see cref="LedgeGrabPoint"/> (Trigger layer, Player mask), whose grab, carry and
/// release launch are unchanged. Real physics frames and a real Story hero.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PendulumRopeBarTests {
    private const string TemplatePath = "res://scenes/templates/PendulumAnchorTemplate.tscn";

    /// <summary>Every scene that instances the rope template, with its rope count.</summary>
    private static readonly (string Path, int Ropes)[] RopeScenes = {
        ("res://scenes/campaign/Level_07_Nassau.tscn", 3),
        ("res://scenes/campaign/Level_10_Globe.tscn", 4),
    };

    [TestCase]
    public void TheTemplateAndEveryAuthoredRopeBlockNothingAndKeepTheirGrab() {
        var issues = new List<string>();
        var template = ResourceLoader.Load<PackedScene>(TemplatePath).Instantiate<PendulumAnchor>();
        try {
            CheckRope(TemplatePath, template, issues);
        } finally {
            template.Free();
        }

        foreach ((string path, int expected) in RopeScenes) {
            Node level = ResourceLoader.Load<PackedScene>(path).Instantiate();
            try {
                int found = 0;
                var stack = new Stack<Node>();
                stack.Push(level);
                while (stack.Count > 0) {
                    Node node = stack.Pop();
                    foreach (Node child in node.GetChildren()) stack.Push(child);
                    if (node is not PendulumAnchor rope) continue;
                    found++;
                    CheckRope($"{path}:{rope.Name}", rope, issues);
                }
                if (found != expected) issues.Add($"{path}: {found} ropes, expected {expected}");
            } finally {
                level.Free();
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// A hero dropped onto the top of a plumb rope falls straight down its bar,
    /// never standing on it or sliding off it, until the grab box at the bottom
    /// catches it - and a ray with every mask bit set crosses the bar without a
    /// hit, so no enemy probe or projectile is stopped by it either.
    /// </summary>
    [TestCase]
    public async Task AHeroFallsThroughTheBarUntilTheGrabBelowItCatches() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var pivot = new Vector2(72000f, -30000f);
        var host = new Node2D { Name = "RopeBarHost" };
        tree.Root.AddChild(host);
        PlayerController hero = null;
        var input = new BufferedInputSource();
        try {
            var rope = ResourceLoader.Load<PackedScene>(TemplatePath).Instantiate<PendulumAnchor>();
            rope.Name = "RopeBar";
            rope.AnchorID = "test.rope_bar";
            rope.Enabled = false; // phase 0, offset 0: the rope hangs plumb
            rope.Position = pivot;
            host.AddChild(rope);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertFloat(rope.RotationDegrees).IsEqualApprox(0f, 0.01f);

            PhysicsDirectSpaceState2D space = rope.GetWorld2D().DirectSpaceState;
            var query = PhysicsRayQueryParameters2D.Create(pivot + new Vector2(-120f, 160f),
                pivot + new Vector2(120f, 160f), uint.MaxValue);
            using (Godot.Collections.Dictionary hit = space.IntersectRay(query)) {
                AssertThat(hit.Count)
                    .OverrideFailureMessage($"A ray across the rope bar hit {(hit.Count > 0 ? hit["collider"] : "")}.")
                    .IsEqual(0);
            }

            hero = CharacterFactory.CreateCharacter("joan", 0);
            InputManager.Instance.SetInputSource(0, input);
            input.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            host.AddChild(hero);
            // Feet 40 px above the pivot, centred on the bar: a solid bar is landed on.
            hero.GlobalPosition = pivot + new Vector2(0f, -40f);
            hero.Velocity = Vector2.Zero;

            bool stood = false;
            bool touchedBar = false;
            float sideDrift = 0f;
            LedgeGrabPoint ledge = rope.Ledge;
            for (int frame = 0; frame < 150 && hero.CurrentState != CharacterState.LedgeHanging; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                if (hero.CurrentState == CharacterState.LedgeHanging) break;
                stood |= hero.IsOnFloor();
                int count = hero.GetSlideCollisionCount();
                for (int index = 0; index < count; index++) {
                    if (hero.GetSlideCollision(index)?.GetCollider() == rope) touchedBar = true;
                }
                sideDrift = Mathf.Max(sideDrift, Mathf.Abs(hero.GlobalPosition.X - pivot.X));
            }
            AssertThat(stood).OverrideFailureMessage("The hero stood on the rope bar.").IsFalse();
            AssertThat(touchedBar).OverrideFailureMessage("The hero collided with the rope bar.").IsFalse();
            AssertFloat(sideDrift)
                .OverrideFailureMessage($"The bar pushed the hero {sideDrift:0.0} px sideways.")
                .IsLess(1f);
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage($"The grab never caught the falling hero (state {hero.CurrentState}, feet {hero.GlobalPosition - pivot}).")
                .IsEqual(CharacterState.LedgeHanging);
            AssertObject(rope.Occupant).IsEqual(hero);
            AssertFloat(hero.GlobalPosition.DistanceTo(ledge.HangPosition)).IsLess(0.5f);
        } finally {
            InputManager.Instance.ClearInputSource(0);
            if (hero != null && GodotObject.IsInstanceValid(hero)) {
                hero.GetParent()?.RemoveChild(hero);
                hero.Free();
            }
            host.Free();
        }
    }

    private static void CheckRope(string label, PendulumAnchor rope, List<string> issues) {
        if (rope.CollisionLayer != 0u) issues.Add($"{label}: rope body on layer {rope.CollisionLayer} (must be 0)");
        if (rope.CollisionMask != 0u) issues.Add($"{label}: rope body scans mask {rope.CollisionMask} (must be 0)");
        LedgeGrabPoint ledge = rope.Ledge;
        if (ledge == null) {
            issues.Add($"{label}: no LedgeGrabPoint");
            return;
        }
        if (ledge.CollisionLayer != CollisionLayers.Trigger) issues.Add($"{label}: grab on layer {ledge.CollisionLayer}");
        if (ledge.CollisionMask != CollisionLayers.Player) issues.Add($"{label}: grab scans mask {ledge.CollisionMask}");
        if (!rope.CarryOccupant) issues.Add($"{label}: CarryOccupant is off");
    }
}
