using Godot;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 8 - Cleopatra's Palace, Alexandria 30 BC. Four rooms across 10,240 px
    /// split into the two zones the design calls for: a sun-bleached SURFACE where
    /// Octavian's cult-armed legion is pressing the palace, and the TOMB NETWORK
    /// below it, reached by a vertical descent down a collapsed burial shaft.
    ///
    /// Era identity is mechanical in both zones:
    /// <list type="bullet">
    /// <item>Surface: five <see cref="MovementDampenerZone"/> drifts of deep sand at
    ///       the design's ~50% slow. They are deliberately non-overlapping -
    ///       <see cref="EnvironmentPlayerModifiers"/> publishes the PRODUCT of every
    ///       live source, so two drifts over one player would compound to a crawl.</item>
    /// <item>Tombs: a four-glyph <see cref="SequenceLock"/> seals the vault route into
    ///       Cleopatra's chambers. The glyphs are mounted on the wall in an order that
    ///       is NOT the answer; the answer is carved on a relief in a side alcove
    ///       further west, so the lock is read rather than brute-forced.</item>
    /// </list>
    ///
    /// Routing the legion away from the inner chambers is the through-line: the tomb
    /// route is the way past the siege line, and the vault seal is what keeps the
    /// Romans out of it.
    ///
    /// Locked encounter economy (docs/DUST_ECONOMY.md, DustEconomyTests row 8):
    /// 10 standards / 0 elites / 1 boss / 3 extractors. The authored tables below are
    /// the single source of those counts and Level08ContentTests asserts them.
    ///
    /// This level owes a FOURTH dialogue beat: <c>level_08.postboss</c> is an authored
    /// campaign scene (design-godot.md 3368-3374) that runs between the boss dying and
    /// the ordinary exit beat. It is declared through the base class's
    /// <see cref="StoryLevelControllerBase.PostBossDialogueIDs"/> hook, which chains
    /// defeat -> postboss -> exit -> results without taking the defeat bookkeeping
    /// away from the base.
    /// </summary>
    public partial class Level08Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_08_egypt";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/jackal_priest.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_08_dialogue.tres";

        /// <summary>Stable, save-persisted puzzle id for the hieroglyph vault seal.</summary>
        public const string HieroglyphPuzzleID = "level_08.hieroglyph_lock";

        public override string LevelID => ID;
        public override CampaignLevel Level => CampaignLevel.Egypt;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 360f;
        public override string LevelTitleKey => "egypt_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(200, 650);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        /// <summary>The authored Cleopatra scene; see design-godot.md 3368-3374.</summary>
        public string PostBossDialogueID => $"{DialoguePrefix}.postboss";

        protected override string InitialObjectiveKey => "egypt_objective_dunes";
        protected override string BossObjectiveKey => "egypt_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "egypt_objective_complete";

        // === Layout ===

        public const float LevelWidth = 10240f;
        /// <summary>Taller than Florence's 1080: the surface and the tombs are stacked.</summary>
        public const float LevelHeight = 1800f;

        /// <summary>Desert floor surface (rooms 1-2).</summary>
        public const float SurfaceGroundY = 700f;
        /// <summary>Tomb corridor floor (rooms 3-4).</summary>
        public const float TombFloorY = 1620f;

        private const float SurfaceEnemyY = SurfaceGroundY - 50f;
        private const float TombEnemyY = TombFloorY - 50f;

        /// <summary>East edge of the surface: past this the burial shaft opens up.</summary>
        public const float SurfaceEndX = 5700f;
        /// <summary>East edge of the open shaft; the tomb ceiling starts here.</summary>
        private const float ShaftEndX = 6450f;
        /// <summary>Underside of the tomb corridor ceiling slab.</summary>
        private const float TombCeilingY = 1000f;
        /// <summary>Top of the boss chamber ceiling slab.</summary>
        private const float ChamberCeilingY = 840f;
        /// <summary>Underside of that slab - where the chamber's open air begins.</summary>
        private const float ChamberHeadroomY = ChamberCeilingY + DefaultFloorThickness;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 3200f;
        private const float Room3StartX = 5760f;
        public const float Room4StartX = 8320f;

        /// <summary>The hieroglyph-sealed vault door; the only route into the chambers.</summary>
        public const float VaultGateX = Room4StartX;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 3200, 1080);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, 1080);
        // The descent room spans both zones, so it gets the full level height.
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX, 0, 2560, LevelHeight);
        // Underground: anchor the 1080 window to the lower band of the level.
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX, 720, 1920, 1080);

        /// <summary>Boss court width; the Jackal Priest's ranged band has to fit inside it.</summary>
        public const float BossArenaWidth = LevelWidth - Room4StartX;

        // === Era identity constants ===

        public const float DeepSandMultiplier = 0.5f;
        public const int DeepSandZoneCount = 5;
        public const int GlyphCount = 4;

        /// <summary>
        /// Wall order (west to east) paired with the activation order the alcove
        /// relief carves. They deliberately disagree: pressing the wall left to right
        /// fails, which is what makes the relief worth finding.
        /// </summary>
        public static readonly (string NodeName, string LabelKey, int OrderIndex)[] GlyphPlan = {
            ("GlyphFalcon", "egypt_glyph_falcon", 2),
            ("GlyphScarab", "egypt_glyph_scarab", 0),
            ("GlyphJackal", "egypt_glyph_jackal", 3),
            ("GlyphIbis", "egypt_glyph_ibis", 1)
        };

        // === Authored encounter tables (the locked economy row lives here) ===

        /// <summary>
        /// Era roster: `plasma_spear_ward` (Egypt standard, plasma javelins) mixed with
        /// `chrono_slasher` (Unbound shock-trooper standard, melee). No elites - the locked
        /// row for level 8 is E = 0.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] DuneSpawns = {
            ("plasma_spear_ward", new Vector2(1300, SurfaceEnemyY), new Vector2(1150, SurfaceEnemyY), new Vector2(1450, SurfaceEnemyY)),
            ("chrono_slasher", new Vector2(2100, SurfaceEnemyY), new Vector2(1950, SurfaceEnemyY), new Vector2(2250, SurfaceEnemyY)),
            ("plasma_spear_ward", new Vector2(2900, SurfaceEnemyY), null, null)
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] SiegeLineSpawns = {
            ("chrono_slasher", new Vector2(3800, SurfaceEnemyY), new Vector2(3650, SurfaceEnemyY), new Vector2(3950, SurfaceEnemyY)),
            ("plasma_spear_ward", new Vector2(4400, SurfaceEnemyY), null, null),
            ("chrono_slasher", new Vector2(5200, SurfaceEnemyY), new Vector2(5050, SurfaceEnemyY), new Vector2(5350, SurfaceEnemyY))
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] TombSpawns = {
            ("chrono_slasher", new Vector2(6900, TombEnemyY), new Vector2(6750, TombEnemyY), new Vector2(7050, TombEnemyY)),
            ("plasma_spear_ward", new Vector2(7320, TombEnemyY), null, null)
        };

        /// <summary>Ward detail standing over the seal itself: the lock is solved under fire.</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] VaultSpawns = {
            ("plasma_spear_ward", new Vector2(7920, TombEnemyY), null, null),
            ("chrono_slasher", new Vector2(8150, TombEnemyY), new Vector2(8060, TombEnemyY), new Vector2(8240, TombEnemyY))
        };

        /// <summary>15 dust each and resource-owned; never overridden from level code.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_08.extractor_dune_ridge", new Vector2(2600, 460)),
            ("level_08.extractor_tomb_alcove", new Vector2(6700, 1240)),
            ("level_08.extractor_vault_ledge", new Vector2(7600, 1200))
        };

        public static int AuthoredStandardCount =>
            DuneSpawns.Length + SiegeLineSpawns.Length + TombSpawns.Length + VaultSpawns.Length;

        public static int AuthoredEliteCount => 0;

        public static int AuthoredExtractorCount => ExtractorPlacements.Length;

        // === Runtime state ===

        private readonly List<MovementDampenerZone> _deepSand = new();
        private PuzzleManager _hieroglyphPuzzle;
        private SequenceLock _hieroglyphLock;
        private StaticBody2D _vaultDoor;
        private bool _siegeLineWaveSpawned;
        private bool _tombWaveSpawned;
        private bool _vaultWaveSpawned;
        private bool _descended;

        public IReadOnlyList<MovementDampenerZone> DeepSandZones => _deepSand;
        public PuzzleManager HieroglyphPuzzle => _hieroglyphPuzzle;
        public SequenceLock HieroglyphLock => _hieroglyphLock;
        public bool VaultDoorOpen => _vaultDoor == null || !IsInstanceValid(_vaultDoor);

        /// <summary>True while the authored Cleopatra scene is the beat on screen.</summary>
        public bool PostBossBeatActive => ActivePostBossDialogueID == PostBossDialogueID;

        // === Palette: bleached limestone, tomb shadow, Archive cyan ===

        protected override Color FloorColor => new(0.30f, 0.25f, 0.16f);
        protected override Color FloorEdgeColor => new(0.72f, 0.60f, 0.34f);
        protected override Color PlatformColor => new(0.40f, 0.34f, 0.22f);
        protected override Color WallColor => new(0.14f, 0.12f, 0.09f);
        protected override Color HazardColor => new(0.66f, 0.34f, 0.12f);

        private static readonly Color TombFloorColor = new(0.18f, 0.16f, 0.14f);
        private static readonly Color TombPlatformColor = new(0.26f, 0.23f, 0.19f);
        private static readonly Color VaultDoorColor = new(0.28f, 0.36f, 0.42f);
        private static readonly Color GlyphTextColor = new(1f, 0.82f, 0.36f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1ShiftingDunes();
            BuildRoom2SiegeLine();
            BuildRoom3TombNetwork();
            BuildRoom4InnerChamber();
            BuildExtractors(ExtractorPlacements);
        }

        /// <summary>Room 1: the open desert approach, buried under drifting sand.</summary>
        private void BuildRoom1ShiftingDunes() {
            BuildRoomBackground("BG_Room1", Room1StartX, 3200, LevelHeight, new Color(0.20f, 0.15f, 0.07f, 0.35f));
            BuildFloor(Room1StartX, SurfaceGroundY, 3200);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(600, 560, 240);
            BuildPlatform(1100, 430, 200);
            BuildPlatform(1700, 570, 260);
            BuildPlatform(2200, 450, 220);
            // Wind-cut ridge holding the first extractor.
            BuildPlatform(2600, 520, 240);
            BuildPlatform(3000, 600, 220);

            BuildCheckpoint(200, SurfaceEnemyY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "egypt_room_dunes", new Color(0.95f, 0.82f, 0.42f));
            BuildRoomTransition("egypt_room_dunes", new Vector2(300, 500), Room1CameraBounds,
                new Vector2(80, 1080));
        }

        /// <summary>
        /// Room 2: Octavian's siege line. The surface simply ends here - the ground
        /// gives way into a collapsed burial shaft, which is the route the design
        /// wants (down and around, away from the inner chambers).
        /// </summary>
        private void BuildRoom2SiegeLine() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.18f, 0.13f, 0.06f, 0.4f));
            BuildFloor(Room2StartX, SurfaceGroundY, SurfaceEndX - Room2StartX);

            BuildPlatform(3500, 560, 240);
            BuildPlatform(3950, 430, 200);
            BuildPlatform(4500, 570, 260);
            BuildPlatform(5100, 480, 220);
            BuildPlatform(5450, 600, 200);

            // Scorched plasma scars where the legion's new weapons have been tested.
            BuildHazardSpikes(3300, 690, 140);
            BuildHazardSpikes(5000, 690, 120);

            BuildRoomDecoration(Room2StartX, "egypt_room_siege", new Color(0.95f, 0.55f, 0.30f));
            BuildRoomTransition("egypt_room_siege", new Vector2(Room2StartX, 500), Room2CameraBounds,
                new Vector2(80, 1080),
                onEntered: _ => RefreshObjective());
            BuildWaveTrigger("SiegeLineWaveTrigger", new Vector2(3400, 600), TriggerSiegeLineWave,
                new Vector2(60, 900));
        }

        /// <summary>
        /// Room 3: the vertical descent and the tomb network. The shaft drops ~920 px
        /// on drop-through ledges; the corridor below runs east to the sealed vault.
        /// </summary>
        private void BuildRoom3TombNetwork() {
            BuildRoomBackground("BG_Room3", Room3StartX, 2560, LevelHeight, new Color(0.07f, 0.06f, 0.07f, 0.5f));

            // The descent: drop-through ledges zig-zagging down the burial shaft.
            BuildOneWayPlatform(5860, 900, 220);
            BuildOneWayPlatform(6160, 1080, 220);
            BuildOneWayPlatform(5900, 1260, 220);
            BuildOneWayPlatform(6220, 1440, 220);

            // Tomb corridor floor: runs unbroken beneath the shaft and the vault.
            BuildFloor(SurfaceEndX, TombFloorY, LevelWidth - SurfaceEndX, TombFloorColor);
            // Corridor ceiling. Starts east of the shaft so the descent stays open.
            BuildFloor(ShaftEndX, TombCeilingY, VaultGateX - ShaftEndX, TombFloorColor);

            // Side alcove: the carved relief that spells the glyph order, plus the
            // second extractor. Paying the detour twice is what makes it worth taking.
            BuildPlatform(6420, 1460, 200, TombPlatformColor);
            BuildPlatform(6700, 1300, 260, TombPlatformColor);
            BuildCarvedRelief(new Vector2(6700, 1180));

            // Vault ledge over the corridor holding the third extractor.
            BuildPlatform(7180, 1420, 200, TombPlatformColor);
            BuildPlatform(7600, 1260, 240, TombPlatformColor);

            BuildCheckpoint(6520, TombEnemyY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room3StartX, "egypt_room_tombs", new Color(0.55f, 0.80f, 0.90f));
            BuildRoomTransition("egypt_room_tombs", new Vector2(Room3StartX, 900), Room3CameraBounds,
                new Vector2(80, LevelHeight));
            BuildWaveTrigger("TombWaveTrigger", new Vector2(6600, 1450), TriggerTombWave,
                new Vector2(60, 400));
            BuildWaveTrigger("VaultWaveTrigger", new Vector2(7700, 1450), TriggerVaultWave,
                new Vector2(60, 400));

            // The seal itself. It reaches the corridor ceiling, so the vault cannot be
            // jumped: reading the relief is the only way through.
            _vaultDoor = BuildDoor("VaultDoor", new Vector2(VaultGateX, TombFloorY), "egypt_vault_sealed",
                new Vector2(48, TombFloorY - TombCeilingY), VaultDoorColor);
        }

        /// <summary>
        /// Room 4: Cleopatra's inner chamber, a royal vault cut below the palace.
        /// Sandy floor plus two stone sarcophagi as raised platforms, kept to the
        /// flanks so the Jackal Priest's teleport has open anchor space mid-floor.
        /// </summary>
        private void BuildRoom4InnerChamber() {
            BuildRoomBackground("BG_Room4_Chamber", Room4StartX, BossArenaWidth, LevelHeight,
                new Color(0.16f, 0.09f, 0.14f, 0.5f));
            // Chamber ceiling: higher than the corridor's, so the fight has headroom.
            BuildFloor(Room4StartX, ChamberCeilingY, BossArenaWidth, TombFloorColor);
            // Seals the pocket above the vault door where the two ceilings step.
            BuildWall(Room4StartX, ChamberHeadroomY, TombCeilingY - ChamberHeadroomY);
            BuildWall(LevelWidth - 20f, 0, LevelHeight);

            // Sarcophagi. Flanked deliberately: the centre floor stays clear.
            BuildPlatform(8880, 1420, 260, TombPlatformColor);
            BuildPlatform(9700, 1420, 260, TombPlatformColor);

            BuildCheckpoint(8420, TombEnemyY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "egypt_room_chamber", new Color(0.95f, 0.35f, 0.45f));
            BuildRoomTransition("egypt_room_chamber", new Vector2(Room4StartX + 40, 1200), Room4CameraBounds,
                new Vector2(80, 1080));

            BuildBossEncounter(BossResourcePath, new Vector2(9800, TombEnemyY),
                "jackal_priest_encounter", revealDistance: 900f);
        }

        /// <summary>
        /// The discoverable half of the hieroglyph puzzle: a wall relief in the tomb
        /// alcove listing the activation order. Placeholder signage; Package 8
        /// replaces it with an actual carved surface.
        /// </summary>
        private void BuildCarvedRelief(Vector2 position) {
            var label = new Label {
                Name = "CarvedOrderRelief",
                Text = Tr("egypt_relief_order"),
                Position = position + new Vector2(-240, -40),
                CustomMinimumSize = new Vector2(480, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", GlyphTextColor);
            AddChild(label);
        }

        // === Hieroglyph vault seal ===

        protected override void OnLevelReady() {
            CollectDeepSand();
            WireHieroglyphLock();
            RefreshObjective();
        }

        /// <summary>
        /// Picks up the scene-authored sand drifts. Their signage is authored, not
        /// patched: each instance sets <c>LabelKey = "egypt_deep_sand"</c> on the
        /// template root, which the component resolves through
        /// <see cref="ToolkitLabel"/>. (Before the Wave B integration the template
        /// shipped a hardcoded English label and this method rewrote it.)
        /// </summary>
        private void CollectDeepSand() {
            _deepSand.Clear();
            Godot.Collections.Array<Node> children = GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is MovementDampenerZone zone) _deepSand.Add(zone);
            }
        }

        private void WireHieroglyphLock() {
            _hieroglyphPuzzle = GetNodeOrNull<PuzzleManager>("HieroglyphPuzzle");
            _hieroglyphLock = GetNodeOrNull<SequenceLock>("HieroglyphLock");
            if (_hieroglyphPuzzle == null || _hieroglyphLock == null) {
                GD.PushError("Level 8: the authored 'HieroglyphPuzzle'/'HieroglyphLock' nodes are missing.");
                return;
            }

            LabelGlyphs();
            _hieroglyphPuzzle.PuzzleCompleted += OnHieroglyphPuzzleCompleted;
            _hieroglyphLock.SequenceAdvanced += _ => RefreshObjective();
            _hieroglyphLock.SequenceReset += RefreshObjective;

            // Two ways the vault must already be open: the save carries the completion
            // (PersistCompletionToSave), or the run resumed at the pre-boss checkpoint,
            // which stands PAST the door. Either way a sealed door walls the player in.
            if (_hieroglyphPuzzle.IsCompleted || ResumedCheckpointID == Checkpoint2) OpenVault();
        }

        /// <summary>Names each glyph on the wall so the alcove relief can be matched to it.</summary>
        private void LabelGlyphs() {
            foreach ((string nodeName, string labelKey, int _) in GlyphPlan) {
                if (_hieroglyphLock.GetNodeOrNull<SequenceGlyph>(nodeName) is not SequenceGlyph glyph) continue;
                if (glyph.HasNode("GlyphName")) continue;
                var label = new Label {
                    Name = "GlyphName",
                    Text = Tr(labelKey),
                    Position = new Vector2(-70, 68),
                    CustomMinimumSize = new Vector2(140, 18),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                label.AddThemeFontSizeOverride("font_size", 11);
                label.AddThemeColorOverride("font_color", GlyphTextColor);
                glyph.AddChild(label);
            }
        }

        /// <summary>
        /// Idempotent by construction: <see cref="StoryLevelControllerBase.OpenDoor"/>
        /// nulls the reference. Required - <see cref="PuzzleManager"/> re-emits
        /// <c>PuzzleCompleted</c> one deferred frame after _Ready for a completion it
        /// restored from the save, so a resumed run runs this handler twice.
        /// </summary>
        private void OnHieroglyphPuzzleCompleted(string puzzleID) {
            OpenVault();
            RefreshObjective();
        }

        private void OpenVault() => OpenDoor(ref _vaultDoor);

        /// <summary>
        /// Test/diagnostic entry point: presses the glyphs in the carved order,
        /// exactly as four player interactions would.
        /// </summary>
        public bool SolveHieroglyphLockForTest() {
            if (_hieroglyphLock == null) return false;
            for (int order = 0; order < _hieroglyphLock.GlyphCount; order++) {
                SequenceGlyph glyph = GlyphForOrder(order);
                if (glyph == null || !_hieroglyphLock.Activate(glyph)) return false;
            }
            return _hieroglyphLock.IsCompleted;
        }

        /// <summary>The glyph whose authored <c>OrderIndex</c> is <paramref name="order"/>.</summary>
        public SequenceGlyph GlyphForOrder(int order) {
            if (_hieroglyphLock == null) return null;
            foreach (SequenceGlyph glyph in _hieroglyphLock.Glyphs) {
                if (glyph.OrderIndex == order) return glyph;
            }
            return null;
        }

        // === Encounters ===

        protected override void SpawnInitialEnemies() => SpawnWave(DuneSpawns);

        private void TriggerSiegeLineWave() {
            if (_siegeLineWaveSpawned) return;
            _siegeLineWaveSpawned = true;
            SpawnWave(SiegeLineSpawns);
        }

        private void TriggerTombWave() {
            if (_tombWaveSpawned) return;
            _tombWaveSpawned = true;
            _descended = true;
            SpawnWave(TombSpawns);
            RefreshObjective();
        }

        private void TriggerVaultWave() {
            if (_vaultWaveSpawned) return;
            _vaultWaveSpawned = true;
            SpawnWave(VaultSpawns);
        }

        /// <summary>
        /// Authored counts are the Normal-difficulty truth; ScaleEncounterCount is the
        /// only thing allowed to move them, and it never exceeds the authored table.
        /// Concurrency stays at 3 or fewer live standards, well inside the level_08
        /// pool config's warm counts (standard_enemy 12, enemy_projectile 20).
        /// </summary>
        private void SpawnWave((string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] wave) {
            if (wave == null) return;
            int count = StoryDifficultyTuning.ScaleEncounterCount(
                wave.Length, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < wave.Length; index++) {
                (string enemyID, Vector2 position, Vector2? patrolA, Vector2? patrolB) = wave[index];
                EnemyFactory.Spawn(enemyID, this, position, patrolA, patrolB);
            }
        }

        // === Resume ===

        protected override void MarkWavesClearedThrough(string checkpointID) {
            switch (checkpointID) {
                case Checkpoint1:
                    _siegeLineWaveSpawned = true;
                    _descended = true;
                    break;
                case Checkpoint2:
                    _siegeLineWaveSpawned = true;
                    _tombWaveSpawned = true;
                    _vaultWaveSpawned = true;
                    _descended = true;
                    break;
            }
        }

        // === Boss beat: defeat -> authored Cleopatra scene -> exit -> results ===

        /// <summary>
        /// The authored Cleopatra scene (design-godot.md 3368-3374) runs between the
        /// Jackal Priest dying and the ordinary exit beat. Declaring it here is the
        /// whole of it: the base plays the beat after its usual defeat delay, waits
        /// for it, then starts the exit sequence — and keeps ownership of
        /// <c>IsBossDefeated</c>, the completion objective, and the dust tally.
        /// (Before the Wave B integration this level overrode <c>OnBossDefeated</c>
        /// wholesale and lost all three.)
        /// </summary>
        protected override IReadOnlyList<string> PostBossDialogueIDs =>
            _postBossBeats ??= new[] { PostBossDialogueID };

        private string[] _postBossBeats;

        // === Objectives ===

        private void RefreshObjective() {
            if (IsBossDefeated) {
                SetObjective(CompletionObjectiveKey);
            } else if (VaultDoorOpen && _descended) {
                SetObjective("egypt_objective_chamber");
            } else if (_descended && _hieroglyphLock != null && _hieroglyphLock.Progress > 0) {
                SetObjective("egypt_objective_seal", _hieroglyphLock.Progress, _hieroglyphLock.GlyphCount);
            } else if (_descended) {
                SetObjective("egypt_objective_tombs");
            } else {
                SetObjective(InitialObjectiveKey);
            }
        }
    }
}
