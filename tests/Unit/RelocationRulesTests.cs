using System.Collections.Generic;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-09 (VERIFY-L10-ARENA-RECESS-EXIT, boss half): the pure relocation
/// rule every Story direct position write goes through — travel at body height
/// and stop short of the first wall; then a floor within the search depth or a
/// step back toward the origin, else a refusal. The world is a plain list of
/// rects (no physics space), so the rule is pinned in isolation; the suite still
/// carries <c>[RequireGodotRuntime]</c> because it uses Godot's <see cref="Vector2"/>
/// and <see cref="Rect2"/> (the GdUnit0501 analyzer).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RelocationRulesTests {
    /// <summary>Boss.tscn's body: 96 x 128, centred 64 px above the feet.</summary>
    private static readonly RelocationBody Boss = new(48f, -64f, 64f, needsFloor: true, passesWalls: false);

    private const float FloorTop = 1000f;

    [TestCase]
    public void AnOpenTravelLandsWhereRequested() {
        var world = new RectWorld();
        world.Solid(new Rect2(0f, FloorTop, 4000f, 40f));
        RelocationResult result = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1300f, FloorTop), Boss);
        AssertThat(result.Outcome).IsEqual(RelocationOutcome.AsRequested);
        AssertThat(result.Position).IsEqual(new Vector2(1300f, FloorTop));
        AssertThat(result.ClippedAtWall).IsFalse();
    }

    [TestCase]
    public void ATravelIntoAWallStopsItsHalfWidthAndClearanceShortOfTheFace() {
        var world = new RectWorld();
        world.Solid(new Rect2(0f, FloorTop, 4000f, 40f));
        world.Solid(new Rect2(1200f, FloorTop - 300f, 20f, 300f));
        RelocationResult east = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1400f, FloorTop), Boss);
        AssertThat(east.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
        AssertFloat(east.Position.X).IsEqualApprox(1200f - 48f - RelocationRules.WallClearancePixels, 0.01f);
        AssertFloat(east.Position.Y).IsEqualApprox(FloorTop, 0.01f);

        // The same from the far side, travelling west into the wall's east face.
        RelocationResult west = RelocationRules.Resolve(world,
            new Vector2(1500f, FloorTop), new Vector2(1000f, FloorTop), Boss);
        AssertThat(west.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
        AssertFloat(west.Position.X).IsEqualApprox(1220f + 48f + RelocationRules.WallClearancePixels, 0.01f);

        // The clip in isolation: no wall leaves the travel, a wall clamps it to
        // [0, travel] after the half-width and the clearance.
        AssertFloat(RelocationRules.AllowedTravel(300f, -1f, 48f)).IsEqual(300f);
        AssertFloat(RelocationRules.AllowedTravel(300f, 500f, 48f)).IsEqual(300f);
        AssertFloat(RelocationRules.AllowedTravel(300f, 200f, 48f)).IsEqual(200f - 48f - RelocationRules.WallClearancePixels);
        AssertFloat(RelocationRules.AllowedTravel(300f, 30f, 48f)).IsEqual(0f);
    }

    [TestCase]
    public void AWallOnlyTheBodysHeadWouldMeetStillStopsIt() {
        // A board edge at head height with open space under it (the Level 10
        // stage boards over the recess): the body's top ray finds it.
        var world = new RectWorld();
        world.Solid(new Rect2(0f, FloorTop, 4000f, 40f));
        world.Solid(new Rect2(1200f, FloorTop - 140f, 800f, 40f));
        // The three rays run inside the body: top near its head, bottom clear of its feet.
        AssertFloat(RelocationRules.WallRayOffsetY(Boss, 0)).IsLess(-100f);
        AssertFloat(RelocationRules.WallRayOffsetY(Boss, 0)).IsGreater(-128f);
        AssertFloat(RelocationRules.WallRayOffsetY(Boss, 1)).IsEqual(-64f);
        AssertFloat(RelocationRules.WallRayOffsetY(Boss, 2)).IsLess(-12f);
        AssertFloat(RelocationRules.WallRayOffsetY(Boss, 2)).IsGreater(-64f);
        RelocationResult result = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1500f, FloorTop), Boss);
        AssertThat(result.ClippedAtWall).IsTrue();
        AssertFloat(result.Position.X).IsEqualApprox(1200f - 48f - RelocationRules.WallClearancePixels, 0.01f);
    }

    /// <summary>
    /// The body's own box is swept along the travel, so a slab thinner than the
    /// spacing of the old wall rays (44.8 px on the boss body) stops it wherever
    /// it lies across the body — here a 15 px board between the centre and top
    /// rays, and one between the centre and bottom rays. The band at the feet is
    /// not swept: a low step and the seam where two floor pieces meet are floor,
    /// not walls.
    /// </summary>
    [TestCase]
    public void AThinSlabBetweenTheOldRayLinesStopsTheBody() {
        foreach (float slabTop in new[] { FloorTop - 95f, FloorTop - 45f }) {
            var world = new RectWorld();
            world.Solid(new Rect2(0f, FloorTop, 4000f, 40f));
            world.Solid(new Rect2(1150f, slabTop, 450f, 15f));
            RelocationResult result = RelocationRules.Resolve(world,
                new Vector2(1000f, FloorTop), new Vector2(1400f, FloorTop), Boss);
            AssertThat(result.Outcome)
                .OverrideFailureMessage($"A board at y {slabTop}..{slabTop + 15f} let the body through: {result}.")
                .IsEqual(RelocationOutcome.ClippedAtWall);
            AssertFloat(result.Position.X).IsEqualApprox(1150f - 48f - RelocationRules.WallClearancePixels, 0.01f);
            AssertFloat(result.Position.Y).IsEqualApprox(FloorTop, 0.01f);
        }

        // The swept box: the full width, from the head down to the lowest ray's line.
        Rect2 box = RelocationRules.SweepBox(Boss);
        AssertFloat(box.Position.X).IsEqual(-48f);
        AssertFloat(box.Size.X).IsEqual(96f);
        AssertFloat(box.Position.Y).IsEqual(-128f);
        AssertFloat(box.End.Y).IsEqualApprox(RelocationRules.WallRayOffsetY(Boss, 2), 0.001f);

        // A 10 px step on the floor and a seam between two floor pieces are not walls.
        var stepped = new RectWorld();
        stepped.Solid(new Rect2(0f, FloorTop, 4000f, 40f));
        stepped.Solid(new Rect2(1150f, FloorTop - 10f, 450f, 10f));
        RelocationResult onStep = RelocationRules.Resolve(stepped,
            new Vector2(1000f, FloorTop), new Vector2(1400f, FloorTop), Boss);
        AssertThat(onStep.Outcome).IsEqual(RelocationOutcome.AsRequested);
        AssertThat(onStep.Position).IsEqual(new Vector2(1400f, FloorTop));
        var seamed = new RectWorld();
        seamed.Solid(new Rect2(0f, FloorTop, 1200f, 40f));
        seamed.Solid(new Rect2(1200f, FloorTop, 2800f, 40f));
        RelocationResult acrossSeam = RelocationRules.Resolve(seamed,
            new Vector2(1000f, FloorTop), new Vector2(1400f, FloorTop), Boss);
        AssertThat(acrossSeam.Outcome).IsEqual(RelocationOutcome.AsRequested);
        AssertThat(acrossSeam.Position).IsEqual(new Vector2(1400f, FloorTop));
    }

    [TestCase]
    public void ABodyFlushAgainstAWallDoesNotMoveThroughIt() {
        var world = new RectWorld();
        world.Solid(new Rect2(0f, FloorTop, 4000f, 40f));
        world.Solid(new Rect2(1049f, FloorTop - 300f, 20f, 300f));
        RelocationResult result = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1400f, FloorTop), Boss);
        AssertThat(result.ClippedAtWall).IsTrue();
        AssertThat(result.Position).IsEqual(new Vector2(1000f, FloorTop));
    }

    [TestCase]
    public void ALandingOverAGapStepsBackToTheFirstSupportedPoint() {
        var world = new RectWorld();
        // The floor ends at x 1100; beyond it the drop is deeper than the search.
        world.Solid(new Rect2(0f, FloorTop, 1100f, 40f));
        world.Solid(new Rect2(1100f, FloorTop + RelocationRules.FloorSearchDepthPixels + 100f, 2000f, 40f));
        RelocationResult result = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1300f, FloorTop), Boss);
        AssertThat(result.Outcome).IsEqual(RelocationOutcome.SteppedBackToSupport);
        AssertFloat(result.Position.X).IsLess(1100f);
        AssertFloat(result.Position.X).IsGreater(1100f - RelocationRules.StepBackPixels - 0.01f);
        AssertFloat(result.Position.Y).IsEqualApprox(FloorTop, 0.01f);

        // A floor inside the search depth is a drop, not a void: it stands.
        var stepDown = new RectWorld();
        stepDown.Solid(new Rect2(0f, FloorTop, 1100f, 40f));
        stepDown.Solid(new Rect2(1100f, FloorTop + 140f, 2000f, 40f));
        RelocationResult dropped = RelocationRules.Resolve(stepDown,
            new Vector2(1000f, FloorTop), new Vector2(1300f, FloorTop), Boss);
        AssertThat(dropped.Outcome).IsEqual(RelocationOutcome.AsRequested);
        AssertThat(dropped.Position).IsEqual(new Vector2(1300f, FloorTop));

        // A one-way platform is support too.
        var oneWay = new RectWorld();
        oneWay.Solid(new Rect2(0f, FloorTop, 1100f, 40f));
        oneWay.OneWay(new Rect2(1250f, FloorTop + 60f, 120f, 8f));
        RelocationResult onPlatform = RelocationRules.Resolve(oneWay,
            new Vector2(1000f, FloorTop), new Vector2(1300f, FloorTop), Boss);
        AssertThat(onPlatform.Outcome).IsEqual(RelocationOutcome.AsRequested);
    }

    [TestCase]
    public void WithNoSupportedPointShortOfTheOriginTheWriteIsRefused() {
        // The body stands on a narrow pillar; everything past it
        // is a void.
        var world = new RectWorld();
        world.Solid(new Rect2(990f, FloorTop, 20f, 400f));
        RelocationResult result = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1300f, FloorTop), Boss);
        AssertThat(result.Outcome).IsEqual(RelocationOutcome.HeldAtOrigin);
        AssertThat(result.HeldInPlace).IsTrue();
        AssertThat(result.Position).IsEqual(new Vector2(1000f, FloorTop));
    }

    /// <summary>
    /// A body standing on a ledge with its centre past the edge — held up by the
    /// rest of its shape — is supported, not "already over nothing": a write
    /// toward the void steps back, and with no supported point short of the
    /// origin it is refused, so the body stays on its ledge. Origin support is
    /// read across the body's whole width (centre and both half-width extents);
    /// a single ray at the centre called it unsupported and let the write land
    /// over the void.
    /// </summary>
    [TestCase]
    public void ABodyOnALedgeWithItsCentrePastTheEdgeIsStillSupported() {
        const float ledge = 1000f;
        var world = new RectWorld();
        // The floor ends at x 1000; beyond it the drop is deeper than the search.
        world.Solid(new Rect2(0f, FloorTop, ledge, 40f));
        world.Solid(new Rect2(ledge, FloorTop + RelocationRules.FloorSearchDepthPixels + 200f, 3000f, 40f));
        // Centre 30 px past the edge; the west 18 px of the 96 px body still on the floor.
        var onLedge = new Vector2(ledge + 30f, FloorTop);

        RelocationResult east = RelocationRules.Resolve(world, onLedge, new Vector2(ledge + 330f, FloorTop), Boss);
        AssertThat(east.Outcome)
            .OverrideFailureMessage($"A write from the ledge toward the void resolved to {east}.")
            .IsEqual(RelocationOutcome.HeldAtOrigin);
        AssertThat(east.Position).IsEqual(onLedge);

        // The mirror image: the floor starts at x 3000, the body hangs 30 px west of it.
        var mirrored = new RectWorld();
        mirrored.Solid(new Rect2(3000f, FloorTop, 2000f, 40f));
        var onWestLedge = new Vector2(3000f - 30f, FloorTop);
        RelocationResult west = RelocationRules.Resolve(mirrored, onWestLedge, new Vector2(3000f - 330f, FloorTop), Boss);
        AssertThat(west.Outcome).IsEqual(RelocationOutcome.HeldAtOrigin);
        AssertThat(west.Position).IsEqual(onWestLedge);

        // A body whose whole width is past the edge really is over nothing.
        var offLedge = new Vector2(ledge + 60f, FloorTop);
        RelocationResult falling = RelocationRules.Resolve(world, offLedge, new Vector2(ledge + 360f, FloorTop), Boss);
        AssertThat(falling.Outcome).IsEqual(RelocationOutcome.OriginUnsupported);
    }

    /// <summary>
    /// A summoner standing on a ledge with its centre 30 px past the edge (the
    /// boss body: the west 18 px of its 96 px on the floor) summons a 46 px
    /// minion over the void: the minion emerges from the part of the summoner
    /// that stands on something and lands there, its own centre over the floor.
    /// Travelling out from the summoner's centre and judged by its own width
    /// around it, the minion found no floor at the origin and landed over the
    /// void ("already over nothing"). A minion summoned over the floor keeps its
    /// requested point; a summoner whose centre stands on the floor, or a minion
    /// that flies, leaves from the summoner's feet as before; and a summoner
    /// wholly past the edge is over nothing, as before.
    /// </summary>
    [TestCase]
    public void ANarrowSummonFromALedgeLandsWhereItsOwnBodyHasAFloor() {
        const float ledge = 1000f;
        var narrow = new RelocationBody(23f, -36f, 36f, needsFloor: true, passesWalls: false);
        var world = new RectWorld();
        world.Solid(new Rect2(0f, FloorTop, ledge, 40f));
        world.Solid(new Rect2(ledge, FloorTop + RelocationRules.FloorSearchDepthPixels + 200f, 3000f, 40f));
        var summoner = new Vector2(ledge + 30f, FloorTop);

        foreach (float offset in new[] { 96f, 144f }) {
            RelocationResult east = RelocationRules.ResolveSummon(world, summoner, Boss.HalfWidth,
                summoner + new Vector2(offset, 0f), narrow);
            AssertFloat(east.Position.X)
                .OverrideFailureMessage($"A summon {offset} px east of a summoner on the ledge resolved to {east}.")
                .IsLessEqual(ledge);
            AssertFloat(east.Position.X).IsGreaterEqual(summoner.X - Boss.HalfWidth);
            AssertFloat(east.Position.Y).IsEqualApprox(FloorTop, 0.01f);
            AssertThat(east.Outcome).IsEqual(RelocationOutcome.HeldAtOrigin);

            RelocationResult west = RelocationRules.ResolveSummon(world, summoner, Boss.HalfWidth,
                summoner - new Vector2(offset, 0f), narrow);
            AssertThat(west.Outcome).IsEqual(RelocationOutcome.AsRequested);
            AssertThat(west.Position).IsEqual(summoner - new Vector2(offset, 0f));
        }

        // A summoner whose centre stands on the floor, and a flying minion, leave from its feet.
        var onFloor = new Vector2(ledge - 60f, FloorTop);
        AssertThat(RelocationRules.EmergencePoint(world, onFloor, Boss.HalfWidth, onFloor.X + 96f, narrow))
            .IsEqual(onFloor);
        var flyer = new RelocationBody(23f, -36f, 36f, needsFloor: false, passesWalls: false);
        AssertThat(RelocationRules.EmergencePoint(world, summoner, Boss.HalfWidth, summoner.X + 96f, flyer))
            .IsEqual(summoner);

        // A summoner wholly past the edge is over nothing: the minion lands as requested.
        var offLedge = new Vector2(ledge + 60f, FloorTop);
        RelocationResult falling = RelocationRules.ResolveSummon(world, offLedge, Boss.HalfWidth,
            offLedge + new Vector2(96f, 0f), narrow);
        AssertThat(falling.Outcome).IsEqual(RelocationOutcome.OriginUnsupported);
        AssertThat(falling.Position).IsEqual(offLedge + new Vector2(96f, 0f));
    }

    [TestCase]
    public void ABodyAlreadyOverNothingKeepsItsWallClippedLanding() {
        var world = new RectWorld();
        world.Solid(new Rect2(1200f, FloorTop - 300f, 20f, 300f));
        RelocationResult open = RelocationRules.Resolve(world,
            new Vector2(1500f, FloorTop), new Vector2(1700f, FloorTop), Boss);
        AssertThat(open.Outcome).IsEqual(RelocationOutcome.OriginUnsupported);
        AssertThat(open.Position).IsEqual(new Vector2(1700f, FloorTop));

        RelocationResult walled = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1400f, FloorTop), Boss);
        AssertThat(walled.Outcome).IsEqual(RelocationOutcome.OriginUnsupported);
        AssertThat(walled.ClippedAtWall).IsTrue();
        AssertFloat(walled.Position.X).IsEqualApprox(1200f - 48f - RelocationRules.WallClearancePixels, 0.01f);
    }

    [TestCase]
    public void AFlyerNeedsNoFloorAndAPhaserIgnoresWalls() {
        var world = new RectWorld();
        world.Solid(new Rect2(0f, FloorTop, 1100f, 40f));
        world.Solid(new Rect2(1050f, FloorTop - 300f, 20f, 300f));
        var flyer = new RelocationBody(20f, -30f, 30f, needsFloor: false, passesWalls: false);
        RelocationResult flown = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop - 200f), new Vector2(1000f - 400f, FloorTop - 200f), flyer);
        AssertThat(flown.Outcome).IsEqual(RelocationOutcome.AsRequested);
        // ...but a flyer still stops at a wall it cannot pass.
        RelocationResult blocked = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop - 100f), new Vector2(1400f, FloorTop - 100f), flyer);
        AssertThat(blocked.ClippedAtWall).IsTrue();
        AssertFloat(blocked.Position.X).IsEqualApprox(1050f - 20f - RelocationRules.WallClearancePixels, 0.01f);

        // The Rift Phantom: a flying wall-phaser blinks straight through.
        var phaser = new RelocationBody(20f, -30f, 30f, needsFloor: false, passesWalls: true);
        RelocationResult phased = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop - 100f), new Vector2(1400f, FloorTop - 100f), phaser);
        AssertThat(phased.Outcome).IsEqual(RelocationOutcome.AsRequested);
        AssertThat(phased.Position).IsEqual(new Vector2(1400f, FloorTop - 100f));
    }

    [TestCase]
    public void AnUnaskableWorldLeavesTheRequestUnverified() {
        var world = new RectWorld { Queryable = false };
        world.Solid(new Rect2(1200f, FloorTop - 300f, 20f, 300f));
        RelocationResult result = RelocationRules.Resolve(world,
            new Vector2(1000f, FloorTop), new Vector2(1400f, FloorTop), Boss);
        AssertThat(result.Outcome).IsEqual(RelocationOutcome.Unverified);
        AssertThat(result.Verified).IsFalse();
        AssertThat(result.Position).IsEqual(new Vector2(1400f, FloorTop));
        AssertThat(RelocationRules.Resolve(null, Vector2.Zero, Vector2.One, Boss).Outcome)
            .IsEqual(RelocationOutcome.Unverified);
    }

    /// <summary>
    /// The defect itself, against a replica of the Level 10 arena recess: every
    /// <c>exit_stage_left</c> distance (180..360 px past the target) from inside
    /// the recess lands inside it, east and west, on its floor.
    /// </summary>
    [TestCase]
    public void TheTragedyKingsExitStageLeftStaysInsideTheLevel10Recess() {
        const float boardTop = 1120f;
        const float boardUnderside = 1160f;
        const float recessWest = 8970f;
        const float recessEast = 9670f;
        const float recessFloor = 1260f;
        const float wall = 20f;
        var world = new RectWorld();
        world.Solid(new Rect2(8320f, boardTop, recessWest - 8320f, boardUnderside - boardTop));
        world.Solid(new Rect2(recessEast, boardTop, 10240f - recessEast, boardUnderside - boardTop));
        world.Solid(new Rect2(recessWest, recessFloor, recessEast - recessWest, 40f));
        world.Solid(new Rect2(recessWest - wall, boardUnderside, wall, recessFloor + 40f - boardUnderside));
        world.Solid(new Rect2(recessEast, boardUnderside, wall, recessFloor + 40f - boardUnderside));
        world.OneWay(new Rect2(9190f, boardTop, 260f, 8f));

        var king = new Vector2(9320f, recessFloor);
        for (float distance = 180f; distance <= 360f; distance += 20f) {
            foreach ((float targetX, float side) in new[] { (9500f, 1f), (9150f, -1f) }) {
                var requested = new Vector2(targetX + side * distance, recessFloor);
                RelocationResult result = RelocationRules.Resolve(world, king, requested, Boss);
                AssertThat(result.Position.X >= recessWest + 48f - 0.01f && result.Position.X <= recessEast - 48f + 0.01f)
                    .OverrideFailureMessage(
                        $"exit_stage_left to {requested} landed at {result} — outside the recess {recessWest}..{recessEast}.")
                    .IsTrue();
                AssertFloat(result.Position.Y).IsEqualApprox(recessFloor, 0.01f);
                AssertThat(result.Outcome is RelocationOutcome.AsRequested or RelocationOutcome.ClippedAtWall)
                    .OverrideFailureMessage($"exit_stage_left to {requested}: {result}.")
                    .IsTrue();
            }
        }
    }

    // === A world of axis-aligned rects ===================================

    /// <summary>
    /// Solids are Environment (walls and floors alike — a wall ray or the swept
    /// box meets a floor edge); one-way platforms are floor support only. A wall
    /// ray that starts inside a rect does not report it, as a Godot ray against a
    /// convex shape does not; the swept box ignores a rect it starts inside or
    /// touching, as the engine's shape cast does, and meets one it touches on the
    /// way; the floor probe reports a rect it starts inside, as the engine adapter
    /// asks it to.
    /// </summary>
    private sealed class RectWorld : IRelocationWorld {
        private readonly List<Rect2> _solids = new();
        private readonly List<Rect2> _oneWays = new();
        public bool Queryable = true;

        public void Solid(Rect2 rect) => _solids.Add(rect);
        public void OneWay(Rect2 rect) => _oneWays.Add(rect);

        public bool CanQuery => Queryable;

        public float SweepContactTravel(Rect2 box, Vector2 motion) {
            float best = -1f;
            Vector2 from = box.GetCenter();
            Vector2 to = from + motion;
            foreach (Rect2 rect in _solids) {
                // The box's centre meets the rect grown by the box exactly where the box meets the rect.
                var grown = new Rect2(rect.Position - box.Size * 0.5f, rect.Size + box.Size);
                if (ContainsOrTouches(grown, from)) continue;
                float t = SegmentEntry(grown, from, to);
                if (t < 0f) continue;
                float distance = motion.Length() * t;
                if (best < 0f || distance < best) best = distance;
            }
            return best;
        }

        public float WallHitDistance(Vector2 from, Vector2 to) {
            float best = -1f;
            foreach (Rect2 rect in _solids) {
                if (Contains(rect, from)) continue;
                float t = SegmentEntry(rect, from, to);
                if (t < 0f) continue;
                float distance = from.DistanceTo(to) * t;
                if (best < 0f || distance < best) best = distance;
            }
            return best;
        }

        public bool HasFloorOnSegment(Vector2 from, Vector2 to) {
            foreach (Rect2 rect in _solids) {
                if (Contains(rect, from) || SegmentEntry(rect, from, to) >= 0f) return true;
            }
            foreach (Rect2 rect in _oneWays) {
                if (Contains(rect, from) || SegmentEntry(rect, from, to) >= 0f) return true;
            }
            return false;
        }

        private static bool Contains(Rect2 rect, Vector2 point) =>
            point.X > rect.Position.X && point.X < rect.End.X
            && point.Y > rect.Position.Y && point.Y < rect.End.Y;

        private static bool ContainsOrTouches(Rect2 rect, Vector2 point) =>
            point.X >= rect.Position.X && point.X <= rect.End.X
            && point.Y >= rect.Position.Y && point.Y <= rect.End.Y;

        /// <summary>Slab test: the segment parameter where it enters the rect, or -1.</summary>
        private static float SegmentEntry(Rect2 rect, Vector2 from, Vector2 to) {
            Vector2 delta = to - from;
            float tMin = 0f;
            float tMax = 1f;
            for (int axis = 0; axis < 2; axis++) {
                float origin = axis == 0 ? from.X : from.Y;
                float direction = axis == 0 ? delta.X : delta.Y;
                float low = axis == 0 ? rect.Position.X : rect.Position.Y;
                float high = axis == 0 ? rect.End.X : rect.End.Y;
                if (Mathf.Abs(direction) < 1e-6f) {
                    if (origin < low || origin > high) return -1f;
                    continue;
                }
                float t1 = (low - origin) / direction;
                float t2 = (high - origin) / direction;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return -1f;
            }
            return tMin;
        }
    }
}
