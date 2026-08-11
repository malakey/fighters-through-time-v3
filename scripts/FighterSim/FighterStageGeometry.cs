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
        /// Orléans Vanguard: symmetrical battlements — two low stone ledges and a
        /// central high platform forming a triangle. Trebuchet debris rolls in from
        /// either wall anchor.
        /// </summary>
        public static FighterStageGeometry Orleans { get; } = new(
            stageID: "orleans_vanguard",
            leftWall: FP64.FromInt(-9),
            rightWall: FP64.FromInt(9),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromDouble(-3.5), FP64.FromInt(2), FP64.FromDouble(1.4)),
                new FighterStagePlatform(FP64.FromDouble(3.5), FP64.FromInt(2), FP64.FromDouble(1.4)),
                new FighterStagePlatform(FP64.Zero, FP64.FromInt(4), FP64.FromDouble(1.4))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-8), FP64.FromInt(8) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromDouble(-3.5), FP64.FromDouble(2.5)),
                new FPVector2(FP64.FromDouble(3.5), FP64.FromDouble(2.5)),
                new FPVector2(FP64.Zero, FP64.FromDouble(4.5))
            });

        /// <summary>
        /// Chicago Exposition: a wide flat metallic stage with two high conductor
        /// coils as side platforms and a single central induction-grid anchor.
        /// </summary>
        public static FighterStageGeometry Chicago { get; } = new(
            stageID: "chicago_exposition",
            leftWall: FP64.FromInt(-10),
            rightWall: FP64.FromInt(10),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromInt(-6), FP64.FromDouble(3.2), FP64.FromDouble(1.5)),
                new FighterStagePlatform(FP64.FromInt(6), FP64.FromDouble(3.2), FP64.FromDouble(1.5))
            },
            hazardAnchorXs: new[] { FP64.Zero },
            orbAnchors: new[] {
                new FPVector2(FP64.FromInt(-6), FP64.FromDouble(3.7)),
                new FPVector2(FP64.FromInt(6), FP64.FromDouble(3.7)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5))
            });

        /// <summary>
        /// Paris Bastille: two broad drawbridge walkways over the central floor.
        /// The neural-dampening beam starts from one of three anchors and sweeps.
        /// </summary>
        public static FighterStageGeometry Paris { get; } = new(
            stageID: "paris_bastille",
            leftWall: FP64.FromInt(-9),
            rightWall: FP64.FromInt(9),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromInt(-4), FP64.FromDouble(2.6), FP64.FromInt(2)),
                new FighterStagePlatform(FP64.FromInt(4), FP64.FromDouble(2.6), FP64.FromInt(2))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-6), FP64.Zero, FP64.FromInt(6) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromInt(-4), FP64.FromDouble(3.1)),
                new FPVector2(FP64.FromInt(4), FP64.FromDouble(3.1)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5))
            });

        /// <summary>
        /// Vesuvius Caldera: the narrowest arena — two asymmetric stone ledges at
        /// different heights, with four rockfall anchors across the caldera floor.
        /// </summary>
        public static FighterStageGeometry Vesuvius { get; } = new(
            stageID: "vesuvius_caldera",
            leftWall: FP64.FromInt(-8),
            rightWall: FP64.FromInt(8),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromDouble(-4.5), FP64.FromInt(2), FP64.FromDouble(1.1)),
                new FighterStagePlatform(FP64.FromInt(3), FP64.FromDouble(3.5), FP64.FromDouble(1.1))
            },
            hazardAnchorXs: new[] {
                FP64.FromInt(-6), FP64.FromInt(-2), FP64.FromInt(2), FP64.FromInt(6)
            },
            orbAnchors: new[] {
                new FPVector2(FP64.FromDouble(-4.5), FP64.FromDouble(2.5)),
                new FPVector2(FP64.FromInt(3), FP64.FromInt(4)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5))
            });

        /// <summary>
        /// Nassau Flagship: a burning deck with two horizontal wooden yards. Mortar
        /// target grids land on three deck anchors.
        /// </summary>
        public static FighterStageGeometry Nassau { get; } = new(
            stageID: "nassau_flagship",
            leftWall: FP64.FromInt(-9),
            rightWall: FP64.FromInt(9),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromInt(-4), FP64.FromDouble(2.8), FP64.FromDouble(1.8)),
                new FighterStagePlatform(FP64.FromInt(4), FP64.FromDouble(2.8), FP64.FromDouble(1.8))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-5), FP64.Zero, FP64.FromInt(5) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromInt(-4), FP64.FromDouble(3.3)),
                new FPVector2(FP64.FromInt(4), FP64.FromDouble(3.3)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5))
            });

        /// <summary>
        /// Alexandria Chambers: a sandy floor with two low sarcophagus platforms —
        /// the lowest platforms in the catalog — and three sinkhole anchors.
        /// </summary>
        public static FighterStageGeometry Alexandria { get; } = new(
            stageID: "alexandria_chambers",
            leftWall: FP64.FromInt(-9),
            rightWall: FP64.FromInt(9),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromInt(-4), FP64.FromDouble(1.6), FP64.FromDouble(1.3)),
                new FighterStagePlatform(FP64.FromInt(4), FP64.FromDouble(1.6), FP64.FromDouble(1.3))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-6), FP64.Zero, FP64.FromInt(6) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromInt(-4), FP64.FromDouble(2.1)),
                new FPVector2(FP64.FromInt(4), FP64.FromDouble(2.1)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5))
            });

        /// <summary>
        /// Berlin Wall: a wide snowy street with two far-out guard-tower balconies.
        /// Searchlight columns sweep down onto three central anchors.
        /// </summary>
        public static FighterStageGeometry Berlin { get; } = new(
            stageID: "berlin_wall",
            leftWall: FP64.FromInt(-10),
            rightWall: FP64.FromInt(10),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromDouble(-6.5), FP64.FromDouble(3.6), FP64.FromDouble(1.3)),
                new FighterStagePlatform(FP64.FromDouble(6.5), FP64.FromDouble(3.6), FP64.FromDouble(1.3))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-4), FP64.Zero, FP64.FromInt(4) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromDouble(-6.5), FP64.FromDouble(4.1)),
                new FPVector2(FP64.FromDouble(6.5), FP64.FromDouble(4.1)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5))
            });

        /// <summary>
        /// Globe Theatre: a flat wooden stage with two tiered gallery balconies and
        /// a high central canopy. Hecklers pelt from the three gallery anchors.
        ///
        /// <para>The tiring-house balcony sat at 4.4 until the 2026-08-10 feel
        /// batch cut jump force 20% (§2.2): the post-retune double-jump ceiling is
        /// ≈4.21, so 4.4 became the one authored platform nobody could reach. It
        /// is 4.0 now — the same height as the Orléans centre platform, which
        /// keeps ≈0.2 units of clearance.</para>
        /// </summary>
        public static FighterStageGeometry Globe { get; } = new(
            stageID: "globe_theatre",
            leftWall: FP64.FromInt(-9),
            rightWall: FP64.FromInt(9),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromInt(-5), FP64.FromDouble(2.4), FP64.FromDouble(1.4)),
                new FighterStagePlatform(FP64.FromInt(5), FP64.FromDouble(2.4), FP64.FromDouble(1.4)),
                new FighterStagePlatform(FP64.Zero, FP64.FromDouble(4.0), FP64.FromDouble(1.2))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-5), FP64.Zero, FP64.FromInt(5) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromInt(-5), FP64.FromDouble(2.9)),
                new FPVector2(FP64.FromInt(5), FP64.FromDouble(2.9)),
                new FPVector2(FP64.Zero, FP64.FromDouble(4.5))
            });

        /// <summary>
        /// Gettysburg Ridge: a wide dirt path with two low broken rail fences. The
        /// artillery sight line is the widest hazard band in the catalog.
        /// </summary>
        public static FighterStageGeometry Gettysburg { get; } = new(
            stageID: "gettysburg_ridge",
            leftWall: FP64.FromInt(-10),
            rightWall: FP64.FromInt(10),
            ceiling: FP64.FromInt(9),
            bottomBlastZone: FP64.FromInt(-5),
            spawnDistance: 4,
            platforms: new[] {
                new FighterStagePlatform(FP64.FromDouble(-4.5), FP64.FromDouble(1.8), FP64.FromDouble(1.2)),
                new FighterStagePlatform(FP64.FromDouble(4.5), FP64.FromDouble(1.8), FP64.FromDouble(1.2))
            },
            hazardAnchorXs: new[] { FP64.FromInt(-5), FP64.Zero, FP64.FromInt(5) },
            orbAnchors: new[] {
                new FPVector2(FP64.FromDouble(-4.5), FP64.FromDouble(2.3)),
                new FPVector2(FP64.FromDouble(4.5), FP64.FromDouble(2.3)),
                new FPVector2(FP64.Zero, FP64.FromDouble(0.5))
            });

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
    }
}
