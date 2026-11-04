using System;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Traversal stretches and optional routes (rulings 7a, 7b, 7c, 7c+). Every piece
    /// is a base-kit piece: no new verb and no rope. The rules live in
    /// <see cref="TraversalRules"/>; a helper throws at build when a call cannot obey
    /// them, so a bad layout fails on the first load of the scene rather than in play.
    /// The recipe, with Level 2 as the worked example, is
    /// <c>docs/handoffs/GR_PILOT.md</c>.
    /// </summary>
    public abstract partial class StoryLevelControllerBase {
        /// <summary>A surface kind on a stretch's line.</summary>
        protected enum StretchStepKind {
            /// <summary>A solid platform: a hero rising under it stops at its underside.</summary>
            Solid,
            /// <summary>A one-way platform: jump up through it, double tap Down to drop.</summary>
            OneWay,
            /// <summary>Shakes when landed on, falls, and comes back. Keep the stretch crossable without it (a trench does).</summary>
            Crumbling
        }

        private const float OneWayStepThickness = 20f;
        private const string CyclicHazardTemplatePath = "res://scenes/templates/CyclicHazardTemplate.tscn";
        private const string LedgeGrabTemplatePath = "res://scenes/templates/LedgeGrabPointTemplate.tscn";
        /// <summary>A grab point stands this far back from the wall's face, so its box straddles the corner.</summary>
        private const float LedgeGrabInsetPixels = 6f;

        /// <summary>
        /// Opens a stretch: records it and builds its room trigger just inside
        /// <paramref name="minX"/>. Call it from <c>BuildLevel</c>'s room order, between
        /// the rooms it joins, so <see cref="FindRoomTriggerContaining"/> meets the rooms
        /// west to east.
        ///
        /// <para>The camera rect runs from <paramref name="cameraWestX"/> (the west edge of
        /// the room before the stretch) to <paramref name="maxX"/>. It deliberately keeps
        /// the room behind in view: a hero who turns back for a secret stays on screen,
        /// and entering the stretch moves only the east limit, so the camera does not
        /// pan.</para>
        ///
        /// <para>The caller ends its deck at <paramref name="westDeckEndX"/> and resumes it
        /// at <paramref name="eastDeckStartX"/>. With a trench those two are the ramps'
        /// high joints; with no trench the deck simply carries on.</para>
        /// </summary>
        protected TraversalStretch BeginStretch(string stretchID, string roomID, float minX, float maxX, float deckY,
            float westDeckEndX, float eastDeckStartX, float cameraWestX) {
            if (maxX - cameraWestX < TraversalRules.MinCameraWidthPixels) {
                throw new ArgumentException($"{stretchID}: its camera rect {cameraWestX}..{maxX} is narrower than one view.");
            }
            RoomTransitionTrigger room = BuildRoomTransition(roomID, new Vector2(minX + 40f, LevelBounds.Size.Y * 0.5f),
                new Rect2(cameraWestX, 0f, maxX - cameraWestX, LevelBounds.Size.Y),
                triggerSize: new Vector2(80f, LevelBounds.Size.Y));
            TraversalStretch stretch = TraversalStretch.Create(stretchID, minX, maxX, deckY, westDeckEndX, eastDeckStartX, room);
            AddChild(stretch);
            return stretch;
        }

        /// <summary>
        /// A non-lethal trench under a stretch's line (<c>VERIFY-STORY-PITS</c>): a floor
        /// <paramref name="depth"/> under the deck from the west deck's end to the east
        /// deck's start, with a walkable ramp at each end, so a hero who falls in walks out.
        /// Throws when the depth or the ramp breaks <see cref="TraversalRules"/>.
        /// </summary>
        protected void BuildStretchTrench(TraversalStretch stretch, float depth = 120f, float rampRun = 160f) {
            if (!TraversalRules.TrenchIsAllowed(depth, rampRun)) {
                throw new ArgumentException($"{stretch.StretchID}: a trench {depth} deep with {rampRun} px ramps breaks the rule.");
            }
            float westX = stretch.WestDeckEndX;
            float eastX = stretch.EastDeckStartX;
            if (eastX - westX < rampRun * 2f) {
                throw new ArgumentException($"{stretch.StretchID}: {westX}..{eastX} has no room for two {rampRun} px ramps.");
            }
            float floorY = stretch.DeckY + depth;
            BuildFloor(westX, floorY, eastX - westX);
            BuildRampBetweenJoints(new Vector2(westX + rampRun, floorY), new Vector2(westX, stretch.DeckY));
            BuildRampBetweenJoints(new Vector2(eastX - rampRun, floorY), new Vector2(eastX, stretch.DeckY));
            stretch.RecordTrench(floorY, rampRun);
        }

        /// <summary>
        /// The next surface of a stretch's line. Steps are recorded in call order, west
        /// deck first, and that order is what the sweep hops. <paramref name="id"/> names
        /// a crumbling step for its hazard events.
        /// </summary>
        protected TraversalSurface BuildStretchStep(TraversalStretch stretch, float x, float y, float width,
            StretchStepKind kind = StretchStepKind.Solid, string id = "") {
            float top;
            switch (kind) {
                case StretchStepKind.OneWay:
                    BuildOneWayPlatform(x, y, width, null, OneWayStepThickness);
                    top = y - OneWayStepThickness / 2f;
                    break;
                case StretchStepKind.Crumbling:
                    BuildLandingCrumblingPlatform(
                        string.IsNullOrEmpty(id) ? $"{stretch.StretchID}.crumbling_{Mathf.RoundToInt(x)}" : id, x, y, width);
                    top = y - DefaultPlatformThickness / 2f;
                    break;
                default:
                    BuildPlatform(x, y, width);
                    top = y - DefaultPlatformThickness / 2f;
                    break;
            }
            var surface = new TraversalSurface(x - width / 2f, x + width / 2f, top);
            stretch.AddLineSurface(surface);
            return surface;
        }

        /// <summary>
        /// A rise to climb: a solid block <paramref name="width"/> wide whose top is the next
        /// surface of the line, with a <see cref="LedgeGrabPoint"/> on its west corner. A hero
        /// double-jumps it or catches the ledge. Keep its top within one hop of the surface
        /// before it and of the ramp or deck below it.
        /// </summary>
        protected TraversalSurface BuildStretchWallStep(TraversalStretch stretch, float westX, float topY, float bottomY,
            float width = 40f) {
            BuildWall(westX, topY, bottomY - topY, null, width);
            var ledge = ResourceLoader.Load<PackedScene>(LedgeGrabTemplatePath).Instantiate<LedgeGrabPoint>();
            ledge.Name = $"LedgeGrab_{Mathf.RoundToInt(westX)}";
            ledge.Position = new Vector2(westX - LedgeGrabInsetPixels, topY);
            AddChild(ledge);
            var surface = new TraversalSurface(westX, westX + width, topY);
            stretch.AddLineSurface(surface);
            return surface;
        }

        /// <summary>
        /// A telegraphed cyclic hazard standing on a floor at <paramref name="floorY"/>
        /// (the template's strip is 120 px wide and rises 220 px). A trench lane takes a
        /// <paramref name="withCover"/> mantlet as the mortar lanes do. Built with
        /// <paramref name="startEnabled"/> false it is an arena change's piece: it does
        /// nothing until a level sets <see cref="StoryCyclicHazard.Enabled"/>, and a death
        /// rewind leaves it as it stands.
        /// </summary>
        protected StoryCyclicHazard BuildCyclicHazardLane(string hazardID, float x, float floorY,
            float warningSeconds, float activeSeconds, float cooldownSeconds, int damage,
            bool startEnabled = true, bool withCover = false) {
            var hazard = ResourceLoader.Load<PackedScene>(CyclicHazardTemplatePath).Instantiate<StoryCyclicHazard>();
            hazard.Name = $"Hazard_{hazardID.Replace('.', '_')}";
            hazard.HazardID = hazardID;
            hazard.Position = new Vector2(x, floorY);
            hazard.WarningDuration = warningSeconds;
            hazard.ActiveDuration = activeSeconds;
            hazard.CooldownDuration = cooldownSeconds;
            hazard.Damage = damage;
            hazard.Enabled = startEnabled;
            if (!startEnabled) hazard.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            if (withCover) hazard.AddChild(new VolleyCover { Name = "Mantlet", Position = new Vector2(-24f, 0f) });
            AddChild(hazard);
            return hazard;
        }

        /// <summary>
        /// A swarm row's lock wave (rulings 1b, 1b+, 1b++): every spawn is the fodder mob at
        /// <paramref name="y"/>, triggered as <see cref="SwarmWaveRules"/> says. Its table is
        /// the manifest row of the same ID, the row's count taken from
        /// <see cref="SwarmWaveRules.RowMobsForLockWidth"/>. Throws on a row outside 6–10.
        /// </summary>
        protected static EncounterLockWave SwarmWave(string rowID, float y, params float[] spawnXs) {
            int count = spawnXs?.Length ?? 0;
            if (count < SwarmWaveRules.RowMinMobs || count > SwarmWaveRules.RowMaxMobs) {
                throw new ArgumentException($"{rowID}: a swarm row has {SwarmWaveRules.RowMinMobs}-{SwarmWaveRules.RowMaxMobs} mobs, not {count}.");
            }
            var spawns = new EncounterLockSpawn[count];
            for (int index = 0; index < count; index++) {
                spawns[index] = new EncounterLockSpawn(HazardMobRules.FodderEnemyID, new Vector2(spawnXs[index], y));
            }
            return new EncounterLockWave {
                RowID = rowID,
                Spawns = spawns,
                Trigger = EncounterWaveTrigger.WhenAliveAtMost,
                AliveAtMost = SwarmWaveRules.TriggerAliveAtMost
            };
        }

        /// <summary>
        /// Opens an optional route: the reward is an existing manifest source (an Extractor
        /// share or the secret), so the route adds no dust. <paramref name="entry"/> is the
        /// surface on the main path the base-kit line leaves from.
        /// </summary>
        protected OptionalRoute BeginOptionalRoute(string routeID, string rewardSourceID, Vector2 rewardPosition,
            TraversalSurface entry) {
            OptionalRoute route = OptionalRoute.Create(routeID, rewardSourceID, rewardPosition, entry);
            AddChild(route);
            return route;
        }

        /// <summary>
        /// The next surface of a route's base-kit line, a one-way platform so a hero rises
        /// through it from any side. Every hero must reach the end of this line on the base
        /// kit (<c>OptionalRouteReachTests</c> checks each with its own jump).
        /// </summary>
        protected TraversalSurface BuildRouteStep(OptionalRoute route, float x, float y, float width) {
            BuildOneWayPlatform(x, y, width, null, OneWayStepThickness);
            var surface = new TraversalSurface(x - width / 2f, x + width / 2f, y - OneWayStepThickness / 2f);
            route.AddBaseLineSurface(surface);
            return surface;
        }

        /// <summary>
        /// A crumbling platform that starts on a landing (<see cref="BuildFracturePlatform"/>
        /// starts on the Collapse Tremor instead). It comes back after its respawn, and a rewind
        /// restores it.
        /// </summary>
        private CrumblingPlatform BuildLandingCrumblingPlatform(string platformID, float x, float y, float width) {
            var platform = new CrumblingPlatform {
                Name = $"Crumbling_{platformID.Replace('.', '_')}",
                PlatformID = platformID,
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            platform.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(width, DefaultPlatformThickness) }
            });
            platform.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, DefaultPlatformThickness),
                Position = new Vector2(-width / 2f, -DefaultPlatformThickness / 2f),
                Color = PlatformColor.Lerp(HazardColor, 0.35f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            var sensor = new Area2D {
                Name = "LandingSensor",
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            sensor.AddChild(new CollisionShape2D {
                Position = new Vector2(0f, -DefaultPlatformThickness),
                Shape = new RectangleShape2D { Size = new Vector2(width, DefaultPlatformThickness) }
            });
            platform.AddChild(sensor);
            AddChild(platform);
            return platform;
        }
    }
}
