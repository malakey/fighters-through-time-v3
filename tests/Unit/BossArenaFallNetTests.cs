using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-09 (VERIFY-L10-ARENA-RECESS-EXIT, boss half): the safety net under
/// the relocation rule. A leashed boss — and the Level 13 Mirror clone, which has
/// its own leash — whose feet end a frame below its arena's bottom edge has left
/// the world (the Tragedy King under the Level 10 boards fell forever and the
/// level could not be won). It is put back on its authored anchor with no damage
/// and no HP, phase or reward change, once per fall, and T01b's historical
/// recovery is left exactly as it was.
///
/// <para>VERIFY-L04-PIT-FALL-NET (2026-10-09): "below its arena" means past
/// <see cref="BossController.ArenaFallNetLineY"/> — the bottom edge plus
/// <see cref="BossController.ArenaFallNetMarginPixels"/>, deeper than any
/// walkable floor authored under an arena's edge (Level 4's Tribunal pit floor,
/// 40 px under it, sent the surviving squad member back to its spawn).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossArenaFallNetTests {
    private const float Step = 1f / 60f;
    private const string BossScenePath = "res://scenes/enemies/Boss.tscn";

    /// <summary>
    /// Real physics: a leashed boss stepped off its spawn platform falls through
    /// the open pit beside it. No frame ends with it below the arena, and it is
    /// returned exactly once — to the spawn point, where it then stands.
    /// </summary>
    [TestCase]
    public async Task ALeashedBossThatFallsOutOfItsArenaIsReturnedToItsSpawnUnharmed() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var spawn = new Vector2(160000f, -50000f);
        var arena = new Rect2(spawn.X - 600f, spawn.Y - 1000f, 1200f, 1300f);
        Node host = CreateHost("ArenaFallHost");
        // The spawn platform; beside it, nothing down to the arena's bottom and beyond.
        BuildBox(host, new Rect2(spawn.X - 100f, spawn.Y, 200f, 40f));
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                BossController boss = CreateBoss(host, ScratchBoss("arena_fall_boss"), spawn, physics: true);
                boss.ArenaBounds = arena;
                boss.IsEngaged = false;
                int hp = boss.CurrentHP;
                float netLine = BossController.ArenaFallNetLineY(arena);
                boss.GlobalPosition = new Vector2(spawn.X + 400f, spawn.Y);
                boss.Velocity = Vector2.Zero;

                bool returned = false;
                for (int frame = 0; frame < 600; frame++) {
                    boss._PhysicsProcess(Step);
                    AssertFloat(boss.GlobalPosition.Y)
                        .OverrideFailureMessage(
                            $"frame {frame}: the boss ended a frame at {boss.GlobalPosition}, below its arena's fall net (y {netLine}).")
                        .IsLessEqual(netLine);
                    if (boss.ArenaFallRecoveries > 0 && !returned) {
                        returned = true;
                        AssertThat(boss.GlobalPosition).IsEqual(spawn);
                        AssertThat(boss.Velocity).IsEqual(Vector2.Zero);
                    }
                }
                AssertThat(boss.ArenaFallRecoveries).IsEqual(1);
                AssertThat(boss.LastArenaFallAnchor).IsEqual(spawn);
                AssertFloat(boss.GlobalPosition.X).IsEqualApprox(spawn.X, 0.5f);
                AssertFloat(boss.GlobalPosition.Y).IsEqualApprox(spawn.Y, 1f);
                AssertThat(boss.CurrentHP).IsEqual(hp);
                AssertThat(boss.CurrentPhase).IsEqual(0);
                AssertThat(boss.CurrentState == BossState.Dead).IsFalse();
            });
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// VERIFY-L04-PIT-FALL-NET: feet a pit floor's depth under the arena's bottom
    /// edge (Level 4's Tribunal pit, 40 px) are standing, not falling — the net
    /// leaves them alone; feet past the net line are caught.
    /// </summary>
    [TestCase]
    public void FeetAPitFloorBelowTheArenaEdgeAreNotAFall() {
        Node host = CreateHost("ArenaFallShallowHost");
        try {
            var spawn = new Vector2(176000f, -50000f);
            var arena = new Rect2(spawn.X - 600f, spawn.Y - 1000f, 1200f, 1300f);
            BossController boss = CreateBoss(host, ScratchBoss("arena_fall_shallow_boss"), spawn);
            boss.ArenaBounds = arena;
            float pitDepth = FTT.Environment.Level04Controller.TribunalPitFloorY
                - FTT.Environment.Level04Controller.BossArenaBounds.End.Y;
            AssertFloat(pitDepth).IsGreater(0f);
            AssertFloat(BossController.ArenaFallNetMarginPixels).IsGreater(pitDepth);

            var onPitFloor = new Vector2(spawn.X + 200f, arena.End.Y + pitDepth);
            boss.GlobalPosition = onPitFloor;
            boss.RecoverFromArenaFallForTest();
            AssertThat(boss.ArenaFallRecoveries).IsEqual(0);
            AssertThat(boss.GlobalPosition).IsEqual(onPitFloor);

            boss.GlobalPosition = new Vector2(spawn.X + 200f, BossController.ArenaFallNetLineY(arena) - 0.5f);
            boss.RecoverFromArenaFallForTest();
            AssertThat(boss.ArenaFallRecoveries).IsEqual(0);

            boss.GlobalPosition = new Vector2(spawn.X + 200f, BossController.ArenaFallNetLineY(arena) + 0.5f);
            boss.RecoverFromArenaFallForTest();
            AssertThat(boss.ArenaFallRecoveries).IsEqual(1);
            AssertThat(boss.GlobalPosition).IsEqual(spawn);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AnAuthoredSafeAnchorWinsAndTheHistoricalRecoveryIsUntouched() {
        Node host = CreateHost("ArenaFallAnchorHost");
        try {
            var spawn = new Vector2(164000f, -50000f);
            var arena = new Rect2(spawn.X - 600f, spawn.Y - 1000f, 1200f, 1300f);
            BossData data = ScratchBoss("arena_fall_anchor_boss");
            data.HasHistoricalRecovery = true;
            var anchor = new Node2D { Name = "SafeAnchor", Position = spawn + new Vector2(-300f, -40f) };
            host.AddChild(anchor);
            BossController boss = CreateBoss(host, data, spawn);
            boss.HistoricalRecoveryAnchor = anchor;
            boss.ArenaBounds = arena;
            int hp = boss.CurrentHP;
            int samples = boss.HistorySampleCount;

            boss.GlobalPosition = new Vector2(spawn.X + 200f, BossController.ArenaFallNetLineY(arena) + 50f);
            boss.Velocity = new Vector2(0f, 900f);
            boss.RecoverFromArenaFallForTest();
            AssertThat(boss.GlobalPosition).IsEqual(anchor.GlobalPosition);
            AssertThat(boss.Velocity).IsEqual(Vector2.Zero);
            AssertThat(boss.ArenaFallRecoveries).IsEqual(1);
            AssertThat(boss.CurrentHP).IsEqual(hp);
            AssertThat(boss.HistoricalRecoveryUsed).IsFalse();
            AssertThat(boss.HistorySampleCount).IsEqual(samples);

            // Back inside: nothing more happens.
            boss.RecoverFromArenaFallForTest();
            AssertThat(boss.ArenaFallRecoveries).IsEqual(1);
            AssertThat(boss.GlobalPosition).IsEqual(anchor.GlobalPosition);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AnUnleashedBossIsNeverMovedByTheNet() {
        Node host = CreateHost("ArenaFallUnleashedHost");
        try {
            var spawn = new Vector2(168000f, -50000f);
            BossController boss = CreateBoss(host, ScratchBoss("arena_fall_unleashed_boss"), spawn);
            AssertThat(boss.HasArenaLeash).IsFalse();
            var deep = new Vector2(spawn.X, spawn.Y + 100000f);
            boss.GlobalPosition = deep;
            boss.RecoverFromArenaFallForTest();
            AssertThat(boss.GlobalPosition).IsEqual(deep);
            AssertThat(boss.ArenaFallRecoveries).IsEqual(0);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheMirrorCloneThatFallsOutOfItsArenaIsReturnedToItsSpawn() {
        var spawn = new Vector2(172000f, -50000f);
        var arena = new Rect2(spawn.X - 600f, spawn.Y - 1000f, 1200f, 1300f);
        var mirror = new MirrorParadoxController {
            Name = "ArenaFallMirror",
            Data = AuthoredResources.Load<BossData>("res://resources/Bosses/mirror_paradox.tres"),
            CharacterIDOverride = "einstein",
            DecisionSeed = 4244,
            // The clamp and the net only — never an AI-driven fight.
            ActivateOnSpawn = false,
            ArenaBounds = arena,
            Position = spawn
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(mirror);
        try {
            PlayerController clone = mirror.Clone;
            AssertObject(clone).IsNotNull();
            int hp = clone.CurrentHP;
            clone.GlobalPosition = new Vector2(spawn.X + 200f, BossController.ArenaFallNetLineY(arena) + 40f);
            clone.Velocity = new Vector2(0f, 900f);
            mirror.ApplyArenaLeashForTest();
            AssertThat(clone.GlobalPosition).IsEqual(spawn);
            AssertThat(clone.Velocity).IsEqual(Vector2.Zero);
            AssertThat(mirror.ArenaFallRecoveries).IsEqual(1);
            AssertThat(clone.CurrentHP).IsEqual(hp);

            mirror.ApplyArenaLeashForTest();
            AssertThat(mirror.ArenaFallRecoveries).IsEqual(1);
        } finally {
            if (GodotObject.IsInstanceValid(mirror)) {
                mirror.GetParent()?.RemoveChild(mirror);
                mirror.Free();
            }
        }
    }

    // === Helpers =========================================================

    private static Node CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static void BuildBox(Node host, Rect2 rect) {
        var body = new StaticBody2D {
            Name = "ArenaFallBox",
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

    /// <summary>
    /// The body spawns at <paramref name="at"/> — its spawn point. Without
    /// <paramref name="physics"/> it is processing-disabled (out of the physics
    /// space) and the test calls the net directly.
    /// </summary>
    private static BossController CreateBoss(Node host, BossData data, Vector2 at, bool physics = false) {
        var boss = ResourceLoader.Load<PackedScene>(BossScenePath).Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        boss.Position = at;
        if (!physics) boss.ProcessMode = Node.ProcessModeEnum.Disabled;
        host.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }
}
