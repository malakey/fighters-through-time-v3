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
    }

    /// <summary>
    /// Immutable fixed-point stage geometry for the deterministic Fighter
    /// simulation: solid side walls and ceiling, bottom blast zone, one-way
    /// platforms, spawn distance, and authored hazard/orb anchor points.
    /// Geometry is constant for the whole match, is derived from the stage ID on
    /// every peer, and never enters rollback snapshots. Presentation scenes must
    /// mirror these values through the driver's 62.5 pixels-per-unit contract.
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

        private static readonly FighterStagePlatform[] NoPlatforms = System.Array.Empty<FighterStagePlatform>();
        private static readonly FP64[] NoHazardAnchors = System.Array.Empty<FP64>();
        private static readonly FPVector2[] NoOrbAnchors = System.Array.Empty<FPVector2>();

        private FighterStageGeometry(
            string stageID,
            FP64 leftWall,
            FP64 rightWall,
            FP64 ceiling,
            FP64 bottomBlastZone,
            int spawnDistance,
            FighterStagePlatform[] platforms,
            FP64[] hazardAnchorXs,
            FPVector2[] orbAnchors) {
            StageID = stageID;
            LeftWall = leftWall;
            RightWall = rightWall;
            Ceiling = ceiling;
            BottomBlastZone = bottomBlastZone;
            SpawnDistance = spawnDistance;
            Platforms = platforms;
            HazardAnchorXs = hazardAnchorXs;
            OrbAnchors = orbAnchors;
        }

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

        /// <summary>
        /// Florence Workshop: central floor with two wooden gear platforms, wall
        /// steam-pipe hazard anchors, and orb anchors above each gear platform.
        /// </summary>
        public static FighterStageGeometry Florence { get; } = new(
            stageID: "florence_workshop",
            leftWall: FP64.FromInt(-9),
            rightWall: FP64.FromInt(9),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromInt(-4), FP64.FromDouble(2.4), FP64.FromDouble(1.6)),
                new FighterStagePlatform(FP64.FromInt(4), FP64.FromDouble(2.4), FP64.FromDouble(1.6))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-6), FP64.Zero, FP64.FromInt(6) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromInt(-4), FP64.FromDouble(2.9)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5)),
                new FPVector2(FP64.FromInt(4), FP64.FromDouble(2.9))
            });

        /// <summary>
        /// Resolves authored geometry for a stage ID. Stages without a production
        /// geometry entry keep the legacy flat arena until they are authored.
        /// </summary>
        public static FighterStageGeometry ForStage(string stageID) => stageID switch {
            "florence_workshop" => Florence,
            _ => Default
        };
    }
}
