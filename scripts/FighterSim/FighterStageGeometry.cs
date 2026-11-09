using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// One deterministic one-way platform. Fighters land on the surface only when
    /// falling across it from above; drop-through input passes through it.
    /// </summary>
    public readonly struct FighterStagePlatform {
        public readonly FP64 CenterX;
        public readonly FP64 SurfaceY;
        public readonly FP64 HalfWidth;

        public FighterStagePlatform(FP64 centerX, FP64 surfaceY, FP64 halfWidth) {
            CenterX = centerX;
            SurfaceY = surfaceY;
            HalfWidth = halfWidth;
        }

        public bool Supports(FP64 x) => FP64.Abs(x - CenterX) <= HalfWidth;

        /// <summary>The platform's left (side 0) or right (side 1) end, in world units.</summary>
        public FP64 EdgeX(int side) => side == 0 ? CenterX - HalfWidth : CenterX + HalfWidth;

        /// <summary>
        /// Where a fighter hangs off <paramref name="side"/>: the edge nudged
        /// <see cref="FighterLedgeRules.HangOutwardOffset"/> units away from the
        /// platform, one unit below the surface (gameplay-feel plan §2.11).
        /// </summary>
        public FPVector2 HangPosition(int side) => new(
            side == 0
                ? EdgeX(0) - FighterLedgeRules.HangOutwardOffset
                : EdgeX(1) + FighterLedgeRules.HangOutwardOffset,
            SurfaceY - FighterLedgeRules.HangDepth);

        /// <summary>
        /// CM-MOVE (decision 5.2, 2026-10-16): where every Fighter ledge get-up
        /// stands the fighter — on the surface,
        /// <see cref="FTT.Combat.BasicComboRules.LedgeStandInsetUnits"/> (0.5 u)
        /// inside the <paramref name="side"/> end, or at the centre of a platform
        /// narrower than that.
        /// </summary>
        public FPVector2 StandPosition(int side) {
            FP64 inset = FP64.Min(LedgeStandInset, HalfWidth);
            return new FPVector2(side == 0 ? EdgeX(0) + inset : EdgeX(1) - inset, SurfaceY);
        }

        private static readonly FP64 LedgeStandInset =
            FP64.FromDouble(FTT.Combat.BasicComboRules.LedgeStandInsetUnits);

        /// <summary>
        /// True when a fighter at <paramref name="position"/> is inside the capture
        /// box of <paramref name="side"/>: within
        /// <see cref="FighterLedgeRules.CaptureHalfWidth"/> horizontally of the edge
        /// and in the <see cref="FighterLedgeRules.CaptureDepth"/> band just under
        /// the surface. Geometry only — the fighter-state conditions live in
        /// <see cref="FighterLedgeRules.CanGrab"/>.
        /// </summary>
        public bool IsInCaptureBox(in FPVector2 position, int side) =>
            IsInCaptureBox(in position, side, FP64.Zero);

        /// <summary>
        /// Package 13 W5: the capture box widened by
        /// <paramref name="extraHalfWidth"/> horizontally — North Star Leap
        /// "snaps to ledges from 0.5 units farther than normal". Zero is the
        /// ordinary box.
        /// </summary>
        public bool IsInCaptureBox(in FPVector2 position, int side, FP64 extraHalfWidth) =>
            FP64.Abs(position.x - EdgeX(side)) <= FighterLedgeRules.CaptureHalfWidth + extraHalfWidth
            && position.y <= SurfaceY
            && position.y >= SurfaceY - FighterLedgeRules.CaptureDepth;
    }

    /// <summary>
    /// Immutable fixed-point stage geometry for the deterministic Fighter
    /// simulation: solid side walls and ceiling, bottom blast zone, one-way
    /// platforms, the main floor's solid segments, spawn distance, and authored
    /// hazard/orb anchor points.
    /// Geometry is constant for the whole match, is derived from the stage ID on
    /// every peer, and never enters rollback snapshots. Presentation scenes must
    /// mirror these values through the driver's 62.5 pixels-per-unit contract.
    ///
    /// <para><b>Floor segments (V7 pillar decision, Option A — resolves audit
    /// H-11; Package 11 A9).</b> The main floor used to be an implicit wall-to-wall
    /// plane at y = 0, which made the bottom blast zone unreachable on every
    /// authored stage. It is now authored as <see cref="FloorSegments"/>: an empty
    /// array means "unbroken floor, wall to wall" and is what the seven Sealed
    /// stages and the legacy flat arena keep, so their behaviour is bit-identical
    /// to before. The three Open stages author real segments with gaps between
    /// them; <see cref="HasFloorSupport"/> is the per-x lookup the movement system
    /// consults instead of assuming a plane, and every segment end that faces a
    /// pit is a true, grabbable ledge.</para>
    /// </summary>
    public sealed class FighterStageGeometry {
        public readonly string StageID;
        public readonly FP64 LeftWall;
        public readonly FP64 RightWall;
        public readonly FP64 Ceiling;
        public readonly FP64 BottomBlastZone;
        public readonly int SpawnDistance;
        public readonly FighterStagePlatform[] Platforms;
        /// <summary>Authored hazard spawn X anchors. Empty falls back to the legacy random range.</summary>
        public readonly FP64[] HazardAnchorXs;
        /// <summary>Authored Chronal Orb anchor points. Empty falls back to the legacy random range.</summary>
        public readonly FPVector2[] OrbAnchors;
        /// <summary>
        /// The solid spans of the main floor, reusing <see cref="FighterStagePlatform"/>
        /// with <c>SurfaceY = 0</c> so the ledge helpers come for free. <b>Empty
        /// means unbroken wall-to-wall floor</b> — the Sealed-stage and legacy
        /// sentinel. Segments never overlap and are authored left to right.
        /// </summary>
        public readonly FighterStagePlatform[] FloorSegments;
        /// <summary>
        /// Where the Chronal Respawn Platform materialises. Stage centre +3 for
        /// every Sealed stage; an Open stage whose centre is a pit overrides it so
        /// a knocked-out fighter is never dropped straight back into the hole.
        /// </summary>
        public readonly FPVector2 RespawnPlatformPosition;

        private static readonly FighterStagePlatform[] NoPlatforms = System.Array.Empty<FighterStagePlatform>();
        private static readonly FP64[] NoHazardAnchors = System.Array.Empty<FP64>();
        private static readonly FPVector2[] NoOrbAnchors = System.Array.Empty<FPVector2>();
        /// <summary>The "unbroken floor, wall to wall" sentinel.</summary>
        private static readonly FighterStagePlatform[] NoFloorSegments =
            System.Array.Empty<FighterStagePlatform>();

        private FighterStageGeometry(
            string stageID,
            FP64 leftWall,
            FP64 rightWall,
            FP64 ceiling,
            FP64 bottomBlastZone,
            int spawnDistance,
            FighterStagePlatform[] platforms,
            FP64[] hazardAnchorXs,
            FPVector2[] orbAnchors,
            FighterStagePlatform[] floorSegments = null,
            FPVector2? respawnPlatformPosition = null) {
            StageID = stageID;
            LeftWall = leftWall;
            RightWall = rightWall;
            Ceiling = ceiling;
            BottomBlastZone = bottomBlastZone;
            SpawnDistance = spawnDistance;
            Platforms = platforms;
            HazardAnchorXs = hazardAnchorXs;
            OrbAnchors = orbAnchors;
            FloorSegments = floorSegments ?? NoFloorSegments;
            RespawnPlatformPosition =
                respawnPlatformPosition ?? FighterMatchFlowRules.RespawnPlatformPosition;
        }

        /// <summary>
        /// True when solid main floor exists at <paramref name="x"/>. An unbroken
        /// floor (no authored segments) supports every x, which is what keeps the
        /// seven Sealed stages and the legacy flat arena on exactly their old
        /// behaviour.
        /// </summary>
        public bool HasFloorSupport(FP64 x) {
            if (FloorSegments.Length == 0) return true;
            for (int index = 0; index < FloorSegments.Length; index++) {
                if (FloorSegments[index].Supports(x)) return true;
            }
            return false;
        }

        /// <summary>True when the stage authors floor segments, i.e. it has pits.</summary>
        public bool IsOpenStage => FloorSegments.Length > 0;

        /// <summary>
        /// The nearest floor-segment end facing a pit in <paramref name="direction"/>
        /// (−1 left, +1 right) from <paramref name="x"/>. False on an unbroken
        /// floor or when no such end lies that way. Pure geometry; the CPU
        /// observation fill is its only consumer today.
        /// </summary>
        public bool TryGetNearestFloorEdge(FP64 x, int direction, out FP64 edgeX) {
            edgeX = FP64.Zero;
            if (FloorSegments.Length == 0 || direction == 0) return false;
            bool found = false;
            for (int index = 0; index < FloorSegments.Length; index++) {
                for (int side = 0; side < 2; side++) {
                    if (!IsFloorLedge(index, side)) continue;
                    FP64 candidate = FloorSegments[index].EdgeX(side);
                    if (direction > 0 ? candidate < x : candidate > x) continue;
                    if (found && (direction > 0 ? candidate >= edgeX : candidate <= edgeX)) continue;
                    edgeX = candidate;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// A floor-segment end is a true ledge only when it faces a pit. Ends that
        /// sit on a solid side wall are not grabbable — a fighter can never get
        /// below the floor plane there — and rejecting them in one place keeps
        /// <see cref="TryFindLedge"/> and <see cref="TryGetHangPosition"/> from
        /// ever disagreeing about a snapshotted anchor.
        /// </summary>
        private bool IsFloorLedge(int segmentIndex, int side) {
            FP64 edge = FloorSegments[segmentIndex].EdgeX(side);
            return edge > LeftWall && edge < RightWall;
        }

        /// <summary>
        /// CM-MOVE test seam (2026-10-16): a geometry with the legacy arena's
        /// walls, ceiling and blast zone and the caller's platforms (and optional
        /// floor segments), so a suite can put a one-way top at an exact height —
        /// the lip pop's 0.2 u band and the double-tap drop-through are pinned this
        /// way. Never a production stage: no catalog entry resolves to it.
        /// </summary>
        internal static FighterStageGeometry ForTest(
            string stageID,
            FighterStagePlatform[] platforms,
            int spawnDistance = 4,
            FighterStagePlatform[] floorSegments = null) => new(
            stageID: stageID,
            leftWall: FP64.FromInt(-10),
            rightWall: FP64.FromInt(10),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: spawnDistance,
            platforms: platforms ?? NoPlatforms,
            hazardAnchorXs: NoHazardAnchors,
            orbAnchors: NoOrbAnchors,
            floorSegments: floorSegments);

        /// <summary>Legacy flat arena identical to the original hardcoded bounds.</summary>
        public static FighterStageGeometry Default { get; } = new(
            stageID: "",
            leftWall: FP64.FromInt(-10),
            rightWall: FP64.FromInt(10),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: NoPlatforms,
            hazardAnchorXs: NoHazardAnchors,
            orbAnchors: NoOrbAnchors);

        // The selected A plate measurements own the canonical surfaces. Presentation scenes
        // mirror these values; no visual-only platform or spawn correction is applied at runtime.
        public static FighterStageGeometry Florence { get; } = FromReference("florence_workshop", new double[] { -6, 0, 6 });
        public static FighterStageGeometry Orleans { get; } = FromReference("orleans_vanguard", new double[] { -7, 7 });
        public static FighterStageGeometry Chicago { get; } = FromReference("chicago_exposition", new double[] { 0 });
        public static FighterStageGeometry Paris { get; } = FromReference("paris_bastille", new double[] { -6, 0, 6 });
        public static FighterStageGeometry Vesuvius { get; } = FromReference("vesuvius_caldera", new double[] { -6, -2, 2, 4.5 });
        public static FighterStageGeometry Nassau { get; } = FromReference("nassau_flagship", new double[] { -5, 0, 4 });
        public static FighterStageGeometry Alexandria { get; } = FromReference("alexandria_chambers", new double[] { -6, 0, 6 });
        public static FighterStageGeometry Berlin { get; } = FromReference("berlin_wall", new double[] { -4, 0, 4 });
        public static FighterStageGeometry Globe { get; } = FromReference("globe_theatre", new double[] { -5, 0, 5 });
        public static FighterStageGeometry Gettysburg { get; } = FromReference("gettysburg_ridge", new double[] { -5, 0, 5 });

        private static FighterStageGeometry FromReference(string stageID, double[] hazardXs) {
            FighterStageVisualProfile profile = FighterStageVisualProfile.Find(stageID);
            var platforms = new FighterStagePlatform[profile.PlatformSpans.Length];
            var floors = new FighterStagePlatform[profile.FloorSpans.Length];
            bool centerPit = stageID == "paris_bastille" || stageID == "globe_theatre";
            int groundOrbs = centerPit && stageID == "paris_bastille" ? 2 : platforms.Length == 3 ? 0 : 1;
            var orbs = new FPVector2[platforms.Length + groundOrbs];
            for (int i = 0; i < platforms.Length; i++) {
                var span = profile.PlatformSpans[i];
                platforms[i] = new FighterStagePlatform(
                    FP64.FromDouble((profile.WorldX(span.Left) + profile.WorldX(span.Right)) / 2),
                    FP64.FromDouble(profile.WorldY(span.Top)),
                    FP64.FromDouble((profile.WorldX(span.Right) - profile.WorldX(span.Left)) / 2));
                orbs[i] = new FPVector2(platforms[i].CenterX, platforms[i].SurfaceY + FP64.FromDouble(0.5));
            }
            for (int i = 0; i < floors.Length; i++) {
                var span = profile.FloorSpans[i];
                floors[i] = new FighterStagePlatform(
                    FP64.FromDouble((profile.WorldX(span.Left) + profile.WorldX(span.Right)) / 2), FP64.Zero,
                    FP64.FromDouble((profile.WorldX(span.Right) - profile.WorldX(span.Left)) / 2));
            }
            for (int i = 0; i < groundOrbs; i++) {
                orbs[platforms.Length + i] = new FPVector2(
                    FP64.FromInt(centerPit ? (i == 0 ? -6 : 6) : 0), FP64.FromDouble(0.5));
            }
            var hazards = new FP64[hazardXs.Length];
            for (int i = 0; i < hazards.Length; i++) hazards[i] = FP64.FromDouble(hazardXs[i]);
            return new FighterStageGeometry(stageID,
                FP64.FromDouble(profile.WorldX(0)), FP64.FromDouble(profile.WorldX(1672)),
                FP64.FromInt(9), FP64.FromInt(-5), profile.SpawnDistance, platforms, hazards, orbs, floors,
                centerPit ? new FPVector2(FP64.FromInt(-4), FP64.FromInt(3)) : null);
        }

        /// <summary>
        /// Every authored production stage geometry, in catalog order. Harnesses
        /// (rollback readiness, conformance sweeps) enumerate this rather than
        /// hardcoding a stage list, so a new entry is picked up automatically.
        /// </summary>
        public static FighterStageGeometry[] AllAuthored { get; } = {
            Florence, Orleans, Chicago, Paris, Vesuvius,
            Nassau, Alexandria, Berlin, Globe, Gettysburg
        };

        /// <summary>
        /// Resolves authored geometry for a stage ID. Stages without a production
        /// geometry entry keep the legacy flat arena until they are authored.
        /// </summary>
        public static FighterStageGeometry ForStage(string stageID) => stageID switch {
            "florence_workshop" => Florence,
            "orleans_vanguard" => Orleans,
            "chicago_exposition" => Chicago,
            "paris_bastille" => Paris,
            "vesuvius_caldera" => Vesuvius,
            "nassau_flagship" => Nassau,
            "alexandria_chambers" => Alexandria,
            "berlin_wall" => Berlin,
            "globe_theatre" => Globe,
            "gettysburg_ridge" => Gettysburg,
            _ => Default
        };

        /// <summary>
        /// Finds the ledge whose capture box contains <paramref name="position"/>
        /// and returns its encoded anchor. One-way platforms encode as
        /// <c>platformIndex * 2 + side</c>; main-floor segment ends continue the
        /// same run at <c>(Platforms.Length + segmentIndex) * 2 + side</c>, which
        /// is why this needed no new snapshot field —
        /// <c>FighterRuntimeComponent.LedgeAnchor</c> is already an <c>int</c> and
        /// the component is exactly full at 128 bytes.
        ///
        /// <para>Search order is platforms first (index ascending, left edge before
        /// right), then floor segments the same way, so overlapping capture boxes
        /// resolve identically on every peer. Segment ends that sit on a solid side
        /// wall are skipped: they are not ledges. The legacy flat arena has no
        /// platforms and no segments, and therefore no ledges.</para>
        /// </summary>
        public bool TryFindLedge(in FPVector2 position, out int anchor) =>
            TryFindLedge(in position, FP64.Zero, out anchor);

        /// <summary>
        /// Package 13 W5: <see cref="TryFindLedge(in FPVector2, out int)"/> with
        /// every capture box widened by <paramref name="extraHalfWidth"/> (North
        /// Star Leap's extended snap). Same search order, so overlapping boxes
        /// still resolve identically on every peer.
        /// </summary>
        public bool TryFindLedge(in FPVector2 position, FP64 extraHalfWidth, out int anchor) {
            for (int index = 0; index < Platforms.Length; index++) {
                for (int side = 0; side < 2; side++) {
                    if (!Platforms[index].IsInCaptureBox(in position, side, extraHalfWidth)) continue;
                    anchor = index * 2 + side;
                    return true;
                }
            }
            for (int index = 0; index < FloorSegments.Length; index++) {
                for (int side = 0; side < 2; side++) {
                    if (!IsFloorLedge(index, side)) continue;
                    if (!FloorSegments[index].IsInCaptureBox(in position, side, extraHalfWidth)) continue;
                    anchor = (Platforms.Length + index) * 2 + side;
                    return true;
                }
            }
            anchor = FighterLedgeRules.NoAnchor;
            return false;
        }

        /// <summary>
        /// Re-derives a hang position from a snapshotted anchor. Geometry never
        /// enters a snapshot, so this is how a rolled-back or restored frame gets
        /// its pinned position back. An anchor beyond this stage's platform and
        /// segment counts — or one naming a segment end that is not a true ledge —
        /// is rejected rather than clamped.
        /// </summary>
        public bool TryGetHangPosition(int anchor, out FPVector2 position) {
            position = FPVector2.Zero;
            if (anchor < 0) return false;
            int index = anchor / 2;
            int side = anchor % 2;
            if (index < Platforms.Length) {
                position = Platforms[index].HangPosition(side);
                return true;
            }
            int segmentIndex = index - Platforms.Length;
            if (segmentIndex >= FloorSegments.Length) return false;
            if (!IsFloorLedge(segmentIndex, side)) return false;
            position = FloorSegments[segmentIndex].HangPosition(side);
            return true;
        }

        /// <summary>
        /// CM-MOVE (decision 5.2): the get-up stand position for a snapshotted
        /// anchor — <see cref="FighterStagePlatform.StandPosition"/> of the
        /// platform or floor segment the anchor names, with exactly
        /// <see cref="TryGetHangPosition"/>'s anchor rules, so the two can never
        /// disagree about an anchor.
        /// </summary>
        public bool TryGetStandPosition(int anchor, out FPVector2 position) {
            position = FPVector2.Zero;
            if (anchor < 0) return false;
            int index = anchor / 2;
            int side = anchor % 2;
            if (index < Platforms.Length) {
                position = Platforms[index].StandPosition(side);
                return true;
            }
            int segmentIndex = index - Platforms.Length;
            if (segmentIndex >= FloorSegments.Length) return false;
            if (!IsFloorLedge(segmentIndex, side)) return false;
            position = FloorSegments[segmentIndex].StandPosition(side);
            return true;
        }
    }
}
