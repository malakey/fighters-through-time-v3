using System.Collections.Generic;

namespace FTT.FighterSim {
    /// <summary>Measured fight-plane registration of the approved A reference plates.
    /// Reference pixels are converted once into the existing 62.5 px/unit simulation space.
    /// Perspective floor artwork is sampled on the fighters' feet row.</summary>
    public sealed class FighterStageVisualProfile {
        public const double ReferenceWidth = 1672;
        public const double ReferenceHeight = 941;
        public const double PlateWorldWidth = 960;
        public const double PixelsPerUnit = 62.5;
        public const double PlateScale = PlateWorldWidth / ReferenceWidth;
        public const float ReferenceZoom = 2f;
        public const float FighterHeightFraction = 0.33f;
        public readonly string StageID;
        public readonly double FloorRow;
        public readonly int SpawnDistance;
        public readonly (double Left, double Right, double Top)[] PlatformSpans;
        public readonly (double Left, double Right)[] FloorSpans;
        public string PlatePath => $"res://assets/environments/mockup_a/fighter/{StageID}.png";
        public string BackdropPath => $"res://assets/environments/mockup_a/fighter_backdrops/{StageID}.png";
        public string ForegroundPath => $"res://assets/environments/mockup_a/fighter_foregrounds/{StageID}.png";
        public double FrameCenterY => 700 + (ReferenceHeight / 2 - FloorRow) * PlateScale;
        // Quantize endpoints before constructing centre/half-width pairs. The binary
        // grid is far below a visible pixel and makes wall-touching floor ends exact.
        private static double Quantize(double units) => System.Math.Round(units * 1048576) / 1048576;
        public double WorldX(double referenceX) => Quantize((referenceX - ReferenceWidth / 2) * PlateScale / PixelsPerUnit);
        public double WorldY(double referenceY) => Quantize((FloorRow - referenceY) * PlateScale / PixelsPerUnit);

        private FighterStageVisualProfile(string stageID, double floorRow, int spawnDistance,
            (double, double, double)[] platforms, (double, double)[] floors) {
            StageID = stageID;
            FloorRow = floorRow;
            SpawnDistance = spawnDistance;
            PlatformSpans = platforms;
            FloorSpans = floors;
        }

        public static IReadOnlyList<FighterStageVisualProfile> All { get; } = new FighterStageVisualProfile[] {
            new("florence_workshop", 828, 6, new (double, double, double)[] { (225, 632, 385), (1034, 1440, 385) }, new (double, double)[] {  }),
            new("orleans_vanguard", 805, 5, new (double, double, double)[] { (130, 548, 330), (1125, 1555, 330), (580, 1085, 150) }, new (double, double)[] {  }),
            new("chicago_exposition", 730, 5, new (double, double, double)[] { (14, 515, 300), (1157, 1658, 300) }, new (double, double)[] {  }),
            new("paris_bastille", 770, 5, new (double, double, double)[] { (304, 757, 350), (954, 1476, 350) }, new (double, double)[] { (0, 636), (1036, 1672) }),
            new("vesuvius_caldera", 775, 4, new (double, double, double)[] { (30, 470, 410), (1100, 1590, 280) }, new (double, double)[] { (0, 1430) }),
            new("nassau_flagship", 760, 4, new (double, double, double)[] { (205, 680, 300), (1004, 1475, 300) }, new (double, double)[] { (0, 1370) }),
            new("alexandria_chambers", 760, 6, new (double, double, double)[] { (337, 699, 545), (963, 1332, 545) }, new (double, double)[] {  }),
            new("berlin_wall", 680, 4, new (double, double, double)[] { (164, 563, 370), (1012, 1403, 370) }, new (double, double)[] { (0, 1430) }),
            new("globe_theatre", 825, 4, new (double, double, double)[] { (45, 477, 440), (1202, 1631, 440), (619, 1052, 330) }, new (double, double)[] { (0, 636), (1036, 1672) }),
            new("gettysburg_ridge", 785, 4, new (double, double, double)[] { (12, 542, 375), (1142, 1660, 375) }, new (double, double)[] {  }),
        };

        public static FighterStageVisualProfile Find(string stageID) {
            foreach (FighterStageVisualProfile profile in All) {
                if (profile.StageID == stageID) return profile;
            }
            return null;
        }
    }
}
