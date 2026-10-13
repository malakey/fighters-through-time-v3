using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-09 (VERIFY-L10-ARENA-RECESS-EXIT, boss half): every Story direct
/// position write meets the world through <see cref="SafeRelocation"/> — the
/// Teleport archetype (bosses, the Chrono-Warden's Phase Skip, the Rift
/// Phantom's rift step), SummonMinions spawn points and Einstein's Quantum
/// Entanglement blink. Against real Environment bodies: a landing stops short of
/// a wall and needs a floor under it; the Rift Phantom, a wall-phasing flyer,
/// keeps blinking through. The authored ranges are untouched — only where the
/// body lands is validated.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SafeRelocationTests {
    private const float Step = 1f / 60f;
    private const string BossScenePath = "res://scenes/enemies/Boss.tscn";
    /// <summary>Boss.tscn's body is 96 px wide.</summary>
    private const float BossHalfWidth = 48f;

    // === The Teleport archetype =========================================

    [TestCase]
    public async Task ABossTeleportTowardAWallLandsShortOfItOnItsFloor() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var origin = new Vector2(120000f, -50000f);
        Node host = CreateHost("RelocationWallHost");
        BuildBox(host, new Rect2(origin.X - 1000f, origin.Y, 3000f, 40f));
        float wallFace = origin.X + 300f;
        BuildBox(host, new Rect2(wallFace, origin.Y - 400f, 20f, 400f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                BossController boss = CreateBoss(host, ScratchBoss("relocation_wall_boss"), origin);
                // Exactly 200 px past a target 200 px east: 100 px into and past the wall.
                RunAbility(boss, TeleportAbility(200f, 200f), origin + new Vector2(200f, 0f));
                AssertFloat(boss.GlobalPosition.X)
                    .OverrideFailureMessage($"The teleport landed at {boss.GlobalPosition}, through the wall at x {wallFace}.")
                    .IsEqualApprox(wallFace - BossHalfWidth - RelocationRules.WallClearancePixels, 0.5f);
                AssertFloat(boss.GlobalPosition.Y).IsEqualApprox(origin.Y, 0.01f);
                AssertThat(boss.LastTeleportLanding.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
            });
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async Task ABossTeleportPastALedgeStepsBackOntoItsFloor() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var origin = new Vector2(126000f, -50000f);
        Node host = CreateHost("RelocationLedgeHost");
        float ledge = origin.X + 200f;
        BuildBox(host, new Rect2(origin.X - 1000f, origin.Y, ledge - (origin.X - 1000f), 40f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                BossController boss = CreateBoss(host, ScratchBoss("relocation_ledge_boss"), origin);
                RunAbility(boss, TeleportAbility(200f, 200f), origin + new Vector2(200f, 0f));
                AssertFloat(boss.GlobalPosition.X)
                    .OverrideFailureMessage($"The teleport landed at {boss.GlobalPosition}, over the void past the ledge at x {ledge}.")
                    .IsLessEqual(ledge);
                AssertFloat(boss.GlobalPosition.X).IsGreater(ledge - RelocationRules.StepBackPixels - 0.01f);
                AssertThat(boss.LastTeleportLanding.Outcome).IsEqual(RelocationOutcome.SteppedBackToSupport);
            });
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// A board at chest height — 15 px thick, lying between where the old rule's
    /// centre and top wall rays ran (64 and 108.8 px above the feet on the
    /// 128 px boss body) — is a wall to the body: the body's own collision box is
    /// swept along the travel, so the teleport stops short of the board's face
    /// instead of landing inside it.
    /// </summary>
    [TestCase]
    public async Task ABossTeleportStopsShortOfAThinBoardBetweenTheOldRayLines() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var origin = new Vector2(122000f, -50000f);
        Node host = CreateHost("RelocationThinBoardHost");
        BuildBox(host, new Rect2(origin.X - 1000f, origin.Y, 3000f, 40f));
        float boardFace = origin.X + 150f;
        BuildBox(host, new Rect2(boardFace, origin.Y - 95f, 450f, 15f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                BossController boss = CreateBoss(host, ScratchBoss("relocation_thin_board_boss"), origin);
                // 200 px past a target 200 px east: 250 px past the board's west face.
                RunAbility(boss, TeleportAbility(200f, 200f), origin + new Vector2(200f, 0f));
                AssertFloat(boss.GlobalPosition.X)
                    .OverrideFailureMessage(
                        $"The teleport landed at {boss.GlobalPosition} ({boss.LastTeleportLanding}), " +
                        $"through the board whose face is at x {boardFace}.")
                    .IsEqualApprox(boardFace - BossHalfWidth - RelocationRules.WallClearancePixels, 0.5f);
                AssertFloat(boss.GlobalPosition.Y).IsEqualApprox(origin.Y, 0.01f);
                AssertThat(boss.LastTeleportLanding.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
            });
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async Task AChronoWardenPhaseSkipStopsAtAWallAndARiftPhantomBlinksThroughOne() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var wardenAt = new Vector2(132000f, -50000f);
        var phantomAt = new Vector2(136000f, -50000f);
        Node host = CreateHost("RelocationEnemyHost");
        BuildBox(host, new Rect2(wardenAt.X - 1000f, wardenAt.Y, 2000f, 40f));
        float wardenWall = wardenAt.X + 150f;
        BuildBox(host, new Rect2(wardenWall, wardenAt.Y - 400f, 20f, 400f));
        BuildBox(host, new Rect2(phantomAt.X - 1000f, phantomAt.Y, 2000f, 40f));
        float phantomWall = phantomAt.X + 100f;
        BuildBox(host, new Rect2(phantomWall, phantomAt.Y - 400f, 20f, 400f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                // The Chrono-Warden (a grounded elite) skips away from a hero at
                // point blank: 200..360 px past her, through a wall 150 px out.
                EnemyController warden = EnemyFactory.Spawn("chrono_warden", host, wardenAt);
                EnemyAbilityData skip = AuthoredResources.Load<EnemyAbilityData>(
                    "res://resources/Enemies/Abilities/chrono_warden/phase_skip.tres");
                var wardenSkill = new EnemyAbilityExecutor(warden);
                float wardenHalfWidth = HalfWidthOf(warden);
                for (int cast = 0; cast < 6; cast++) {
                    warden.GlobalPosition = wardenAt;
                    wardenSkill.Begin(skip, wardenAt + new Vector2(60f, 0f), facingRight: true);
                    TickToIdle(wardenSkill);
                    AssertFloat(warden.GlobalPosition.X)
                        .OverrideFailureMessage(
                            $"Phase Skip landed at {warden.GlobalPosition}, through the wall at x {wardenWall}.")
                        .IsLessEqual(wardenWall - wardenHalfWidth - RelocationRules.WallClearancePixels + 0.5f);
                    AssertFloat(warden.GlobalPosition.Y).IsEqualApprox(wardenAt.Y, 0.01f);
                    AssertThat(wardenSkill.LastTeleportLanding.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
                }

                // The Rift Phantom's identity is rift-space: its engage blink still
                // lands 60..110 px past its target, on the far side of a wall.
                EnemyController phantom = EnemyFactory.Spawn("rift_phantom", host, phantomAt);
                AssertThat(phantom.Data.PhasesThroughWalls).IsTrue();
                EnemyAbilityData riftStep = AuthoredResources.Load<EnemyAbilityData>(
                    "res://resources/Enemies/Abilities/rift_phantom/rift_step.tres");
                var phantomSkill = new EnemyAbilityExecutor(phantom);
                Vector2 target = phantomAt + new Vector2(200f, -60f);
                phantomSkill.Begin(riftStep, target, facingRight: true);
                TickToIdle(phantomSkill);
                float past = phantom.GlobalPosition.X - target.X;
                AssertThat(past >= riftStep.TeleportRangeMin - 0.01f && past <= riftStep.TeleportRangeMax + 0.01f)
                    .OverrideFailureMessage($"The rift step landed {past} px past its target, outside its authored range.")
                    .IsTrue();
                AssertFloat(phantom.GlobalPosition.X).IsGreater(phantomWall + 20f);
                AssertThat(phantomSkill.LastTeleportLanding.Outcome).IsEqual(RelocationOutcome.AsRequested);
            });
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
        }
    }

    // === SummonMinions ==================================================

    [TestCase]
    public async Task ASummonBesideAWallSpawnsOnTheSummonersSideOnItsFloor() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var origin = new Vector2(140000f, -50000f);
        Node host = CreateHost("RelocationSummonHost");
        BuildBox(host, new Rect2(origin.X - 1000f, origin.Y, 2000f, 40f));
        float wallFace = origin.X + 70f;
        BuildBox(host, new Rect2(wallFace, origin.Y - 400f, 20f, 400f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                BossController boss = CreateBoss(host, ScratchBoss("relocation_summon_boss"), origin);
                var summon = new EnemyAbilityData {
                    AbilityID = "test.relocation_summon",
                    Archetype = EnemyAbilityArchetype.SummonMinions,
                    RangeClass = EnemyAbilityRangeClass.Any,
                    TelegraphFrames = 0,
                    ActiveFrames = 2,
                    RecoveryFrames = 0,
                    SummonEnemyID = "holo_page",
                    SummonCount = 4
                };
                RunAbility(boss, summon, origin + new Vector2(-300f, 0f));

                var minions = new List<EnemyController>();
                Godot.Collections.Array<Node> children = host.GetChildren();
                using (children.AsDisposable()) {
                    foreach (Node child in children) {
                        if (child is EnemyController minion) minions.Add(minion);
                    }
                }
                AssertThat(minions.Count).IsEqual(4);
                foreach (EnemyController minion in minions) {
                    float limit = wallFace - HalfWidthOf(minion) - RelocationRules.WallClearancePixels + 0.5f;
                    AssertFloat(minion.GlobalPosition.X)
                        .OverrideFailureMessage(
                            $"A summon spawned at {minion.GlobalPosition}, behind the wall at x {wallFace}.")
                        .IsLessEqual(limit);
                    AssertFloat(minion.GlobalPosition.Y).IsEqualApprox(origin.Y, 0.01f);
                    AssertThat(minion.IsSummoned).IsTrue();
                }
                // The west-side summons keep their authored offsets (96 and 144 px).
                int west = 0;
                foreach (EnemyController minion in minions) {
                    float offset = minion.GlobalPosition.X - origin.X;
                    if (Mathf.IsEqualApprox(offset, -96f) || Mathf.IsEqualApprox(offset, -144f)) west++;
                }
                AssertThat(west).IsEqual(2);
            });
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
        }
    }

    /// <summary>
    /// The boss stands on a ledge with its centre 30 px past the edge (the west
    /// 18 px of its 96 px body on the floor) and summons four 46 px Holo Pages.
    /// Every page lands with its own centre over the floor: the two summoned east,
    /// over the void, emerge from the part of the boss that stands on something
    /// instead of being judged by their width around the boss's unsupported feet,
    /// which read as "already over nothing" and dropped them into the void. The
    /// two summoned west, over the floor, keep their authored offsets.
    /// </summary>
    [TestCase]
    public async Task ANarrowSummonFromABossOnALedgeLandsOnTheFloor() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var origin = new Vector2(142000f, -50000f);
        Node host = CreateHost("RelocationLedgeSummonHost");
        float ledge = origin.X - 30f;
        // The floor ends at the ledge; east of it there is nothing at all.
        BuildBox(host, new Rect2(origin.X - 1000f, origin.Y, ledge - (origin.X - 1000f), 40f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                BossController boss = CreateBoss(host, ScratchBoss("relocation_ledge_summon_boss"), origin);
                var summon = new EnemyAbilityData {
                    AbilityID = "test.relocation_ledge_summon",
                    Archetype = EnemyAbilityArchetype.SummonMinions,
                    RangeClass = EnemyAbilityRangeClass.Any,
                    TelegraphFrames = 0,
                    ActiveFrames = 2,
                    RecoveryFrames = 0,
                    SummonEnemyID = "holo_page",
                    SummonCount = 4
                };
                RunAbility(boss, summon, origin + new Vector2(300f, 0f));

                var minions = new List<EnemyController>();
                Godot.Collections.Array<Node> children = host.GetChildren();
                using (children.AsDisposable()) {
                    foreach (Node child in children) {
                        if (child is EnemyController minion) minions.Add(minion);
                    }
                }
                AssertThat(minions.Count).IsEqual(4);
                int west = 0;
                foreach (EnemyController minion in minions) {
                    AssertFloat(HalfWidthOf(minion)).IsLess(30f);
                    AssertFloat(minion.GlobalPosition.X)
                        .OverrideFailureMessage(
                            $"A summon spawned at {minion.GlobalPosition}, over the void past the ledge at x {ledge}.")
                        .IsLessEqual(ledge);
                    AssertFloat(minion.GlobalPosition.Y).IsEqualApprox(origin.Y, 0.01f);
                    float offset = minion.GlobalPosition.X - origin.X;
                    if (Mathf.IsEqualApprox(offset, -96f) || Mathf.IsEqualApprox(offset, -144f)) west++;
                }
                AssertThat(west).IsEqual(2);
            });
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
        }
    }

    // === Quantum Entanglement ===========================================

    [TestCase]
    public async Task QuantumEntanglementBlinksItsFullDistanceInTheOpenAndStopsShortOfAWall() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var open = new Vector2(146000f, -50000f);
        var walled = new Vector2(150000f, -50000f);
        Node host = CreateHost("RelocationQuantumHost");
        BuildBox(host, new Rect2(open.X - 1000f, open.Y, 2000f, 40f));
        BuildBox(host, new Rect2(walled.X - 1000f, walled.Y, 2000f, 40f));
        float wallFace = walled.X - 100f;
        BuildBox(host, new Rect2(wallFace - 20f, walled.Y - 400f, 20f, 400f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                (PlayerController einstein, BlockSystem block) = SpawnEinstein(host, open);
                block.DepleteCharges(block.CurrentCharges);
                AssertThat(einstein.CurrentState).IsEqual(CharacterState.Dazed);
                AssertFloat(einstein.GlobalPosition.X)
                    .IsEqualApprox(open.X - BlockSystem.QuantumEntanglementDistance, 0.5f);
                AssertThat(block.LastQuantumEntanglementLanding.Outcome).IsEqual(RelocationOutcome.AsRequested);

                // Facing right, he blinks west — into a wall 100 px behind him.
                einstein.GlobalPosition = walled;
                einstein.Velocity = Vector2.Zero;
                block.RestoreAllCharges();
                block.DepleteCharges(block.CurrentCharges);
                float halfWidth = HalfWidthOf(einstein);
                AssertFloat(einstein.GlobalPosition.X)
                    .OverrideFailureMessage(
                        $"The blink landed at {einstein.GlobalPosition}, through the wall whose face is at x {wallFace}.")
                    .IsEqualApprox(wallFace + halfWidth + RelocationRules.WallClearancePixels, 0.5f);
                AssertThat(block.LastQuantumEntanglementLanding.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
            });
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// A shatter normally resolves inside a hit's physics flush, where the space
    /// cannot be asked. The blink must not land unvalidated there: it lands on
    /// the deferred call right after the flush, against the wall.
    /// </summary>
    [TestCase]
    public async Task AShatterInsideAHitFlushBlinksRightAfterItAndStillStopsAtTheWall() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var walled = new Vector2(154000f, -50000f);
        Node host = CreateHost("RelocationQuantumFlushHost");
        BuildBox(host, new Rect2(walled.X - 1000f, walled.Y, 2000f, 40f));
        float wallFace = walled.X - 100f;
        BuildBox(host, new Rect2(wallFace - 20f, walled.Y - 400f, 20f, 400f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            (PlayerController einstein, BlockSystem block) = SpawnEinstein(host, walled);
            einstein.ProcessMode = Node.ProcessModeEnum.Disabled;
            using (PhysicsCallbackGuard.Enter()) {
                block.DepleteCharges(block.CurrentCharges);
                AssertThat(einstein.GlobalPosition)
                    .OverrideFailureMessage("Inside the flush the blink must wait for a queryable space.")
                    .IsEqual(walled);
            }
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertFloat(einstein.GlobalPosition.X)
                .OverrideFailureMessage(
                    $"After the flush the blink landed at {einstein.GlobalPosition}; the wall's face is at x {wallFace}.")
                .IsEqualApprox(wallFace + HalfWidthOf(einstein) + RelocationRules.WallClearancePixels, 0.5f);
            AssertThat(block.LastQuantumEntanglementLanding.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
        } finally {
            host.Free();
        }
    }

    // === Helpers =========================================================

    private static Node CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    /// <summary>A solid Environment box covering <paramref name="rect"/> (global).</summary>
    private static void BuildBox(Node host, Rect2 rect) {
        var body = new StaticBody2D {
            Name = "RelocationBox",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = rect.GetCenter()
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
        host.AddChild(body);
    }

    private static BossData ScratchBoss(string id) => new() {
        BossID = id,
        DisplayNameKey = "boss",
        MaxHP = 100,
        MoveSpeed = 4f,
        MeleeRangeThreshold = 2f,
        RangedRangeThreshold = 4f,
        ChronalDustDrop = 0
    };

    private static EnemyAbilityData TeleportAbility(float min, float max) => new() {
        AbilityID = "test.relocation_teleport",
        Archetype = EnemyAbilityArchetype.Teleport,
        RangeClass = EnemyAbilityRangeClass.Any,
        TelegraphFrames = 0,
        ActiveFrames = 2,
        RecoveryFrames = 0,
        TeleportRangeMin = min,
        TeleportRangeMax = max
    };

    /// <summary>The body spawns at <paramref name="at"/> (its spawn point) with physics off; the test drives it.</summary>
    private static BossController CreateBoss(Node host, BossData data, Vector2 at) {
        var boss = ResourceLoader.Load<PackedScene>(BossScenePath).Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        boss.Position = at;
        boss.ProcessMode = Node.ProcessModeEnum.Disabled;
        host.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    private static void RunAbility(BossController boss, EnemyAbilityData ability, Vector2 target) {
        boss.BeginAbility(ability, target);
        for (int frame = 0; frame < 240 && boss.AbilityPhase != EnemyAbilityPhase.Idle; frame++) {
            boss.TickAbility(Step);
        }
    }

    private static void TickToIdle(EnemyAbilityExecutor executor) {
        for (int frame = 0; frame < 240 && executor.IsBusy; frame++) executor.Tick(Step);
    }

    private static (PlayerController einstein, BlockSystem block) SpawnEinstein(Node host, Vector2 at) {
        PlayerController einstein = CharacterFactory.CreateCharacter("einstein", 0);
        host.AddChild(einstein);
        einstein.GlobalPosition = at;
        einstein.IsFacingRight = true;
        einstein.StoryAbilityPerks.Add(BlockSystem.QuantumEntanglementPerkKey);
        BlockSystem block = einstein.GetNode<BlockSystem>("BlockSystem");
        AssertThat(block.CurrentCharges).IsGreater(0);
        return (einstein, block);
    }

    private static float HalfWidthOf(Node body) {
        RelocationBody? shape = SafeRelocation.BodyOf(body, needsFloor: true, passesWalls: false);
        AssertThat(shape.HasValue).IsTrue();
        return shape.Value.HalfWidth;
    }
}
