using System;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// FIX-ENEMY (Phase C fix round of the 2026-10-12 gameplay update): Level 5's
/// listing ramps meet their decks flush. The bot batch stalled the staircase
/// lock in every Normal and Hard run: the hero stood "Running" at x 1080 against
/// D1's west face, 19 px above the ramp's top, and the lock's last slasher stood
/// at x 1219 against the ramp's rotated corner, 1.6 px proud of D1. Every ramp
/// had both faults (risers of 19–41 px at the top, lips of 0.5–1.6 px at a
/// corner). The ramps are now authored by their joints
/// (<see cref="Level05Controller.ListingRampJoints"/>). In real frames on the
/// real scene, the locks built Cleared through the test seam so nothing else
/// moves: the floor's top is continuous across every ramp, and a hero and a
/// slasher walk up and down every one without jumping.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level05ListingRampTests {

    private const string ScenePath = "res://scenes/campaign/Level_05_Titanic.tscn";
    private const int ScratchSlot = 2;
    /// <summary>
    /// How far past each joint a walk starts and must end, on the deck beyond it
    /// (the hero's 40 px body clear of the joint; short of extractor 2's 190 px
    /// discharge ring over E2).
    /// </summary>
    private const float Approach = 60f;

    /// <summary>
    /// How far past each joint a slasher starts: far enough that the walk down
    /// crosses where the old ramps' top corners stood proud of D1 and D2 (96 px
    /// east of the joint; the staircase's slasher stood at 1219 against one).
    /// </summary>
    private const float SlasherApproach = 120f;

    [TestCase(Timeout = 60000)]
    public async Task TheFloorsTopIsContinuousAcrossEveryListingRamp() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new TitanicFixture();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        PhysicsDirectSpaceState2D space = fixture.Level.GetWorld2D().DirectSpaceState;
        var query = new PhysicsRayQueryParameters2D {
            CollisionMask = CollisionLayers.Environment,
            CollideWithAreas = false,
            CollideWithBodies = true
        };
        foreach ((Vector2 low, Vector2 high) in Level05Controller.ListingRampJoints) {
            float slope = (low.Y - high.Y) / (high.X - low.X);
            float? previous = null;
            for (float x = low.X - Approach; x <= high.X + Approach; x += 1f) {
                // From just over the ramp's top end: under anything hung above it (an extractor).
                query.From = new Vector2(x, high.Y - 20f);
                query.To = new Vector2(x, low.Y + 200f);
                using Godot.Collections.Dictionary hit = space.IntersectRay(query);
                AssertThat(hit.Count > 0).OverrideFailureMessage($"No floor under x {x} at the ramp {low}..{high}.").IsTrue();
                float top = ((Vector2)hit["position"]).Y;
                if (previous is float before) {
                    // Rising sternward, a 1 px step may climb by the slope, never more,
                    // and never drop: a riser or a proud corner breaks it.
                    float rise = before - top;
                    AssertThat(rise >= -0.25f && rise <= slope + 0.25f)
                        .OverrideFailureMessage($"The floor's top steps by {rise:0.00} px at x {x} on the ramp {low}..{high} "
                            + $"(its slope climbs {slope:0.00} px per px).")
                        .IsTrue();
                }
                previous = top;
            }
            AssertThat(previous.Value).IsEqualApprox(high.Y, 0.5f);
        }
    }

    [TestCase(Timeout = 180000)]
    public async Task AHeroAndASlasherWalkUpAndDownEveryListingRampWithoutJumping() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new TitanicFixture();
        Level05Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var input = new BufferedInputSource();
        InputManager.Instance.SetInputSource(hero.PlayerIndex, input);
        uint tick = 0;
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            foreach ((Vector2 low, Vector2 high) in Level05Controller.ListingRampJoints) {
                string ramp = $"the ramp {low}..{high}";

                // The hero, up and down, never pressing Jump.
                Place(hero, new Vector2(low.X - Approach, low.Y));
                float highestFeet = low.Y;
                for (int frame = 0; frame < 300 && hero.GlobalPosition.X < high.X + Approach; frame++) {
                    input.SetNextFrame(PlayerInputFrame.Create(tick++, 1f, 0f, GameplayButtons.None));
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    AssertThat(tree.Paused).OverrideFailureMessage("Something paused the tree mid-walk.").IsFalse();
                    highestFeet = Mathf.Min(highestFeet, hero.GlobalPosition.Y);
                }
                AssertThat(hero.GlobalPosition.X >= high.X + Approach)
                    .OverrideFailureMessage($"Walking up {ramp}, the hero stopped at {hero.GlobalPosition}.")
                    .IsTrue();
                AssertThat(hero.GlobalPosition.Y).IsEqualApprox(high.Y, 1f);
                AssertThat(highestFeet >= high.Y - 3f)
                    .OverrideFailureMessage($"Walking up {ramp}, the hero's feet rose to {highestFeet:0.0}: it left the floor.")
                    .IsTrue();

                for (int frame = 0; frame < 300 && hero.GlobalPosition.X > low.X - Approach; frame++) {
                    input.SetNextFrame(PlayerInputFrame.Create(tick++, -1f, 0f, GameplayButtons.None));
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X <= low.X - Approach)
                    .OverrideFailureMessage($"Walking down {ramp}, the hero stopped at {hero.GlobalPosition}.")
                    .IsTrue();
                AssertThat(hero.GlobalPosition.Y).IsEqualApprox(low.Y, 1f);

                // A slasher chasing the standing hero, down then up.
                input.SetNextFrame(PlayerInputFrame.Create(tick++, 0f, 0f, GameplayButtons.None));
                hero.SetPhysicsProcess(false);
                try {
                    await WalkSlasher(level, hero, ramp,
                        from: new Vector2(high.X + SlasherApproach, high.Y), heroAt: new Vector2(low.X - 150f, low.Y),
                        reached: slasher => slasher.GlobalPosition.X <= low.X && Mathf.Abs(slasher.GlobalPosition.Y - low.Y) <= 1f);
                    await WalkSlasher(level, hero, ramp,
                        from: new Vector2(low.X - SlasherApproach, low.Y), heroAt: new Vector2(high.X + 150f, high.Y),
                        reached: slasher => slasher.GlobalPosition.X >= high.X && Mathf.Abs(slasher.GlobalPosition.Y - high.Y) <= 1f);
                } finally {
                    hero.SetPhysicsProcess(true);
                }
            }
        } finally {
            InputManager.Instance.ClearInputSource(hero.PlayerIndex);
        }
    }

    /// <summary>
    /// A slasher spawned at <paramref name="from"/> aggroes on the hero standing
    /// beside it, then chases the hero placed at <paramref name="heroAt"/> across
    /// the ramp until <paramref name="reached"/> holds (at most 6 s).
    /// </summary>
    private static async Task WalkSlasher(Level05Controller level, PlayerController hero, string ramp,
        Vector2 from, Vector2 heroAt, Func<EnemyController, bool> reached) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        EnemyController slasher = EnemyFactory.Spawn(Level05Controller.StandardEnemyID, level, from - new Vector2(0f, 2f));
        AssertObject(slasher).IsNotNull();
        slasher.SetPhysicsProcess(true);
        try {
            Place(hero, from + new Vector2(heroAt.X < from.X ? -200f : 200f, 0f));
            for (int frame = 0; frame < 10 && slasher.CurrentState != EnemyState.Chase; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(slasher.CurrentState).IsEqual(EnemyState.Chase);
            Place(hero, heroAt);
            for (int frame = 0; frame < 360 && !reached(slasher); frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(reached(slasher))
                .OverrideFailureMessage($"A slasher chasing the hero to {heroAt} across {ramp} stopped at {slasher.GlobalPosition}.")
                .IsTrue();
        } finally {
            PoolManager.Instance?.Release(slasher);
        }
    }

    private static void Place(PlayerController hero, Vector2 at) {
        hero.GlobalPosition = at;
        hero.Velocity = Vector2.Zero;
    }

    /// <summary>
    /// The real scene resumed at the entry anchor (so no beat pauses the tree)
    /// and then slotless for real frames, with every lock built Cleared through
    /// the test seam. Hands back the save slot, the session, the pause, the seam
    /// and the pooled bodies.
    /// </summary>
    private sealed class TitanicFixture : IDisposable {
        public readonly Level05Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public TitanicFixture() {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Titanic),
                LastCheckpointID = Level05Controller.Checkpoint0,
                CurrentHP = 100
            };
            EncounterLock.SuppressForTesting = true;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level05Controller>();
            Level.Name = "Level_05_Titanic_RampTest";
            tree.Root.AddChild(Level);
            // The resume has been read; real frames run slotless.
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        }

        public void Dispose() {
            EncounterLock.SuppressForTesting = false;
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            StoryEngagementDirector.ResetForTest();
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
