using System;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-LV1: Level 1's Rooftop Run, its route and the workshop's swarm on the real scene. The
/// shared rules are <c>TraversalStretchContentTests</c>; these are Florence's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level01TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_01_Florence.tscn";

    /// <summary>The stretch took the unlocked gap: no lock moved, and the first Extractor went to the route's end.</summary>
    [TestCase]
    public void TheRooftopRunTookTheGapAndOnlyTheFirstExtractorMoved() {
        using var fixture = new Fixture("einstein");
        Level01Controller level = fixture.Level;
        AssertThat(level.PiazzaLock.Area.End.X).IsEqual(4430f);
        AssertThat(level.PrintShopLock.Area.Position.X).IsEqual(5800f);
        AssertThat(level.WorkshopLock.Area.End.X).IsEqual(10460f);

        TraversalStretch run = level.RooftopRun;
        AssertThat(run.MinX).IsEqual(4450f);
        AssertThat(run.MaxX).IsEqual(5780f);
        AssertThat(run.HasTrench).IsTrue();
        AssertThat(run.TrenchFloorY).IsEqual(1020f);

        OptionalRoute route = level.ChimneyLine;
        AssertThat(route.RewardSourceID).IsEqual(Level01Controller.RooftopRouteRewardID);
        AssertThat(route.HasBurstShortcut).IsFalse();
        AssertThat(route.HasRiseShortcut).IsFalse();
        ChronalExtractor first = level.Extractors.Single(extractor => extractor.ObjectID == Level01Controller.RooftopRouteRewardID);
        AssertThat(first.Position).IsEqual(Level01Controller.RooftopRouteRewardPosition);

        // Unmoved: the anchors, the font and the arena.
        AssertThat(Level01Controller.MiddleCheckpointX).IsEqual(8840f);
        AssertThat(Level01Controller.RestorationFontPosition).IsEqual(new Vector2(10530f, 900f));
        AssertThat(Level01Controller.BossArenaBounds.Position.X).IsEqual(10740f);
    }

    /// <summary>
    /// A hero in the trench is never stuck, in real frames: Lincoln walks down the west ramp
    /// and back out, and up the east ramp to the deck, where a double jump clears the wall.
    /// His longest hop on the route is real too.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksOutOfTheTrenchClearsTheWallAndTheRoutesLongestHop() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using (var fixture = new Fixture("lincoln")) {
            PlayerController hero = fixture.Level.GetNode<PlayerController>("Player");
            var source = new BufferedInputSource();
            try {
                await Settle(tree);
                InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
                hero.GlobalPosition = new Vector2(4470f, 890f);
                hero.Velocity = Vector2.Zero;

                // Down the west ramp, stopping short of the trench's spike strip (4950-5050).
                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                float deepest = 0f;
                for (int frame = 0; frame < 300 && hero.GlobalPosition.X < 4900f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
                }
                AssertThat(hero.GlobalPosition.X >= 4900f).OverrideFailureMessage($"Lincoln stalled at {hero.GlobalPosition}.").IsTrue();
                AssertThat(deepest >= 1018f).OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();

                source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 300 && hero.GlobalPosition.X > 4480f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X <= 4480f && hero.GlobalPosition.Y <= 901f)
                    .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();

                // The east half: from the trench floor, up the ramp, to the wall on the deck.
                hero.GlobalPosition = new Vector2(5360f, 1010f);
                hero.Velocity = Vector2.Zero;
                source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                for (int frame = 0; frame < 240 && hero.GlobalPosition.X < 5640f; frame++) {
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X >= 5640f && hero.GlobalPosition.Y <= 901f)
                    .OverrideFailureMessage($"Lincoln did not walk out by the east ramp: {hero.GlobalPosition}.").IsTrue();

                // A double jump, right held, clears the 210 px wall.
                GameplayButtons held = GameplayButtons.None;
                int stage = 0;
                for (int frame = 0; frame < 180 && !(hero.GlobalPosition.X > 5715f && hero.IsOnFloor()); frame++) {
                    GameplayButtons buttons = GameplayButtons.None;
                    switch (stage) {
                        case 0: buttons = GameplayButtons.Jump; if (!hero.IsOnFloor()) stage = 1; break;
                        case 1: buttons = GameplayButtons.Jump; if (hero.Velocity.Y >= 0f) stage = 2; break;
                        case 2: stage = 3; break;
                        default: buttons = GameplayButtons.Jump; break;
                    }
                    source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, buttons, held));
                    held = buttons;
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                }
                AssertThat(hero.GlobalPosition.X > 5715f && hero.GlobalPosition.Y <= 901f)
                    .OverrideFailureMessage($"Lincoln did not clear the deck wall: {hero.GlobalPosition}.").IsTrue();
            } finally {
                InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            }
        }

        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 1).Routes.Single();
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", route.BaseLine[^2], route.BaseLine[^1], TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    /// <summary>
    /// The 7d showcase on the real scene. The row comes once two of the workshop's men are left.
    /// A walking swarmling stands safe in a spike strip and in the live geyser. Launched, it dies
    /// to each of them: flown into a strip, launched from inside a strip (which BodyEntered alone
    /// misses) and flown into the scene's steam geyser. No kill pays dust (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheWorkshopSwarmComesAtTwoAliveAndLaunchedFodderDiesOnEveryHazard() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level01Controller level = fixture.Level;
        PlayerController hero = level.GetNode<PlayerController>("Player");
        try {
            await Settle(tree);
            level.StreetsLock.RestoreCleared();
            level.PiazzaLock.RestoreCleared();
            level.PrintShopLock.RestoreCleared();
            hero.GlobalPosition = new Vector2(9180f, 850f);
            hero.Velocity = Vector2.Zero;
            hero.ScriptedHPFloor = 1;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            EncounterLock workshop = level.WorkshopLock;
            AssertThat(workshop.State).IsEqual(EncounterLockState.Engaged);
            int soldiers = workshop.LivingMemberCount;
            AssertThat(soldiers).IsEqual(Level01Controller.Room3Spawns.Length);

            // Three left: no swarm yet.
            foreach (EnemyController soldier in workshop.LivingMembers.Take(soldiers - 3).ToArray()) soldier.TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(workshop.LivingMemberCount).IsEqual(3);

            // One more down: two left, so the row comes.
            workshop.LivingMembers.First().TakeDamage(99999);
            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            int row = Level01Controller.SwarmSpawnXs.Length;
            AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(workshop.Area.Size.X));
            AssertThat(workshop.LivingMemberCount).IsEqual(2 + row);
            EnemyController[] fodder = workshop.LivingMembers.Where(member => member.Data.DustWeightZero).ToArray();
            AssertThat(fodder.Length).IsEqual(row);

            int wallet = StoryManager.Instance.ChronalDustCollected;
            int pickupsBefore = CountPickups(tree);

            // Walking: safe in a spike strip. It starts at the strip's east end and walks west, toward the hero.
            EnemyController walker = fodder[0];
            walker.GlobalPosition = new Vector2(9570f, 880f);
            for (int frame = 0; frame < 8; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(walker.CurrentState == EnemyState.Dead).OverrideFailureMessage("A walking mob died in a spike strip.").IsFalse();
            AssertThat(walker.GlobalPosition.X > 9520f && walker.GlobalPosition.X < 9580f)
                .OverrideFailureMessage($"The walker left the strip before its launch: {walker.GlobalPosition}.").IsTrue();

            // Launched from where it stands, inside that strip: no BodyEntered will come, and it still dies.
            walker.LaunchThrown(new Vector2(0f, -120f), 0, 0);
            await AssertDiesByHazard(tree, walker, "launched from inside the spike strip");

            // Flown into a strip from outside it.
            EnemyController thrown = fodder[1];
            thrown.GlobalPosition = new Vector2(9460f, 880f);
            thrown.LaunchThrown(new Vector2(600f, -60f), 0, 0);
            await AssertDiesByHazard(tree, thrown, "flown into the spike strip");

            // The scene's steam geyser. Walking in its live column: safe.
            StoryCyclicHazard geyser = level.GetNode<StoryCyclicHazard>("SteamGeyser");
            AssertThat(geyser.Position).IsEqual(new Vector2(9920f, 890f));
            geyser.ForcePhase(HazardPhase.Active, 5f);
            EnemyController steamWalker = fodder[2];
            steamWalker.GlobalPosition = new Vector2(9920f, 880f);
            for (int frame = 0; frame < 8; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(steamWalker.CurrentState == EnemyState.Dead).OverrideFailureMessage("A walking mob died in the live geyser.").IsFalse();

            // Flown into it, with the column live.
            EnemyController steamed = fodder[3];
            steamed.GlobalPosition = new Vector2(9800f, 880f);
            steamed.LaunchThrown(new Vector2(600f, -200f), 0, 0);
            await AssertDiesByHazard(tree, steamed, "flown into the live geyser");

            for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);
            AssertThat(CountPickups(tree)).IsEqual(pickupsBefore);
        } finally {
            hero.ScriptedHPFloor = 0;
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    private static async Task AssertDiesByHazard(SceneTree tree, EnemyController mob, string how) {
        for (int frame = 0; frame < 45 && !mob.KilledByHazard; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(mob.KilledByHazard)
            .OverrideFailureMessage($"A swarmling {how} did not die to the hazard: at {mob.GlobalPosition}, state {mob.CurrentState}, HP {mob.CurrentHP}.")
            .IsTrue();
    }

    private static int CountPickups(SceneTree tree) {
        Godot.Collections.Array<Node> pickups = tree.GetNodesInGroup("chronal_dust");
        using var lifetime = pickups.AsDisposable();
        return pickups.Count;
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    /// <summary>The real scene against no save on Normal, with the session handed back.</summary>
    private sealed class Fixture : IDisposable {
        public readonly Level01Controller Level;
        private readonly SessionData _session;
        private readonly bool _paused;

        public Fixture(string heroID) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _paused = tree.Paused;
            _session = GameManager.Instance.CurrentSession;
            SessionData session = _session;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = heroID;
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level01Controller>();
            Level.Name = "Florence_TraversalFixture";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveUnder(Level);
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession = _session;
            ((SceneTree)Engine.GetMainLoop()).Paused = _paused;
        }
    }
}
