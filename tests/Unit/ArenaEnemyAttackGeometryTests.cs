using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Playtest pass 2026-10-04, workstream ARENA — P2 and P3.
///
/// <para><b>P2.</b> An enemy or boss forward hitbox (MeleeStrike / ChargeDash)
/// stops at the first Environment wall in front of its owner: the Siegemaster
/// Duke's Siege Smash used to land on a player standing on the far side of the
/// 20 px battlement footing. A swing whose whole forward reach is behind the wall
/// lands nothing; a charge re-clips every active frame.</para>
///
/// <para><b>P3.</b> A shot re-samples the telegraphed target at fire time, aims at
/// its body (not its feet) and leads it by its horizontal velocity over the
/// flight time, capped; a target that crossed to the shooter's other side keeps
/// the telegraphed aim.</para>
///
/// <para><b>M1</b> (the 2026-10-04 fix pass): the player side of P2 — a basic
/// swing lands on an enemy or boss only with a clear Environment line from the
/// swinger's body centre to the hurtbox.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ArenaEnemyAttackGeometryTests {
    private const float Step = 1f / 60f;

    // === P2: the pure clip rule =========================================

    [TestCase]
    public void TheClipRuleShortensASpanAtTheWallAndRefusesOneEntirelyBehindIt() {
        // No wall, or a wall beyond the far edge: untouched.
        AssertThat(EnemyAttackGeometryRules.ClipForwardSpan(20f, 132f, -1f, out float near, out float far)).IsTrue();
        AssertFloat(near).IsEqual(20f);
        AssertFloat(far).IsEqual(132f);
        AssertThat(EnemyAttackGeometryRules.ClipForwardSpan(20f, 132f, 200f, out _, out far)).IsTrue();
        AssertFloat(far).IsEqual(132f);

        // A wall inside the reach: the far edge stops short of the face.
        AssertThat(EnemyAttackGeometryRules.ClipForwardSpan(20f, 132f, 60f, out near, out far)).IsTrue();
        AssertFloat(near).IsEqual(20f);
        AssertFloat(far).IsEqual(60f - EnemyAttackGeometryRules.WallClearancePixels);

        // A wall closer than the near edge: no forward reach is left.
        AssertThat(EnemyAttackGeometryRules.ClipForwardSpan(20f, 132f, 15f, out _, out _)).IsFalse();

        // A box straddling the owner keeps its share up to the wall, but a wall
        // flush against the owner's face leaves nothing in front of it.
        AssertThat(EnemyAttackGeometryRules.ClipForwardSpan(-30f, 50f, 25f, out near, out far)).IsTrue();
        AssertFloat(near).IsEqual(-30f);
        AssertFloat(far).IsEqual(25f - EnemyAttackGeometryRules.WallClearancePixels);
        AssertThat(EnemyAttackGeometryRules.ClipForwardSpan(-30f, 50f, 1f, out _, out _)).IsFalse();
    }

    // === P2: the executor against a real wall ===========================

    [TestCase]
    public async Task AForwardSwingStopsAtAWallAndASwingFlushAgainstOneLandsNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "ArenaWallClipHost" };
        tree.Root.AddChild(host);
        // Far from the origin so no other suite's geometry can share the rays.
        var ownerPosition = new Vector2(40000f, -20000f);
        StaticBody2D wall = BuildWall(host, ownerPosition.X + 60f, ownerPosition.Y);
        try {
            // One physics frame so the space has registered the wall.
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox) = CreateSubject(host, ownerPosition);
                // The Siege Smash shape: 112 x 96 at (76, -60) — edges 20..132 ahead.
                EnemyAbilityData smash = Melee(new Vector2(112f, 96f), new Vector2(76f, -60f), active: 6);
                var shape = (RectangleShape2D)hitbox.GetNode<CollisionShape2D>("CollisionShape2D").Shape;

                // Facing away from the wall: the full authored box.
                executor.Begin(smash, ownerPosition + new Vector2(-200f, 0f), facingRight: false);
                AssertThat(hitbox.IsActive).IsTrue();
                AssertFloat(shape.Size.X).IsEqualApprox(112f, 0.01f);
                AssertFloat(hitbox.Position.X).IsEqualApprox(-76f, 0.01f);
                executor.Cancel();

                // Facing the wall 60 px away: the far edge stops at the face.
                executor.Begin(smash, ownerPosition + new Vector2(200f, 0f), facingRight: true);
                AssertThat(hitbox.IsActive).IsTrue();
                AssertThat(executor.LastHitboxWallBlocked).IsFalse();
                float clippedFar = 60f - EnemyAttackGeometryRules.WallClearancePixels;
                AssertFloat(shape.Size.X).IsEqualApprox(clippedFar - 20f, 0.5f);
                AssertFloat(hitbox.Position.X).IsEqualApprox((20f + clippedFar) / 2f, 0.5f);
                AssertFloat(hitbox.Position.X + shape.Size.X / 2f).IsLess(60f);
                executor.Cancel();

                // Flush against the wall (face 10 px out, nearer than the box's
                // near edge): the swing lands nothing at all.
                owner.GlobalPosition = ownerPosition + new Vector2(50f, 0f);
                executor.Begin(smash, ownerPosition + new Vector2(200f, 0f), facingRight: true);
                AssertThat(executor.Phase).IsEqual(EnemyAbilityPhase.Active);
                AssertThat(executor.LastHitboxWallBlocked).IsTrue();
                AssertThat(hitbox.IsActive).IsFalse();
                executor.Cancel();
            });
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async Task AChargeReclipsEveryActiveFrameAndSwitchesOffOnceItsReachIsBehindTheWall() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "ArenaChargeClipHost" };
        tree.Root.AddChild(host);
        var ownerPosition = new Vector2(42000f, -20000f);
        BuildWall(host, ownerPosition.X + 200f, ownerPosition.Y);
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox) = CreateSubject(host, ownerPosition);
                EnemyAbilityData charge = Melee(new Vector2(108f, 92f), new Vector2(60f, -60f), active: 20);
                charge.Archetype = EnemyAbilityArchetype.ChargeDash;
                charge.DashSpeed = 780f;
                var shape = (RectangleShape2D)hitbox.GetNode<CollisionShape2D>("CollisionShape2D").Shape;

                // 200 px from the wall the 6..114 box is clear.
                executor.Begin(charge, ownerPosition + new Vector2(400f, 0f), facingRight: true);
                AssertThat(hitbox.IsActive).IsTrue();
                AssertFloat(shape.Size.X).IsEqualApprox(108f, 0.01f);

                // The owner carries the box toward the wall: it shortens.
                owner.GlobalPosition = ownerPosition + new Vector2(120f, 0f);
                executor.Tick(Step);
                AssertThat(hitbox.IsActive).IsTrue();
                AssertFloat(hitbox.Position.X + shape.Size.X / 2f).IsLess(80f);

                // Pressed against the wall: switched off for the rest of the phase...
                owner.GlobalPosition = ownerPosition + new Vector2(196f, 0f);
                executor.Tick(Step);
                AssertThat(hitbox.IsActive).IsFalse();
                AssertThat(executor.LastHitboxWallBlocked).IsTrue();

                // ...and never re-armed by backing away, so it cannot re-deliver.
                owner.GlobalPosition = ownerPosition;
                executor.Tick(Step);
                AssertThat(hitbox.IsActive).IsFalse();
                executor.Cancel();
            });
        } finally {
            host.Free();
        }
    }

    // === M1 (fix pass): the player's swing stops at walls too ==============

    /// <summary>
    /// The mirror of P2 on the player side: an Area2D hitbox ignores walls, so a
    /// bot as Lincoln dealt the Level 2 Siegemaster Duke 476 of his 520 HP
    /// through the 20 px battlement footing wall. A basic swing now needs a clear
    /// Environment line from the swinger's body centre to the enemy's hurtbox.
    /// Real physics frames deliver the contact, so this also proves the ray is
    /// answered from inside the engine's area-overlap flush.
    /// </summary>
    [TestCase]
    public async Task APlayerSwingNeverLandsOnAnEnemyBehindAWall() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "PlayerSwingWallHost" };
        tree.Root.AddChild(host);
        // Feet on a floor at y = playerAt.Y; far from every other suite's geometry.
        var playerAt = new Vector2(56000f, -20000f);
        StaticBody2D floor = BuildFloor(host, playerAt);
        // The wall spans x +30..+50: clear of the player's body (to +20) and of
        // the enemy's (from +57), but between the two.
        StaticBody2D wall = BuildWall(host, playerAt.X + 30f, playerAt.Y - 100f);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        EnemyController enemy = null;
        try {
            player.Position = playerAt;
            host.AddChild(player);
            player.GlobalPosition = playerAt;
            enemy = CreateEnemy(host, "chrono_slasher", playerAt + new Vector2(80f, 0f));
            enemy.CurrentHP = 9999;
            // A passive target: no aggro, so it never closes in or swings back
            // (a stun would cancel the probe swing). Data is this enemy's copy.
            enemy.Data.AggroRadius = 0f;
            // Settle: the player lands into Idle and the space registers every body.
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            AssertThat(hitbox.RequiresEnvironmentLineOfSight).IsTrue();
            var shape = hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
            // A swing reaching x +10..+110: through the wall and over the enemy.
            ((RectangleShape2D)shape.Shape).Size = new Vector2(100f, 45f);
            shape.Position = new Vector2(60f, -32f);

            hitbox.Activate();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(enemy.CurrentHP)
                .OverrideFailureMessage("The swing landed through the wall.")
                .IsEqual(9999);
            hitbox.Deactivate();

            // Control: the same swing with the wall gone lands.
            wall.GlobalPosition += new Vector2(0f, 5000f);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            hitbox.Activate();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(enemy.CurrentHP < 9999)
                .OverrideFailureMessage("With no wall between them the swing must land.")
                .IsTrue();
            hitbox.Deactivate();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// The Rift Phantom (<c>PhasesThroughWalls</c>) drops the Environment bit
    /// from its body mask while it chases or attacks, so it passes through walls,
    /// and its own swing's wall clip is cast from its centre outward — which never
    /// sees the wall it starts inside. The player's line of sight is cast the same
    /// way, from the target back to the swinger, so a phantom caught mid-wall is
    /// not sheltered by the wall it is passing through; a phantom whose centre is
    /// past a wall's far face is still behind it.
    /// </summary>
    [TestCase]
    public async Task AWallPhasingEnemyInsideAWallIsNotShelteredByIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "PlayerSwingPhantomHost" };
        tree.Root.AddChild(host);
        var playerAt = new Vector2(62000f, -20000f);
        BuildFloor(host, playerAt);
        // A 100 px thick wall spanning x +30..+130: the phantom's centre at +80 is
        // inside it, and the player's body (to +20) is clear of it.
        BuildWall(host, playerAt.X + 30f, playerAt.Y - 100f, width: 100f);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        try {
            player.Position = playerAt;
            host.AddChild(player);
            player.GlobalPosition = playerAt;
            // First past the wall's far face (+130): its body (+127..+173) still
            // overlaps the wall, its centre (+150) does not.
            EnemyController phantom = CreateEnemy(host, "rift_phantom", playerAt + new Vector2(150f, 0f));
            AssertThat(phantom.Data.PhasesThroughWalls).IsTrue();
            phantom.CurrentHP = 9999;
            // Hold it where a chase left it, phasing. Its own tick would retarget
            // and move it; the hurtbox stays live with the tick paused.
            phantom.SetPhysicsProcess(false);
            phantom.CollisionMask = EnemyController.ResolveBodyMask(true, EnemyState.Chase);
            AssertThat(phantom.CollisionMask & CollisionLayers.Environment).IsEqual(0u);
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            AssertThat(hitbox.RequiresEnvironmentLineOfSight).IsTrue();
            var shape = hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
            // A swing reaching x +10..+170: over the phantom in both placements.
            ((RectangleShape2D)shape.Shape).Size = new Vector2(160f, 45f);
            shape.Position = new Vector2(90f, -32f);

            hitbox.Activate();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            hitbox.Deactivate();
            AssertThat(phantom.CurrentHP)
                .OverrideFailureMessage("A phantom whose centre is past the wall took a swing through it.")
                .IsEqual(9999);

            // Mid-wall (+80, inside +30..+130): the wall it is passing through
            // shelters it from nothing.
            phantom.GlobalPosition = playerAt + new Vector2(80f, 0f);
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            hitbox.Activate();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            hitbox.Deactivate();
            AssertThat(phantom.CurrentHP < 9999)
                .OverrideFailureMessage("A wall-phasing enemy inside a wall was sheltered by the wall it is passing through.")
                .IsTrue();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// The same rule when the wall is ONE body built from several shapes (two
    /// adjacent rects here; a decomposed <c>CollisionPolygon2D</c> is the same
    /// case). A ray ignores only the shape it starts inside, so a phantom in the
    /// far half of such a wall used to be sheltered by the near half of the very
    /// wall it is passing through; the line of sight skips the whole body that
    /// contains the target's centre. Another body between the two still blocks.
    /// </summary>
    [TestCase]
    public async Task AWallPhasingEnemyInsideAMultiShapeWallIsNotShelteredByIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "PlayerSwingCompoundWallHost" };
        tree.Root.AddChild(host);
        var playerAt = new Vector2(68000f, -20000f);
        BuildFloor(host, playerAt);
        // One body, two 50 px shapes: x +30..+80 and +80..+130.
        var wall = new StaticBody2D {
            Name = "CompoundWall",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = new Vector2(playerAt.X + 30f, playerAt.Y - 100f)
        };
        wall.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(50f, 600f) },
            Position = new Vector2(25f, 0f)
        });
        wall.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(50f, 600f) },
            Position = new Vector2(75f, 0f)
        });
        host.AddChild(wall);
        // A SEPARATE thin wall at +25..+29, between the compound wall and the
        // player's body (to +20): only the bodies containing the target are
        // skipped, so this one still blocks.
        StaticBody2D separate = BuildWall(host, playerAt.X + 25f, playerAt.Y - 100f, width: 4f);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        try {
            player.Position = playerAt;
            host.AddChild(player);
            player.GlobalPosition = playerAt;
            // Centre at +105: inside the far shape, with the near shape between it
            // and the player's body.
            EnemyController phantom = CreateEnemy(host, "rift_phantom", playerAt + new Vector2(105f, 0f));
            phantom.CurrentHP = 9999;
            phantom.SetPhysicsProcess(false);
            phantom.CollisionMask = EnemyController.ResolveBodyMask(true, EnemyState.Chase);
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            var shape = hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
            // A swing reaching x +10..+170.
            ((RectangleShape2D)shape.Shape).Size = new Vector2(160f, 45f);
            shape.Position = new Vector2(90f, -32f);
            hitbox.Activate();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            hitbox.Deactivate();
            AssertThat(phantom.CurrentHP)
                .OverrideFailureMessage("A separate wall in front of the phantom must still block the swing.")
                .IsEqual(9999);

            // With the separate wall gone, only the phantom's own (compound) wall
            // lies on the line, and it shelters nothing.
            separate.GlobalPosition += new Vector2(0f, 5000f);
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            hitbox.Activate();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            hitbox.Deactivate();
            AssertThat(phantom.CurrentHP < 9999)
                .OverrideFailureMessage("A phantom inside a multi-shape wall was sheltered by another shape of that same wall.")
                .IsTrue();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// A3 — the wall probe is cheap. A MeleeStrike clips once, at activation:
    /// its owner stands for the swing, so no active frame re-probes (not even
    /// one the owner is shoved through). A ChargeDash re-clips only on a frame
    /// its owner has actually moved, and every probe goes through the one
    /// reused ray query the executor holds instead of a new query per ray.
    /// </summary>
    [TestCase]
    public async Task AStrikeProbesItsWallOnceAndAChargeOnlyWhenItsOwnerMoves() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "ArenaProbeCountHost" };
        tree.Root.AddChild(host);
        var ownerPosition = new Vector2(44000f, -20000f);
        // Within reach of both boxes, so every sampled height is blocked and a
        // probe always casts all of its rays (a clear height stops it early).
        BuildWall(host, ownerPosition.X + 100f, ownerPosition.Y);
        try {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox) = CreateSubject(host, ownerPosition);
                int rays = EnemyAttackGeometryRules.WallProbeRays;

                EnemyAbilityData strike = Melee(new Vector2(112f, 96f), new Vector2(76f, -60f), active: 8);
                executor.Begin(strike, ownerPosition + new Vector2(300f, 0f), facingRight: true);
                AssertThat(hitbox.IsActive).IsTrue();
                AssertThat(executor.WallProbeRaysCast).IsEqual(rays);
                PhysicsRayQueryParameters2D query = executor.WallProbeQueryForTest;
                AssertObject(query).IsNotNull();
                for (int frame = 0; frame < 3; frame++) executor.Tick(Step);
                owner.GlobalPosition = ownerPosition + new Vector2(40f, 0f);
                executor.Tick(Step);
                AssertThat(executor.WallProbeRaysCast).IsEqual(rays);
                executor.Cancel();

                owner.GlobalPosition = ownerPosition;
                EnemyAbilityData charge = Melee(new Vector2(108f, 92f), new Vector2(60f, -60f), active: 20);
                charge.Archetype = EnemyAbilityArchetype.ChargeDash;
                charge.DashSpeed = 780f;
                executor.Begin(charge, ownerPosition + new Vector2(300f, 0f), facingRight: true);
                AssertThat(hitbox.IsActive).IsTrue();
                AssertThat(executor.WallProbeRaysCast).IsEqual(2 * rays);

                // A halted charge (pinned, rooted, frozen) probes nothing.
                executor.Tick(Step);
                executor.Tick(Step);
                AssertThat(executor.WallProbeRaysCast).IsEqual(2 * rays);

                // A moving one re-clips, through the same query.
                owner.GlobalPosition = ownerPosition + new Vector2(30f, 0f);
                executor.Tick(Step);
                AssertThat(executor.WallProbeRaysCast).IsEqual(3 * rays);
                AssertThat(ReferenceEquals(executor.WallProbeQueryForTest, query)).IsTrue();
                executor.Cancel();
            });
        } finally {
            host.Free();
        }
    }

    // === G8: the authored gates stop a swing from the far side ==============

    /// <summary>
    /// G8 (playtest bots, 2026-10-04): the bots reported enemies hitting the hero
    /// "through" Pompeii's closed rockfall and Chicago's closed Court of Honor
    /// door. Both are Environment bodies, so the P2 clip sees them: a forward
    /// swing from an enemy pressed against either face — the widest authored
    /// melee (the Siege Smash) and the legacy scalar melee alike — never reaches
    /// past that face, in either direction. (The Pompeii reports attribute every
    /// hit on the bot stuck at the rockfall to "env" — no enemy within the
    /// telemetry's 320 px — 6-point hits late on the timer: the Collapse Tremor's
    /// debris, 5 % of max HP. The Chicago hits came from the Chronal Inventor
    /// roaming out of its arena before P1 leashed it; with the leash every
    /// Inventor hit lands inside the arena.)
    /// </summary>
    [TestCase]
    public async Task TheRockfallAndTheCourtDoorStopForwardSwingsFromEitherSide() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var gates = new (CampaignLevel Level, string Checkpoint, string Door)[] {
            (CampaignLevel.Pompeii, "level_06_pompeii_checkpoint_0", "Rockfall"),
            (CampaignLevel.Chicago, Level03Controller.CheckpointEntry, "CourtOfHonorDoor")
        };
        foreach ((CampaignLevel campaignLevel, string checkpoint, string doorName) in gates) {
            using var fixture = new ResumedLevelFixture(campaignLevel, checkpoint);
            var door = fixture.Level.GetNodeOrNull<StaticBody2D>(doorName);
            AssertObject(door).OverrideFailureMessage($"{campaignLevel}: no '{doorName}'.").IsNotNull();
            AssertThat(door.CollisionLayer & CollisionLayers.Environment)
                .OverrideFailureMessage($"{campaignLevel}: '{doorName}' is not on the Environment layer.")
                .IsNotEqual(0u);
            Rect2 face = DoorRect(door);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            var host = new Node2D { Name = "GateClipHost" };
            tree.Root.AddChild(host);
            try {
                await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                    foreach (bool fromEast in new[] { true, false }) {
                        // An enemy body (46 px wide) pressed flat against the face, at
                        // floor level, swinging across the gate.
                        float ownerX = fromEast ? face.End.X + 23f : face.Position.X - 23f;
                        var ownerPosition = new Vector2(ownerX, face.End.Y - 50f);
                        (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox) = CreateSubject(host, ownerPosition);
                        var shape = (RectangleShape2D)hitbox.GetNode<CollisionShape2D>("CollisionShape2D").Shape;
                        foreach (EnemyAbilityData swing in new[] {
                                     Melee(new Vector2(112f, 96f), new Vector2(76f, -60f), active: 6),
                                     Melee(new Vector2(40f, 40f), new Vector2(30f, -25f), active: 6) }) {
                            Vector2 across = ownerPosition + new Vector2(fromEast ? -300f : 300f, 0f);
                            executor.Begin(swing, across, facingRight: !fromEast);
                            if (hitbox.IsActive) {
                                float west = hitbox.GlobalPosition.X - shape.Size.X / 2f;
                                float east = hitbox.GlobalPosition.X + shape.Size.X / 2f;
                                bool clear = fromEast ? west >= face.End.X - 0.5f : east <= face.Position.X + 0.5f;
                                AssertThat(clear)
                                    .OverrideFailureMessage(
                                        $"{campaignLevel}: a {swing.HitboxSize} swing from the " +
                                        $"{(fromEast ? "east" : "west")} spans x {west:0}..{east:0} across " +
                                        $"'{doorName}' {face}")
                                    .IsTrue();
                            }
                            executor.Cancel();
                        }
                        owner.Free();
                    }
                });
            } finally {
                host.Free();
            }
        }
    }

    private static Rect2 DoorRect(StaticBody2D door) {
        Godot.Collections.Array<Node> children = door.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is CollisionShape2D { Shape: RectangleShape2D rect } shape) {
                return new Rect2(shape.GlobalPosition - rect.Size / 2f, rect.Size);
            }
        }
        AssertThat(false).OverrideFailureMessage($"'{door.Name}' has no rectangle collision shape.").IsTrue();
        return default;
    }

    /// <summary>
    /// An authored campaign scene resumed at a checkpoint (a fresh entry defers a
    /// tree-pausing entrance dialogue), then made slotless so the frames this case
    /// runs can never write a real save slot; everything is handed back on dispose.
    /// </summary>
    private sealed class ResumedLevelFixture : System.IDisposable {
        private const int ScratchSlot = 2;
        public readonly Node Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public ResumedLevelFixture(CampaignLevel level, string checkpointID) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(level),
                LastCheckpointID = checkpointID,
                CurrentHP = 100
            };
            Level = ResourceLoader.Load<PackedScene>(StoryManager.GetLevelScenePath(level)).Instantiate();
            Level.Name = $"{level}_GateClipFixture";
            tree.Root.AddChild(Level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }

    // === P3: lead ========================================================

    [TestCase]
    public void TheLeadExtrapolatesHorizontallyOverTheFlightAndCapsIt() {
        var origin = new Vector2(0f, 0f);
        var aim = new Vector2(300f, -32f);

        // A standing target, or a shot with no speed: the aim point itself.
        AssertThat(EnemyAttackGeometryRules.LeadPoint(origin, aim, Vector2.Zero, 500f)).IsEqual(aim);
        AssertThat(EnemyAttackGeometryRules.LeadPoint(origin, aim, new Vector2(400f, 0f), 0f)).IsEqual(aim);

        // A runner is led along its run, never vertically: two intercept steps,
        // each flight time measured to the previous prediction.
        Vector2 led = EnemyAttackGeometryRules.LeadPoint(origin, aim, new Vector2(400f, -900f), 1000f);
        AssertFloat(led.Y).IsEqual(aim.Y);
        float firstFlight = origin.DistanceTo(aim) / 1000f;
        var firstGuess = new Vector2(aim.X + 400f * firstFlight, aim.Y);
        float secondFlight = origin.DistanceTo(firstGuess) / 1000f;
        AssertThat(secondFlight < EnemyAttackGeometryRules.MaxLeadSeconds).IsTrue();
        AssertFloat(led.X).IsEqualApprox(aim.X + 400f * secondFlight, 0.01f);

        // A slow, distant shot is capped at MaxLeadSeconds of extrapolation.
        Vector2 capped = EnemyAttackGeometryRules.LeadPoint(origin, new Vector2(2000f, 0f), new Vector2(300f, 0f), 100f);
        AssertFloat(capped.X).IsEqualApprox(2000f + 300f * EnemyAttackGeometryRules.MaxLeadSeconds, 0.01f);
    }

    [TestCase]
    public void AShotReacquiresTheRunnerAtFireTimeAndLeadsItsBody() {
        (PoolManager pools, Node parent) = CreatePools();
        var ownerPosition = new Vector2(46000f, -20000f);
        (Node2D owner, EnemyAbilityExecutor executor, _) = CreateSubject(parent, ownerPosition);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
        try {
            parent.AddChild(player);
            // The telegraph promised the spot where the player WAS; it has since
            // run 120 px further and is still running.
            Vector2 telegraphed = ownerPosition + new Vector2(300f, 0f);
            player.GlobalPosition = telegraphed + new Vector2(120f, 0f);
            player.Velocity = new Vector2(420f, 0f);

            EnemyAbilityData shot = Melee(new Vector2(20f, 20f), Vector2.Zero, active: 2);
            shot.AbilityID = "test.arena.lead";
            shot.Archetype = EnemyAbilityArchetype.Projectile;
            shot.ProjectileCount = 1;
            shot.ProjectileSpeed = 460f;
            executor.Begin(shot, telegraphed, facingRight: true);

            IReadOnlyList<Node> active = pools.GetActiveNodes(EnemyAbilityExecutor.ProjectilePoolID);
            AssertThat(active.Count).IsEqual(1);
            Vector2 fired = ((EnemyProjectile)active[0]).Velocity.Normalized();

            Vector2 origin = ownerPosition + new Vector2(24f, -40f);
            Vector2 body = player.GlobalPosition - new Vector2(0f, EnemyAttackGeometryRules.BodyAimHeightPixels);
            Vector2 expected = (EnemyAttackGeometryRules.LeadPoint(origin, body, player.Velocity, 460f) - origin)
                .Normalized();
            AssertFloat(fired.X).IsEqualApprox(expected.X, 0.001f);
            AssertFloat(fired.Y).IsEqualApprox(expected.Y, 0.001f);

            // Not the stale telegraphed feet.
            Vector2 stale = (telegraphed - origin).Normalized();
            AssertThat(fired.DistanceTo(stale) > 0.01f).IsTrue();
        } finally {
            player.GetParent()?.RemoveChild(player);
            player.Free();
            owner.Free();
            CleanupPools(pools, parent);
        }
    }

    [TestCase]
    public void ATargetThatCrossedBehindTheShooterKeepsTheTelegraphedAim() {
        (PoolManager pools, Node parent) = CreatePools();
        var ownerPosition = new Vector2(48000f, -20000f);
        (Node2D owner, EnemyAbilityExecutor executor, _) = CreateSubject(parent, ownerPosition);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
        try {
            parent.AddChild(player);
            Vector2 telegraphed = ownerPosition + new Vector2(300f, 0f);
            // Rolled through the shooter during the wind-up: now behind it.
            player.GlobalPosition = ownerPosition + new Vector2(-60f, 0f);
            player.Velocity = new Vector2(-420f, 0f);

            EnemyAbilityData shot = Melee(new Vector2(20f, 20f), Vector2.Zero, active: 2);
            shot.AbilityID = "test.arena.crossed";
            shot.Archetype = EnemyAbilityArchetype.Projectile;
            shot.ProjectileCount = 1;
            shot.ProjectileSpeed = 460f;
            executor.Begin(shot, telegraphed, facingRight: true);

            var projectile = (EnemyProjectile)pools.GetActiveNodes(EnemyAbilityExecutor.ProjectilePoolID)[0];
            Vector2 origin = ownerPosition + new Vector2(24f, -40f);
            Vector2 expected = (telegraphed - origin).Normalized();
            AssertFloat(projectile.Velocity.Normalized().X).IsEqualApprox(expected.X, 0.001f);
            AssertFloat(projectile.Velocity.Normalized().Y).IsEqualApprox(expected.Y, 0.001f);
        } finally {
            player.GetParent()?.RemoveChild(player);
            player.Free();
            owner.Free();
            CleanupPools(pools, parent);
        }
    }

    // === P3 × GAP-10c: the sand decoy keeps its lure =======================

    /// <summary>
    /// A mob aims at Cleopatra's live sand decoy instead of her, and she always
    /// stands within the re-sample radius of her own decoy. A shot telegraphed
    /// at the decoy must stay on the sand — even when the decoy fades during
    /// the wind-up — rather than re-acquiring and leading Cleopatra.
    /// </summary>
    [TestCase]
    public void AShotTelegraphedAtASandDecoyStaysOnTheSand() {
        (PoolManager pools, Node parent) = CreatePools();
        PlayerController cleopatra = CharacterFactory.CreateCharacter("cleopatra", 0);
        EnemyController archer = null;
        try {
            parent.AddChild(cleopatra);
            var decoyPosition = new Vector2(50000f, -20000f);
            SandDecoyNode decoy = ArmDecoy(parent, cleopatra, decoyPosition);
            // She rushed 200 px away and keeps running, well inside the 480 px
            // re-sample radius of her own decoy.
            cleopatra.GlobalPosition = decoyPosition + new Vector2(-200f, 0f);
            cleopatra.Velocity = new Vector2(-420f, 0f);

            archer = CreateArcher(parent, decoyPosition + new Vector2(400f, 0f));
            archer.SetChaseTargetForTest(cleopatra);
            AssertThat(archer.TargetAimPosition).IsEqual(decoyPosition);

            archer.BeginAttack(Shot("test.arena.decoy_lure", telegraph: 10));
            // The sand blows away during the wind-up; the promise still holds.
            decoy.Disperse();
            EnemyProjectile shot = FireAfterTelegraph(pools, archer);

            Vector2 fired = shot.Velocity.Normalized();
            Vector2 toDecoy = (decoyPosition - shot.GlobalPosition).Normalized();
            AssertFloat(fired.X).IsEqualApprox(toDecoy.X, 0.001f);
            AssertFloat(fired.Y).IsEqualApprox(toDecoy.Y, 0.001f);
            Vector2 toCleopatra = (cleopatra.GlobalPosition - shot.GlobalPosition).Normalized();
            AssertThat(fired.DistanceTo(toCleopatra) > 0.01f).IsTrue();
        } finally {
            FreeShooterFixture(pools, parent, cleopatra, archer);
        }
    }

    /// <summary>
    /// A decoy raised during the telegraph takes the shot: the re-sampled
    /// target is Cleopatra, and a mob chasing her aims at her live decoy.
    /// </summary>
    [TestCase]
    public void ADecoyRaisedDuringTheTelegraphTakesTheShot() {
        (PoolManager pools, Node parent) = CreatePools();
        PlayerController cleopatra = CharacterFactory.CreateCharacter("cleopatra", 0);
        EnemyController archer = null;
        try {
            parent.AddChild(cleopatra);
            var start = new Vector2(52000f, -20000f);
            cleopatra.GlobalPosition = start;
            archer = CreateArcher(parent, start + new Vector2(400f, 0f));
            archer.SetChaseTargetForTest(cleopatra);
            archer.BeginAttack(Shot("test.arena.decoy_mid_telegraph", telegraph: 10));

            // Mid-wind-up she moves off the telegraphed point, mirages from the
            // new spot (the decoy stays there), and keeps going. The shot must
            // answer the decoy — not the telegraphed point, and not her.
            var decoyPosition = start + new Vector2(-120f, -60f);
            cleopatra.GlobalPosition = decoyPosition;
            ArmDecoy(parent, cleopatra, decoyPosition);
            cleopatra.GlobalPosition = decoyPosition + new Vector2(-220f, 0f);
            cleopatra.Velocity = new Vector2(-420f, 0f);
            EnemyProjectile shot = FireAfterTelegraph(pools, archer);

            Vector2 fired = shot.Velocity.Normalized();
            Vector2 toDecoy = (decoyPosition - shot.GlobalPosition).Normalized();
            AssertFloat(fired.X).IsEqualApprox(toDecoy.X, 0.001f);
            AssertFloat(fired.Y).IsEqualApprox(toDecoy.Y, 0.001f);
            Vector2 toTelegraphed = (start - shot.GlobalPosition).Normalized();
            AssertThat(fired.DistanceTo(toTelegraphed) > 0.01f)
                .OverrideFailureMessage("The shot kept the telegraphed point instead of answering the new decoy.")
                .IsTrue();
            Vector2 toCleopatra = (cleopatra.GlobalPosition - shot.GlobalPosition).Normalized();
            AssertThat(fired.DistanceTo(toCleopatra) > 0.01f).IsTrue();
        } finally {
            FreeShooterFixture(pools, parent, cleopatra, archer);
        }
    }

    private static SandDecoyNode ArmDecoy(Node parent, PlayerController owner, Vector2 position) {
        var decoy = new SandDecoyNode { Name = "ArenaTestSandDecoy" };
        parent.AddChild(decoy);
        decoy.Arm(owner, position);
        return decoy;
    }

    private static EnemyController CreateArcher(Node parent, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/laser_archer.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        parent.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    private static EnemyAbilityData Shot(string id, int telegraph) {
        EnemyAbilityData shot = Melee(new Vector2(20f, 20f), Vector2.Zero, active: 2);
        shot.AbilityID = id;
        shot.Archetype = EnemyAbilityArchetype.Projectile;
        shot.TelegraphFrames = telegraph;
        shot.ProjectileCount = 1;
        shot.ProjectileSpeed = 460f;
        return shot;
    }

    private static EnemyProjectile FireAfterTelegraph(PoolManager pools, EnemyController shooter) {
        for (int frame = 0; frame < 30; frame++) {
            IReadOnlyList<Node> active = pools.GetActiveNodes(EnemyAbilityExecutor.ProjectilePoolID);
            if (active.Count > 0) {
                AssertThat(active.Count).IsEqual(1);
                return (EnemyProjectile)active[0];
            }
            shooter.TickAbilityExecutor(Step);
        }
        AssertThat(false).OverrideFailureMessage("the shot never fired").IsTrue();
        return null;
    }

    private static void FreeShooterFixture(PoolManager pools, Node parent, PlayerController player,
        EnemyController shooter) {
        if (player != null && GodotObject.IsInstanceValid(player)) {
            player.GetParent()?.RemoveChild(player);
            player.Free();
        }
        if (shooter != null && GodotObject.IsInstanceValid(shooter)) {
            shooter.GetParent()?.RemoveChild(shooter);
            shooter.Free();
        }
        CleanupPools(pools, parent);
    }

    // === Helpers ==========================================================

    /// <summary>An Environment floor 2000 px wide whose top surface is at <paramref name="at"/>.Y.</summary>
    private static StaticBody2D BuildFloor(Node host, Vector2 at) {
        var floor = new StaticBody2D {
            Name = "SwingFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = at
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(2000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        return floor;
    }

    /// <summary>A canonical-data enemy, positioned before it enters the tree.</summary>
    private static EnemyController CreateEnemy(Node parent, string enemyID, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        parent.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    private static StaticBody2D BuildWall(Node host, float faceX, float centreY, float width = 20f) {
        var wall = new StaticBody2D {
            Name = "ClipWall",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = new Vector2(faceX, centreY)
        };
        wall.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(width, 600f) },
            Position = new Vector2(width / 2f, 0f)
        });
        host.AddChild(wall);
        return wall;
    }

    private static EnemyAbilityData Melee(Vector2 size, Vector2 offset, int active) => new() {
        AbilityID = "test.arena.melee",
        Archetype = EnemyAbilityArchetype.MeleeStrike,
        TelegraphFrames = 0,
        ActiveFrames = active,
        RecoveryFrames = 0,
        Damage = 10f,
        KnockbackForce = new Vector2(3f, -1f),
        HitboxSize = size,
        HitboxOffset = offset
    };

    private static (Node2D owner, EnemyAbilityExecutor executor, Hitbox hitbox) CreateSubject(
        Node parent, Vector2 position) {
        var owner = new Node2D { Name = "ArenaAbilityOwner" };
        var hitbox = new Hitbox { Name = "Hitbox", OwnerPlayerIndex = -1 };
        hitbox.AddChild(new CollisionShape2D {
            Name = "CollisionShape2D",
            Shape = new RectangleShape2D { Size = new Vector2(40f, 40f) }
        });
        owner.AddChild(hitbox);
        parent.AddChild(owner);
        owner.GlobalPosition = position;
        var executor = new EnemyAbilityExecutor(owner) { SourceID = "arena_test" };
        executor.Bind(null, hitbox, null);
        return (owner, executor, hitbox);
    }

    private static (PoolManager pools, Node parent) CreatePools() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "ArenaExecutorPoolParent" };
        tree.Root.AddChild(parent);
        var pools = new PoolManager { Name = "ArenaExecutorPoolManager" };
        tree.Root.AddChild(pools);
        return (pools, parent);
    }

    private static void CleanupPools(PoolManager pools, Node parent) {
        pools.ClearAllPools();
        pools.Free();
        parent.Free();
    }
}
