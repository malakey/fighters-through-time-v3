using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 9 - Berlin, 1961 (The Division of Berlin). The campaign's designated
    /// stealth level. Four rooms across 10,560 px:
    /// <list type="number">
    /// <item><b>Checkpoint Charlie</b> (0-2880) - snowed-in ruined urban blocks;
    ///       the opening firefight, no beams yet.</item>
    /// <item><b>The Death Strip</b> (2880-6400) - the era beat. Three
    ///       <see cref="SearchlightZone"/> beams in
    ///       <see cref="SearchlightMode.DelayedStrike"/> mode with deliberately
    ///       desynced sweep periods, three rubble cover pockets sitting in the gaps
    ///       between their swept footprints, a guard-tower climb on drop-through
    ///       platforms, and the three surveillance feed relays that cut them.</item>
    /// <item><b>Radar Yard</b> (6400-8640) - past the sealed radar gate; the cloaked
    ///       mast climb and the heaviest wave, led by the level's single elite.</item>
    /// <item><b>Bunker Street</b> (8640-10560) - the Iron Chancellor's bunker defense
    ///       battle: a flat snowy street, two waist-high fortifications, and two high
    ///       guard-tower balconies.</item>
    /// </list>
    ///
    /// <para><b>How the stealth reads, and how it differs from Paris.</b> Level 4 runs
    /// the same component in <see cref="SearchlightMode.UltimateDrain"/>: standing in a
    /// Paris beam is an attrition tax on the Ultimate meter that a player can simply
    /// eat. Berlin's beams never touch the meter. Exposure accumulates silently for
    /// <see cref="SearchlightZone.ExposureGraceSeconds"/> (1.5 s, the figure the design
    /// quantifies) and then an automated drone laser lands for
    /// <see cref="StrikeDamage"/> with knockback, and keeps landing every 1.5 s while
    /// the player stays lit. Crossing is therefore a timing problem with a real fail
    /// state rather than a resource cost, and the answer is either the sweep window or
    /// one of the three un-swept cover pockets.</para>
    ///
    /// <para><b>Agency over patience.</b> Each beam is fed by a
    /// <see cref="DestructibleBlock"/> surveillance relay placed past that beam. Cutting
    /// a relay permanently darkens its light, so the gauntlet gets progressively safer
    /// as the player works forward instead of demanding the same patience three times.
    /// All three relays satisfy the <see cref="PuzzleManager"/> that unseals the radar
    /// gate into room three.</para>
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: exactly 12 standards,
    /// 1 elite, 1 boss, 3 extractors.
    /// </summary>
    public partial class Level09Controller : StoryLevelControllerBase {

        // === Layout ===

        public const float LevelWidth = 10560f;
        public const float LevelHeight = 1400f;
        public const float GroundY = 1200f;

        /// <summary>
        /// Ground rooms confine to a bottom-anchored 1080-tall window. The level is
        /// 1400 px tall so the guard towers have real headroom; only the two rooms
        /// that actually contain a climb use the full height.
        /// </summary>
        public const float GroundRoomCameraTop = LevelHeight - 1080f;

        public const float Room1StartX = 0f;
        public const float Room2StartX = 2880f;
        public const float Room3StartX = 6400f;
        public const float Room4StartX = 8640f;

        /// <summary>Depth of the authored searchlight cone, from SearchlightZoneTemplate.tscn.</summary>
        public const float SearchlightConeDepth = 520f;

        /// <summary>Half-width of the authored searchlight cone at full depth.</summary>
        public const float SearchlightConeHalfWidth = 120f;

        private const string FeedPuzzleID = "level_09.cut_feeds";
        private const string BossResourcePath = "res://resources/Bosses/iron_chancellor.tres";

        // === Authored encounter table (asserted by Level09ContentTests) ===

        /// <summary>The Berlin era standard: a long-telegraph, long-range sniper.</summary>
        public const string SentryEnemyID = "infrared_border_sentry";

        /// <summary>The Unbound shock-trooper standard mixed through every era roster.</summary>
        public const string CultistEnemyID = "chrono_slasher";

        /// <summary>The Berlin elite. Exactly one, per the locked economy row.</summary>
        public const string EliteEnemyID = "neural_mech_walker";

        /// <summary>
        /// Authored Normal-difficulty spawn table. Wave 1 is live on entry; waves 2
        /// and 3 arm on their room's proximity trigger.
        /// <see cref="StoryDifficultyTuning.ScaleEncounterCount"/> takes a prefix of
        /// each wave, so the hardest concurrency is wave 3's six bodies - inside
        /// level_09_pool_config's standard warm 14 / elite warm 4 caps even with
        /// every earlier wave still alive.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - Checkpoint Charlie. A sniper on the rubble, Unbound troopers on the street.
            (CultistEnemyID, 1, new Vector2(1000f, 1150f)),
            (SentryEnemyID, 1, new Vector2(1700f, 990f)),
            (CultistEnemyID, 1, new Vector2(2450f, 1150f)),
            // Wave 2 - The Death Strip. Sentries hold the beams; one owns the walkway.
            (SentryEnemyID, 2, new Vector2(3400f, 1150f)),
            (CultistEnemyID, 2, new Vector2(4100f, 1150f)),
            (SentryEnemyID, 2, new Vector2(5000f, 1150f)),
            (SentryEnemyID, 2, new Vector2(5900f, 460f)),
            // Wave 3 - Radar Yard. The elite walker anchors the yard.
            (SentryEnemyID, 3, new Vector2(6800f, 1150f)),
            (CultistEnemyID, 3, new Vector2(7100f, 1150f)),
            (SentryEnemyID, 3, new Vector2(7350f, 570f)),
            (EliteEnemyID, 3, new Vector2(7600f, 1150f)),
            (CultistEnemyID, 3, new Vector2(7900f, 1150f)),
            (SentryEnemyID, 3, new Vector2(8200f, 1150f))
        };

        /// <summary>
        /// The three authored cover slabs in the Death Strip, as (centre X, width).
        /// Each one is deliberately authored in a gap between two searchlight
        /// footprints, so it is genuinely safe ground; the content test proves it
        /// against <see cref="SweptFootprint"/> rather than trusting these numbers.
        /// </summary>
        public static readonly (float CenterX, float Width)[] CoverPockets = {
            (3010f, 240f),   // west of the first beam
            (4120f, 320f),   // the pocket between the two ground beams
            (5200f, 260f)    // east of the second beam, under checkpoint 1
        };

        /// <summary>Three extractors, each guarding a climb the critical path can skip.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_09.extractor_ruins", new Vector2(2300f, 940f)),
            ("level_09.extractor_watchtower", new Vector2(5620f, 480f)),
            ("level_09.extractor_radar_mast", new Vector2(7130f, 580f))
        };

        public static int StandardEnemyCount => CountOf(SentryEnemyID) + CountOf(CultistEnemyID);
        public static int EliteEnemyCount => CountOf(EliteEnemyID);

        private static int CountOf(string enemyID) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) {
                if (id == enemyID) total++;
            }
            return total;
        }

        // === Base contract ===

        public override string LevelID => "level_09_berlin";
        public override CampaignLevel Level => CampaignLevel.Berlin;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 360f;
        public override string LevelTitleKey => "berlin_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_09_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240f, 1150f);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "berlin_objective_infiltrate";
        protected override string BossObjectiveKey => "berlin_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "berlin_objective_complete";

        // === Snow-tinted graybox palette ===

        protected override Color FloorColor => new(0.72f, 0.75f, 0.80f);
        protected override Color FloorEdgeColor => new(0.91f, 0.94f, 0.98f);
        protected override Color PlatformColor => new(0.50f, 0.54f, 0.60f);
        protected override Color WallColor => new(0.23f, 0.25f, 0.30f);
        protected override Color HazardColor => new(0.34f, 0.54f, 0.72f);

        private static readonly Color SnowRubbleColor = new(0.62f, 0.66f, 0.72f);
        private static readonly Color CoverPocketColor = new(0.40f, 0.46f, 0.54f);
        private static readonly Color DarkenedBeamModulate = new(0.25f, 0.27f, 0.32f, 0.35f);

        // === Scene-authored toolkit instances ===

        /// <summary>One surveillance relay and the beam it powers.</summary>
        public readonly record struct FeedLink(string FeedNodeName, string LightNodeName, string ConditionID);

        /// <summary>
        /// Relay-to-beam wiring. Each relay is authored past the beam it cuts, so the
        /// player crosses a beam once and can then delete it for good.
        /// </summary>
        public static readonly FeedLink[] FeedLinks = {
            new("FeedRelayWest", "SearchlightStripWest", "feed_west_cut"),
            new("FeedRelayEast", "SearchlightStripEast", "feed_east_cut"),
            new("FeedRelayTower", "SearchlightWatchtower", "feed_tower_cut")
        };

        private readonly List<SearchlightZone> _searchlights = new();
        private readonly List<DestructibleBlock> _feedRelays = new();
        private readonly Dictionary<DestructibleBlock, FeedLink> _feedByRelay = new();
        private readonly Dictionary<string, SearchlightZone> _lightsByName = new(StringComparer.Ordinal);
        private PuzzleManager _feedPuzzle;
        private StaticBody2D _radarGate;

        public IReadOnlyList<SearchlightZone> Searchlights => _searchlights;
        public IReadOnlyList<DestructibleBlock> FeedRelays => _feedRelays;
        public PuzzleManager FeedPuzzle => _feedPuzzle;

        /// <summary>False until every surveillance relay is cut and the radar gate lifts.</summary>
        public bool RadarGateOpen { get; private set; }

        /// <summary>How many relays have been cut (0-3). Drives the HUD counter.</summary>
        public int FeedsCut { get; private set; }

        /// <summary>Times a beam has caught the player. Drives the exposure warning objective.</summary>
        public int ExposureAlarmCount { get; private set; }

        private readonly HashSet<int> _wavesSpawned = new();
        private bool _resumedPastTheGate;

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildCheckpointCharlie();
            BuildDeathStrip();
            BuildRadarYard();
            BuildBunkerStreet();
            BuildExtractors(ExtractorPlacements);
        }

        /// <summary>
        /// Godot readies a packed scene's children before its root, so every authored
        /// template instance in Level_09_Berlin.tscn is already live here.
        /// </summary>
        private void CollectAuthoredNodes() {
            _feedPuzzle = GetNodeOrNull<PuzzleManager>("FeedPuzzle");
            // PuzzleManager re-emits a save-restored completion one deferred frame
            // after _Ready (Package 5 Wave A integration), so this handler is
            // deliberately idempotent.
            if (_feedPuzzle != null) _feedPuzzle.PuzzleCompleted += _ => OnAllFeedsCut();

            foreach (FeedLink link in FeedLinks) {
                if (GetNodeOrNull<SearchlightZone>(link.LightNodeName) is SearchlightZone light) {
                    _searchlights.Add(light);
                    _lightsByName[link.LightNodeName] = light;
                    light.PlayerDetected += _ => OnBeamExposure();
                }
                if (GetNodeOrNull<DestructibleBlock>($"FeedPuzzle/{link.FeedNodeName}") is DestructibleBlock relay) {
                    _feedRelays.Add(relay);
                    _feedByRelay[relay] = link;
                    relay.Destroyed += () => OnFeedRelayCut(relay);
                }
            }
        }

        /// <summary>Room 1: snowed-in ruined blocks. Flat street, rubble climbs, exposed rebar.</summary>
        private void BuildCheckpointCharlie() {
            BuildRoomBackground("BG_Room1_CheckpointCharlie", Room1StartX, 2880f, LevelHeight,
                new Color(0.14f, 0.16f, 0.20f, 0.45f));

            BuildFloor(Room1StartX, GroundY, 2880f);
            BuildPlatform(600f, 1060f, 300f, SnowRubbleColor);
            BuildPlatform(1150f, 940f, 260f, SnowRubbleColor);
            BuildPlatform(1700f, 1050f, 280f, SnowRubbleColor);
            BuildPlatform(2300f, 980f, 320f, SnowRubbleColor);   // holds extractor 1
            BuildPlatform(2650f, 860f, 220f, SnowRubbleColor);

            BuildHazardSpikes(1450f, 1190f, 140f);
            BuildHazardSpikes(2050f, 1190f, 120f);

            BuildCheckpoint(240f, GroundY - 50f, CheckpointID(0), CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "berlin_room_checkpoint_charlie", new Color(0.80f, 0.86f, 0.95f));

            BuildRoomTransition("berlin_room_checkpoint_charlie", new Vector2(560f, 900f),
                new Rect2(Room1StartX, GroundRoomCameraTop, 2880f, 1080f),
                triggerSize: new Vector2(80f, LevelHeight));
        }

        /// <summary>
        /// Room 2: the stealth gauntlet. The beams themselves are authored in the
        /// scene; this builds the geometry that makes timing them matter - three
        /// cover pockets sitting in the gaps between the swept cone footprints, and
        /// the guard-tower climb that ends on the beam-swept upper walkway.
        /// </summary>
        private void BuildDeathStrip() {
            BuildRoomBackground("BG_Room2_DeathStrip", Room2StartX, 3520f, LevelHeight,
                new Color(0.10f, 0.12f, 0.17f, 0.5f));

            BuildFloor(Room2StartX, GroundY, 3520f);

            // Cover pockets. Every slab's span is authored OUTSIDE every beam's swept
            // footprint (Level09ContentTests pins that), so these are genuine safe
            // ground rather than decoration - the Area2D cone has no occlusion, so
            // cover here is positional, and the pockets are where the cones never go.
            foreach ((float x, float width) in CoverPockets) {
                BuildPlatform(x, 1120f, width, CoverPocketColor);
            }

            // Guard tower: the level's first vertical climb, drop-through on the way down.
            BuildOneWayPlatform(5450f, 1040f, 220f);
            BuildOneWayPlatform(5700f, 900f, 220f);
            BuildOneWayPlatform(5480f, 760f, 220f);
            BuildOneWayPlatform(5760f, 640f, 200f);

            // Upper walkway, swept end to end by the watchtower beam.
            BuildPlatform(5900f, 520f, 700f);

            BuildCheckpoint(5200f, GroundY - 50f, CheckpointID(1), CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "berlin_room_death_strip", new Color(1.0f, 0.92f, 0.55f));

            BuildRoomTransition("berlin_room_death_strip", new Vector2(Room2StartX, 700f),
                new Rect2(Room2StartX, 0f, 3520f, LevelHeight),
                triggerSize: new Vector2(80f, LevelHeight));

            _radarGate = BuildDoor("RadarGate", new Vector2(6360f, GroundY),
                "berlin_gate_locked", new Vector2(48f, 900f), new Color(0.30f, 0.33f, 0.38f));

            BuildWaveTrigger("Room2WaveTrigger", new Vector2(Room2StartX + 120f, 1100f), () => SpawnWave(2));
        }

        /// <summary>Room 3: the cloaked radar mast, past the gate the relays unseal.</summary>
        private void BuildRadarYard() {
            BuildRoomBackground("BG_Room3_RadarYard", Room3StartX, 2240f, LevelHeight,
                new Color(0.12f, 0.14f, 0.18f, 0.5f));

            BuildFloor(Room3StartX, GroundY, 2240f);

            // Radar mast: the second guard-tower climb.
            BuildOneWayPlatform(6900f, 1040f, 220f);
            BuildOneWayPlatform(7150f, 900f, 220f);
            BuildOneWayPlatform(6950f, 760f, 200f);
            BuildPlatform(7250f, 620f, 420f);   // mast gantry, holds extractor 3

            BuildPlatform(7900f, 1040f, 260f, SnowRubbleColor);
            BuildPlatform(8300f, 920f, 240f, SnowRubbleColor);

            BuildCheckpoint(8480f, GroundY - 50f, CheckpointID(2), CheckpointRole.PreBoss);
            BuildRoomDecoration(Room3StartX, "berlin_room_radar_yard", new Color(0.60f, 0.85f, 0.70f));

            BuildRoomTransition("berlin_room_radar_yard", new Vector2(Room3StartX + 80f, 700f),
                new Rect2(Room3StartX, 0f, 2240f, LevelHeight),
                triggerSize: new Vector2(80f, LevelHeight));

            BuildWaveTrigger("Room3WaveTrigger", new Vector2(Room3StartX + 200f, 1100f), () => SpawnWave(3));
        }

        /// <summary>
        /// Room 4: the bunker defense battle. A flat snowy street with two waist-high
        /// fortifications for the Chancellor's shockwaves to break line on, and the two
        /// high guard-tower balconies the design asks for. The west wall stops above the
        /// entry so the player is not sealed out of the radar yard.
        /// </summary>
        private void BuildBunkerStreet() {
            BuildRoomBackground("BG_Room4_BunkerStreet", Room4StartX, 1920f, LevelHeight,
                new Color(0.18f, 0.14f, 0.16f, 0.5f));

            BuildFloor(Room4StartX, GroundY, 1920f);

            BuildPlatform(9100f, 800f, 420f);    // west guard-tower balcony
            BuildPlatform(10100f, 800f, 420f);   // east guard-tower balcony

            BuildPlatform(9280f, 1090f, 200f, CoverPocketColor);   // fortification
            BuildPlatform(9960f, 1090f, 200f, CoverPocketColor);   // fortification

            BuildWall(Room4StartX - 20f, 300f, 500f);   // partial: the entry stays open below
            BuildWall(LevelWidth - 20f, 440f, 760f);

            BuildRoomDecoration(Room4StartX, "berlin_room_bunker_street", new Color(0.95f, 0.35f, 0.30f));

            BuildRoomTransition("berlin_room_bunker_street", new Vector2(Room4StartX + 60f, 900f),
                new Rect2(Room4StartX, GroundRoomCameraTop, 1920f, 1080f),
                triggerSize: new Vector2(80f, LevelHeight));

            BuildBossEncounter(BossResourcePath, new Vector2(9600f, 1150f),
                encounterName: "IronChancellorEncounter", revealDistance: 800f);
        }

        public string CheckpointID(int index) => $"{LevelID}_checkpoint_{index}";

        // === Encounters ===

        protected override void SpawnInitialEnemies() => SpawnWave(1);

        private void SpawnWave(int wave) {
            if (!_wavesSpawned.Add(wave)) return;
            var authored = new List<(string EnemyID, Vector2 Position)>();
            foreach ((string enemyID, int waveIndex, Vector2 position) in SpawnTable) {
                if (waveIndex == wave) authored.Add((enemyID, position));
            }
            int count = StoryDifficultyTuning.ScaleEncounterCount(
                authored.Count, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < authored.Count; index++) {
                (string enemyID, Vector2 position) = authored[index];
                EnemyFactory.Spawn(enemyID, this, position,
                    waypointA: position + new Vector2(-220f, 0f),
                    waypointB: position + new Vector2(220f, 0f));
            }
        }

        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == CheckpointID(1)) {
                _wavesSpawned.Add(2);
            } else if (checkpointID == CheckpointID(2)) {
                _wavesSpawned.Add(2);
                _wavesSpawned.Add(3);
                // Checkpoint 2 stands east of the radar gate. A resume that lost the
                // puzzle flag would strand the player behind a sealed gate with the
                // gauntlet still live behind it, so the gate opens and the beams go
                // dark structurally rather than relying on the save.
                _resumedPastTheGate = true;
            }
        }

        // === Era mechanics ===

        protected override void OnLevelReady() {
            if (_resumedPastTheGate || _feedPuzzle is { IsCompleted: true }) {
                // Blow the relays through the same path the player's attacks take, so
                // the counter, the conditions, and the darkened beams all agree with
                // a run that actually cut them (the Level 2 generator precedent).
                CutRemainingRelays();
                OnAllFeedsCut();
            } else {
                PostFeedObjective();
            }
        }

        private void CutRemainingRelays() {
            foreach (DestructibleBlock relay in _feedRelays) {
                if (!IsInstanceValid(relay)) continue;
                for (int guard = 0; guard < 8 && !relay.IsDestroyed; guard++) {
                    relay.TakeEnvironmentDamage(relay.MaxHP);
                }
            }
        }

        /// <summary>
        /// Berlin's beams never touch the Ultimate meter; a catch is a warning that a
        /// drone strike lands in <see cref="SearchlightZone.ExposureGraceSeconds"/>.
        /// The 12-standard budget is locked, so this posts an objective rather than an
        /// alarm wave.
        /// </summary>
        private void OnBeamExposure() {
            ExposureAlarmCount++;
            SetObjective("berlin_objective_exposed");
        }

        private void OnFeedRelayCut(DestructibleBlock relay) {
            if (relay == null || !_feedByRelay.TryGetValue(relay, out FeedLink link)) return;
            if (!DarkenSearchlight(link.LightNodeName)) return;
            FeedsCut++;
            _feedPuzzle?.SetCondition(link.ConditionID, true);
            if (_feedPuzzle is not { IsCompleted: true }) PostFeedObjective();
        }

        /// <summary>
        /// Kills one beam for good. <see cref="SearchlightZone.Enabled"/> stops the
        /// sweep and the exposure tick; dropping the tracked player as well means a
        /// player standing in the cone when the relay blows is released instead of
        /// being frozen mid-countdown. Monitoring is deferred because this runs inside
        /// an Area2D hit callback. Idempotent.
        /// </summary>
        private bool DarkenSearchlight(string lightNodeName) {
            if (!_lightsByName.TryGetValue(lightNodeName, out SearchlightZone light)) return false;
            if (!IsInstanceValid(light) || !light.Enabled) return false;
            light.Enabled = false;
            if (Player != null) light.RemovePlayer(Player);
            light.SetDeferred(Area2D.PropertyName.Monitoring, false);
            if (light.GetNodeOrNull<CanvasItem>("Visual") is CanvasItem beam) beam.Modulate = DarkenedBeamModulate;
            return true;
        }

        /// <summary>Every relay is cut: the gate lifts and the whole strip goes dark. Idempotent.</summary>
        private void OnAllFeedsCut() {
            if (RadarGateOpen) return;
            RadarGateOpen = true;
            foreach (FeedLink link in FeedLinks) DarkenSearchlight(link.LightNodeName);
            OpenDoor(ref _radarGate);
            SetObjective("berlin_objective_reach_bunker");
        }

        private void PostFeedObjective() =>
            SetObjective("berlin_objective_cut_feeds", FeedsCut, FeedLinks.Length);

        // === Searchlight footprint geometry (shared with Level09ContentTests) ===

        /// <summary>
        /// Conservative world-space span an arc-sweeping searchlight cone can reach.
        /// The authored cone is an isoceles triangle from the apex down to
        /// <see cref="SearchlightConeDepth"/> with half-width
        /// <see cref="SearchlightConeHalfWidth"/>; rotating it by half the sweep arc
        /// pushes its far corner out to
        /// <c>halfWidth*cos(a) + depth*sin(a)</c>.
        ///
        /// Used to author checkpoints and cover pockets outside every beam, and
        /// asserted by the content test so re-tuning a sweep arc cannot silently drop
        /// a respawn point into a live beam.
        /// </summary>
        public static Rect2 SweptFootprint(Vector2 origin, float sweepArcDegrees) {
            float halfArc = Mathf.DegToRad(Mathf.Abs(sweepArcDegrees) * 0.5f);
            float reach = SearchlightConeHalfWidth * Mathf.Cos(halfArc) + SearchlightConeDepth * Mathf.Sin(halfArc);
            return new Rect2(origin.X - reach, origin.Y, reach * 2f, SearchlightConeDepth);
        }

        /// <summary>Swept footprints of every authored beam, in scene order.</summary>
        public IEnumerable<Rect2> SearchlightFootprints() {
            foreach (SearchlightZone light in _searchlights) {
                if (IsInstanceValid(light)) yield return SweptFootprint(light.Position, light.SweepArcDegrees);
            }
        }
    }
}
