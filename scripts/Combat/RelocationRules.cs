using Godot;

namespace FTT.Combat {

    /// <summary>
    /// The shape of a body being moved by a direct position write, measured from
    /// its origin (its feet: every Story combatant's origin sits on the floor line
    /// with its collision shape above it).
    /// </summary>
    public readonly struct RelocationBody {
        /// <summary>Half the collision shape's width.</summary>
        public readonly float HalfWidth;
        /// <summary>Feet-to-centre offset of the collision shape (negative: the centre is above the feet).</summary>
        public readonly float CentreOffsetY;
        /// <summary>Half the collision shape's height.</summary>
        public readonly float HalfHeight;
        /// <summary>
        /// The body falls (gravity applies), so its landing needs a floor under
        /// it. False for authored flyers, which ignore floors.
        /// </summary>
        public readonly bool NeedsFloor;
        /// <summary>
        /// The body's authored identity is passing through walls (the Rift
        /// Phantom), so the wall half of the rule does not apply to it.
        /// </summary>
        public readonly bool PassesWalls;

        public RelocationBody(float halfWidth, float centreOffsetY, float halfHeight, bool needsFloor, bool passesWalls) {
            HalfWidth = Mathf.Max(0f, halfWidth);
            CentreOffsetY = centreOffsetY;
            HalfHeight = Mathf.Max(0f, halfHeight);
            NeedsFloor = needsFloor;
            PassesWalls = passesWalls;
        }
    }

    /// <summary>What a relocation resolved to.</summary>
    public enum RelocationOutcome {
        /// <summary>
        /// The world could not be asked (no tree, no physics space, a physics
        /// in/out flush, or a shapeless owner): the requested point stands, as
        /// it always did. A caller that can wait for a queryable world defers.
        /// </summary>
        Unverified,
        /// <summary>The requested point is clear and (when needed) supported.</summary>
        AsRequested,
        /// <summary>The travel met a wall; the landing stops short of it, supported.</summary>
        ClippedAtWall,
        /// <summary>
        /// The landing had no floor within <see cref="RelocationRules.FloorSearchDepthPixels"/>;
        /// it stepped back toward the origin to the first supported point.
        /// </summary>
        SteppedBackToSupport,
        /// <summary>
        /// No point between the landing and the origin had a floor except the
        /// origin itself: the write is refused and the body stays where it was.
        /// </summary>
        HeldAtOrigin,
        /// <summary>
        /// The body was already over nothing at its origin (airborne over a
        /// void, or a world with no floor geometry at all) — no floor under its
        /// centre or either half-width extent. The floor half has no support to
        /// protect, so the wall-clipped landing stands.
        /// </summary>
        OriginUnsupported
    }

    /// <summary>The landing a relocation resolved to.</summary>
    public readonly struct RelocationResult {
        public readonly Vector2 Position;
        public readonly RelocationOutcome Outcome;
        /// <summary>True when a wall shortened the travel (whatever happened after).</summary>
        public readonly bool ClippedAtWall;

        public RelocationResult(Vector2 position, RelocationOutcome outcome, bool clippedAtWall) {
            Position = position;
            Outcome = outcome;
            ClippedAtWall = clippedAtWall;
        }

        /// <summary>The world was asked: the landing is the rule's, not merely the request.</summary>
        public bool Verified => Outcome != RelocationOutcome.Unverified;

        /// <summary>The landing is the body's own origin — nothing moved.</summary>
        public bool HeldInPlace => Outcome == RelocationOutcome.HeldAtOrigin;

        public override string ToString() => $"{Outcome} at {Position}{(ClippedAtWall ? " (clipped)" : "")}";
    }

    /// <summary>
    /// The three questions a relocation asks the world. <see cref="SafeRelocation"/>
    /// answers them with an Environment shape cast and Environment rays; tests
    /// answer them with plain rects.
    /// </summary>
    public interface IRelocationWorld {
        /// <summary>False when the world cannot be asked right now (the write stays unverified).</summary>
        bool CanQuery { get; }

        /// <summary>
        /// How far the axis-aligned <paramref name="box"/> (global) can travel
        /// along <paramref name="motion"/> before it first touches a solid wall
        /// (Environment), or a negative value when the whole motion is clear. A
        /// shape the box already overlaps where it starts is not reported (the
        /// engine's shape cast ignores it).
        /// </summary>
        float SweepContactTravel(Rect2 box, Vector2 motion);

        /// <summary>
        /// Distance from <paramref name="from"/> to the first solid wall
        /// (Environment) on the segment to <paramref name="to"/>, or a negative
        /// value when the segment is clear. A shape the segment starts inside is
        /// not reported.
        /// </summary>
        float WallHitDistance(Vector2 from, Vector2 to);

        /// <summary>
        /// True when the vertical segment from <paramref name="from"/> down to
        /// <paramref name="to"/> meets something a body stands on (Environment or a
        /// one-way platform), including a shape the segment starts inside.
        /// </summary>
        bool HasFloorOnSegment(Vector2 from, Vector2 to);
    }

    /// <summary>
    /// Playtest pass 2026-10-09 (VERIFY-L10-ARENA-RECESS-EXIT, boss half): the
    /// one statement of how a Story <b>direct position write</b> meets the world.
    /// A write that is not a <c>MoveAndSlide</c> — an enemy or boss teleport, a
    /// summon's spawn point, a perk's blink — never collides with anything on its
    /// own, so before this the Tragedy King's <c>exit_stage_left</c> could put him
    /// past the Level 10 arena recess's wall, under the boards, where he fell
    /// forever and the level could not be won.
    ///
    /// <para>The rule, in order:</para>
    /// <list type="number">
    /// <item><b>Walls.</b> The body's own collision box (<see cref="SweepBox"/>:
    ///       its full width, from its head down to just above the floor it
    ///       stands on) is swept from its origin toward the destination and
    ///       stops <see cref="WallClearancePixels"/> short of the first solid
    ///       wall it would touch, so no slab however thin can lie between the
    ///       probes. Three rays across the shape (<see cref="WallRayOffsetY"/>)
    ///       still run beside the sweep, for a wall the box already overlaps where
    ///       it starts, which a shape cast does not report. Skipped for a body
    ///       whose identity is passing through walls
    ///       (<see cref="RelocationBody.PassesWalls"/>).</item>
    /// <item><b>Floor.</b> A body that falls needs a floor within
    ///       <see cref="FloorSearchDepthPixels"/> under its landing x. Without one
    ///       the landing steps back toward the origin every
    ///       <see cref="StepBackPixels"/> to the first supported point; with no
    ///       such point short of the origin the write is refused
    ///       (<see cref="RelocationOutcome.HeldAtOrigin"/>). Skipped for flyers.
    ///       A body that was already over nothing keeps its wall-clipped landing
    ///       (<see cref="RelocationOutcome.OriginUnsupported"/>): the rule protects
    ///       support a body has; it does not refuse for lack of a floor that the
    ///       origin never had. "Over nothing" is read across the body's whole
    ///       width (<see cref="HasOriginSupport"/>), so a body standing on a
    ///       ledge with its centre past the edge is supported.</item>
    /// </list>
    ///
    /// <para>A summon (<see cref="ResolveSummon"/>) is the same rule for the
    /// minion's body, travelling out from the part of the summoner that stands
    /// on something (<see cref="EmergencePoint"/>).</para>
    ///
    /// <para>Pure: the world is an <see cref="IRelocationWorld"/>, so the whole
    /// rule is pinnable without a physics space. Story-only; nothing in
    /// <c>scripts/FighterSim/</c> may consume it (the sim's fixed-point bodies
    /// have their own stage geometry).</para>
    /// </summary>
    public static class RelocationRules {

        /// <summary>
        /// The landing stops this far short of a wall face beyond the body's own
        /// half-width, so float noise never leaves the shape touching the far
        /// side of a 20 px wall.
        /// </summary>
        public const float WallClearancePixels = 2f;

        /// <summary>
        /// How far below a landing's feet a floor still counts as support: a
        /// drop, never a void. Eight world units (60 px per unit) — deeper than
        /// every authored step-down a boss arena has (Level 10's recess is 140 px
        /// under the boards), shallower than a fall that leaves the arena.
        /// </summary>
        public const float FloorSearchDepthPixels = 480f;

        /// <summary>
        /// The floor probe starts this far above the feet, so a landing whose
        /// feet sit a little inside a low step or a slope still finds its floor.
        /// </summary>
        public const float FloorProbeRisePixels = 24f;

        /// <summary>Step size of the walk back toward the origin when a landing has no floor.</summary>
        public const float StepBackPixels = 16f;

        /// <summary>A travel shorter than this is not probed for walls.</summary>
        public const float MinimumTravelPixels = 0.5f;

        /// <summary>
        /// Where the three wall rays run, as fractions of the half-height from the
        /// shape's centre: centre, and 70 % of the way to the top and to the
        /// bottom — inside the body, clear of the floor it stands on. Since the
        /// sweep (2026-10-09) the rays only guard a wall the body's box already
        /// overlaps where it starts; the lowest ray's line is also the bottom of
        /// <see cref="SweepBox"/>.
        /// </summary>
        public const float WallRaySpreadFraction = 0.7f;

        /// <summary>Feet-relative y offset of wall ray <paramref name="index"/> (0..2).</summary>
        public static float WallRayOffsetY(in RelocationBody body, int index) =>
            body.CentreOffsetY + (index - 1) * WallRaySpreadFraction * body.HalfHeight;

        /// <summary>
        /// The box swept along a travel, relative to the feet: the body's
        /// collision shape at its full width and up to its head, cut off at the
        /// lowest wall ray's line (30 % of the half-height above the shape's
        /// bottom — 19.2 px on the 128 px boss body). That band is the floor the
        /// body stands on: without it the box would touch its own floor, stop at
        /// the seam where two floor pieces meet and stop at every low step, none
        /// of which the old rays stopped at either. Everything above the band, the
        /// whole way to the head, is swept.
        /// </summary>
        public static Rect2 SweepBox(in RelocationBody body) {
            float top = body.CentreOffsetY - body.HalfHeight;
            float bottom = WallRayOffsetY(body, 2);
            return new Rect2(-body.HalfWidth, top, body.HalfWidth * 2f, Mathf.Max(0f, bottom - top));
        }

        /// <summary>
        /// The pure wall clip: how far along a travel of <paramref name="travel"/>
        /// the body may go when the nearest wall face is <paramref name="wallDistance"/>
        /// away (negative: no wall).
        /// </summary>
        public static float AllowedTravel(float travel, float wallDistance, float halfWidth) {
            if (wallDistance < 0f) return travel;
            return Mathf.Clamp(wallDistance - halfWidth - WallClearancePixels, 0f, travel);
        }

        /// <summary>
        /// The pure sweep clip: how far along a travel of <paramref name="travel"/>
        /// the body may go when its swept box first touches a wall after
        /// <paramref name="contactTravel"/> (negative: the sweep met nothing) —
        /// <see cref="WallClearancePixels"/> short of the contact.
        /// </summary>
        public static float AllowedTravelToContact(float travel, float contactTravel) {
            if (contactTravel < 0f) return travel;
            return Mathf.Clamp(contactTravel - WallClearancePixels, 0f, travel);
        }

        /// <summary>The floor probe segment under a landing's feet.</summary>
        public static (Vector2 From, Vector2 To) FloorProbe(Vector2 feet) => (
            new Vector2(feet.X, feet.Y - FloorProbeRisePixels),
            new Vector2(feet.X, feet.Y + FloorSearchDepthPixels));

        /// <summary>Resolves a direct write from <paramref name="origin"/> to <paramref name="destination"/>.</summary>
        public static RelocationResult Resolve(IRelocationWorld world, Vector2 origin, Vector2 destination,
            in RelocationBody body) {
            if (world == null || !world.CanQuery) {
                return new RelocationResult(destination, RelocationOutcome.Unverified, false);
            }

            Vector2 landing = destination;
            bool clipped = false;
            Vector2 travel = destination - origin;
            float length = travel.Length();
            if (!body.PassesWalls && length > MinimumTravelPixels) {
                Vector2 direction = travel / length;
                // The sweep: the body's own box along the travel, plus the
                // clearance, so a wall just past the destination still keeps the
                // landing clear of it.
                Rect2 box = SweepBox(body);
                box.Position += origin;
                float contact = world.SweepContactTravel(box, direction * (length + WallClearancePixels));
                // The rays: a wall the box already overlaps where it starts, which
                // the sweep does not report.
                float reach = length + body.HalfWidth + WallClearancePixels;
                float nearest = -1f;
                for (int index = 0; index < 3; index++) {
                    Vector2 from = origin + new Vector2(0f, WallRayOffsetY(body, index));
                    float hit = world.WallHitDistance(from, from + direction * reach);
                    if (hit >= 0f && (nearest < 0f || hit < nearest)) nearest = hit;
                }
                float allowed = Mathf.Min(
                    AllowedTravelToContact(length, contact),
                    AllowedTravel(length, nearest, body.HalfWidth));
                if (allowed < length) {
                    landing = origin + direction * allowed;
                    clipped = true;
                }
            }

            if (!body.NeedsFloor || HasSupport(world, landing)) {
                return new RelocationResult(landing,
                    clipped ? RelocationOutcome.ClippedAtWall : RelocationOutcome.AsRequested, clipped);
            }

            // The travel line up to the landing is clear of walls, so every point
            // stepped back along it is too.
            Vector2 back = origin - landing;
            float span = back.Length();
            if (span > MinimumTravelPixels) {
                Vector2 toward = back / span;
                for (float stepped = StepBackPixels; stepped < span; stepped += StepBackPixels) {
                    Vector2 candidate = landing + toward * stepped;
                    if (HasSupport(world, candidate)) {
                        return new RelocationResult(candidate, RelocationOutcome.SteppedBackToSupport, clipped);
                    }
                }
            }
            if (HasOriginSupport(world, origin, body)) {
                return new RelocationResult(origin, RelocationOutcome.HeldAtOrigin, clipped);
            }
            return new RelocationResult(landing, RelocationOutcome.OriginUnsupported, clipped);
        }

        /// <summary>
        /// Resolves a summon: a minion shaped <paramref name="minion"/> requested
        /// at <paramref name="requested"/> by a summoner whose feet are at
        /// <paramref name="summonerFeet"/> and whose body reaches
        /// <paramref name="summonerHalfWidth"/> either side of them. The minion
        /// travels out from <see cref="EmergencePoint"/> — the summoner's feet
        /// unless the minion has no floor there — under <see cref="Resolve"/>, so
        /// its wall clip is its own swept box and its support is its own centre at
        /// the point it lands. A refused write (<see cref="RelocationOutcome.HeldAtOrigin"/>)
        /// lands it at the emergence point, which has a floor under it whenever
        /// any point under the summoner gives the minion one.
        /// </summary>
        public static RelocationResult ResolveSummon(IRelocationWorld world, Vector2 summonerFeet,
            float summonerHalfWidth, Vector2 requested, in RelocationBody minion) {
            if (world == null || !world.CanQuery) {
                return new RelocationResult(requested, RelocationOutcome.Unverified, false);
            }
            Vector2 emergence = EmergencePoint(world, summonerFeet, summonerHalfWidth, requested.X, minion);
            return Resolve(world, emergence, requested, minion);
        }

        /// <summary>
        /// Where a summoned minion leaves its summoner (2026-10-09). The
        /// summoner's feet when the minion has a floor there (or needs none);
        /// otherwise the point under the summoner's body nearest its centre —
        /// every <see cref="StepBackPixels"/> out to its half-width, the requested
        /// side first at equal distance — that the minion's own body reaches from
        /// the summoner's centre without meeting a wall and has a floor under (a
        /// <see cref="Resolve"/> of that short move lands as requested). A
        /// summoner standing on a ledge with its centre past the edge is held up
        /// by the rest of its body, and a narrower minion emerges from that part;
        /// judged instead by its own width around the summoner's unsupported
        /// centre, it read as "already over nothing" and was dropped into the
        /// void. With no such point (a summoner over nothing) the feet stand.
        /// </summary>
        public static Vector2 EmergencePoint(IRelocationWorld world, Vector2 summonerFeet,
            float summonerHalfWidth, float towardX, in RelocationBody minion) {
            if (world == null || !world.CanQuery) return summonerFeet;
            if (!minion.NeedsFloor || HasSupport(world, summonerFeet)) return summonerFeet;
            float reach = Mathf.Max(0f, summonerHalfWidth);
            float first = towardX >= summonerFeet.X ? 1f : -1f;
            for (float stepped = StepBackPixels; reach > 0f; stepped += StepBackPixels) {
                float offset = Mathf.Min(stepped, reach);
                for (int side = 0; side < 2; side++) {
                    float direction = side == 0 ? first : -first;
                    var candidate = new Vector2(summonerFeet.X + direction * offset, summonerFeet.Y);
                    if (!HasSupport(world, candidate)) continue;
                    if (Resolve(world, summonerFeet, candidate, minion).Outcome == RelocationOutcome.AsRequested) {
                        return candidate;
                    }
                }
                if (offset >= reach) break;
            }
            return summonerFeet;
        }

        private static bool HasSupport(IRelocationWorld world, Vector2 feet) {
            (Vector2 from, Vector2 to) = FloorProbe(feet);
            return world.HasFloorOnSegment(from, to);
        }

        /// <summary>
        /// Whether the body already stands on something at its origin, read across
        /// its whole width: the centre and both half-width extents, any one of
        /// them finding a floor. A body standing on a ledge with its centre past
        /// the edge is held up by the rest of its shape; a single probe at the
        /// centre called it "already over nothing" and let a write toward the
        /// void land there (review of the relocation rule, 2026-10-09). A
        /// landing's support stays the centre probe: the body is put down there
        /// whole, and a landing with only an extent on a floor would hang off it.
        /// </summary>
        public static bool HasOriginSupport(IRelocationWorld world, Vector2 origin, in RelocationBody body) {
            if (HasSupport(world, origin)) return true;
            if (body.HalfWidth <= 0f) return false;
            return HasSupport(world, new Vector2(origin.X - body.HalfWidth, origin.Y))
                || HasSupport(world, new Vector2(origin.X + body.HalfWidth, origin.Y));
        }
    }
}
