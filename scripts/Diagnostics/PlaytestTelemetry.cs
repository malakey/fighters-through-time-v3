using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;

namespace FTT.Diagnostics {

    /// <summary>One enemy lifetime (a pooled body that respawns starts a new record).</summary>
    public sealed class EnemyRecord {
        public string Id = "";
        public string Kind = "standard";
        public int MaxHP;
        public int SpawnFrame;
        public int EngagedFrame = -1;
        public int FirstDamagedFrame = -1;
        public int KilledFrame = -1;
        public int PassedFrame = -1;
        public int DamageTaken;
        public int HitsOnPlayer;
        public int DamageToPlayer;
        public int AttacksStarted;
        /// <summary>Largest damage this enemy took inside any 120-frame (2 s) window.</summary>
        public int MaxBurst120;
        public float SpawnX;
        public readonly Dictionary<string, int> DamageByBotAction = new();

        internal readonly Queue<(int Frame, int Damage)> Window = new();
        internal int LastHP;
        internal bool WasAttacking;
        internal bool Closed;

        public int? FramesToKill => KilledFrame >= 0 && FirstDamagedFrame >= 0 ? KilledFrame - FirstDamagedFrame : null;
    }

    /// <summary>
    /// Polls the live Story world once per unpaused physics frame and listens to
    /// <see cref="EventBus"/>. Pure observation: it never writes gameplay state.
    /// </summary>
    public sealed class PlaytestTelemetry {
        private const float EngageDistance = 450f;
        private const float PassDistance = 250f;
        private const float AttributionDistance = 320f;

        private readonly Dictionary<ulong, EnemyRecord> _live = new();
        public readonly List<EnemyRecord> Enemies = new();
        public readonly Dictionary<string, (int Count, float Damage)> HitConfirms = new();
        public readonly List<(int Frame, string Room)> Rooms = new();
        public readonly List<int> CheckpointFrames = new();
        /// <summary>Once-a-second player snapshot, for spotting where a bot stalled.</summary>
        public readonly List<object> Trace = new();

        public int Frame { get; private set; }
        public int PlayerDamageTaken;
        public int PlayerHitsTaken;
        public int PlayerDeaths;
        public int Rewinds;
        public float StartX = float.NaN;
        public float MaxX = float.NegativeInfinity;
        public float LastIntegrity = 100f;
        public int LowestPlayerHP = int.MaxValue;
        public int PlayerMaxHP;
        public int CompletedFrame = -1;
        /// <summary>Trace sampling interval in frames (60 = once a second; --trace-every).</summary>
        public int TraceEveryFrames = 60;
        public int BossDefeatedFrame = -1;
        public int SealedFrame = -1;
        public int CheckpointsActivated;

        /// <summary>Notable moments in order: boss down, seal, checkpoints, dialogue, deaths, rooms.</summary>
        public readonly List<object> Events = new();
        /// <summary>Damage the hero took, by source (enemy id, hazard id, "env").</summary>
        public readonly Dictionary<string, int> DamageBySource = new();
        public readonly List<object> DeathDetails = new();
        private readonly Queue<string> _recentSources = new();

        private PlaytestWorld _world;
        private PlaytestBot _bot;

        private void AddEvent(string type, string detail) {
            if (Events.Count >= 400) return;
            Events.Add(new Dictionary<string, object> { ["f"] = Frame, ["type"] = type, ["detail"] = detail });
        }

        public void Attach(PlaytestWorld world, PlaytestBot bot) {
            _world = world;
            _bot = bot;
            EventBus bus = EventBus.Instance;
            if (bus == null) return;
            bus.OnPlayerHPChanged += OnPlayerHP;
            bus.OnPlayerDied += OnPlayerDied;
            bus.OnRewindTriggered += OnRewind;
            bus.OnHitConfirm += OnHitConfirm;
            bus.OnRoomTransitioned += OnRoom;
            bus.OnCheckpointReached += OnCheckpoint;
            bus.OnTimelineIntegrityChanged += OnIntegrity;
            bus.OnLevelComplete += OnLevelComplete;
            bus.OnBossDefeated += OnBossDefeated;
            bus.OnSealingAnchorChanged += OnSealingAnchor;
            bus.OnCheckpointActivated += OnCheckpointActivated;
            bus.OnDialogueTriggered += OnDialogue;
            bus.OnBossSpawned += OnBossSpawned;
        }

        public void Detach() {
            EventBus bus = EventBus.Instance;
            if (bus == null) return;
            bus.OnPlayerHPChanged -= OnPlayerHP;
            bus.OnPlayerDied -= OnPlayerDied;
            bus.OnRewindTriggered -= OnRewind;
            bus.OnHitConfirm -= OnHitConfirm;
            bus.OnRoomTransitioned -= OnRoom;
            bus.OnCheckpointReached -= OnCheckpoint;
            bus.OnTimelineIntegrityChanged -= OnIntegrity;
            bus.OnLevelComplete -= OnLevelComplete;
            bus.OnBossDefeated -= OnBossDefeated;
            bus.OnSealingAnchorChanged -= OnSealingAnchor;
            bus.OnCheckpointActivated -= OnCheckpointActivated;
            bus.OnDialogueTriggered -= OnDialogue;
            bus.OnBossSpawned -= OnBossSpawned;
        }

        private string _lastPuzzleSnapshot = "";

        /// <summary>Compact state of the level's puzzle pieces (plates, coils, glyph locks, breakables).</summary>
        private string PuzzleSnapshot() {
            var parts = new List<string>();
            foreach (PressurePlate plate in _world.Plates) {
                if (GodotObject.IsInstanceValid(plate)) parts.Add($"{plate.Name}:{plate.CurrentWeight:0.#}/{plate.RequiredWeight:0.#}");
            }
            Godot.Collections.Array<Node> weights = _world.Tree.GetNodesInGroup(WeightedObject.MovableWeightGroup);
            using (weights.AsDisposable()) {
                foreach (Node node in weights) {
                    if (node is WeightedObject weight && GodotObject.IsInstanceValid(weight)) {
                        parts.Add($"{weight.Name}@{Mathf.RoundToInt(weight.GlobalPosition.X)},{Mathf.RoundToInt(weight.GlobalPosition.Y)}{(weight.Sleeping ? "z" : "")}");
                    }
                }
            }
            foreach (ConductiveCoil coil in _world.Coils) {
                if (GodotObject.IsInstanceValid(coil)) parts.Add($"{coil.Name}:q{coil.QuarterTurns}{(coil.IsPowered ? "+" : "")}");
            }
            foreach (SequenceLock sequence in _world.SequenceLocks) {
                if (GodotObject.IsInstanceValid(sequence)) parts.Add($"{sequence.Name}:{sequence.Progress}/{sequence.GlyphCount}{(sequence.IsCompleted ? " done" : "")}");
            }
            foreach (DamageableEnvironmentObject breakable in _world.Breakables) {
                if (GodotObject.IsInstanceValid(breakable) && breakable is not ChronalExtractor) {
                    parts.Add($"{breakable.Name}:{(breakable.IsDestroyed ? "broken" : breakable.CurrentHP.ToString())}");
                }
            }
            return string.Join(" ", parts);
        }

        /// <summary>Free-form note from the runner (watchdogs, scripted beats).</summary>
        public void Note(string type, string detail) => AddEvent(type, detail);

        public bool LevelCompleted => CompletedFrame >= 0;

        public void Tick(SceneTree tree) {
            Frame++;
            PlayerController player = _world.Player;
            Vector2 playerPosition = Vector2.Zero;
            if (player != null) {
                playerPosition = player.GlobalPosition;
                if (float.IsNaN(StartX)) StartX = playerPosition.X;
                MaxX = Mathf.Max(MaxX, playerPosition.X);
                PlayerMaxHP = player.MaximumHP;
                if (player.CurrentHP > 0) LowestPlayerHP = Math.Min(LowestPlayerHP, player.CurrentHP);
                if (Frame % TraceEveryFrames == 1 % TraceEveryFrames) {
                    Trace.Add(new Dictionary<string, object> {
                        ["f"] = Frame,
                        ["x"] = Mathf.RoundToInt(playerPosition.X),
                        ["y"] = Mathf.RoundToInt(playerPosition.Y),
                        ["hp"] = player.CurrentHP,
                        ["state"] = player.CurrentState.ToString(),
                        ["vx"] = Mathf.RoundToInt(player.Velocity.X),
                        ["vy"] = Mathf.RoundToInt(player.Velocity.Y),
                        ["bot"] = _bot?.CurrentAction ?? "",
                        ["nav"] = _bot?.NavDescription ?? "",
                        ["target"] = _bot?.CurrentTarget is Node2D target && GodotObject.IsInstanceValid(target)
                            ? $"{target.Name}@{Mathf.RoundToInt(target.GlobalPosition.X)},{Mathf.RoundToInt(target.GlobalPosition.Y)}"
                            : ""
                    });
                    if (_bot?.CurrentTarget == null && Trace.Count > 0 && Trace[^1] is Dictionary<string, object> last) {
                        // Idle: name the nearest foe so a stalled run shows what it is waiting on.
                        Node2D nearestFoe = null;
                        float best = float.MaxValue;
                        foreach (Node2D foe in _world.Foes()) {
                            float distance = foe.GlobalPosition.DistanceTo(playerPosition);
                            if (distance < best) { best = distance; nearestFoe = foe; }
                        }
                        if (nearestFoe != null) {
                            string state = nearestFoe is BossController boss ? $"{boss.CurrentState}{(boss.IsMechanicInvulnerable ? " invuln" : "")}" : "";
                            last["foe"] = $"{nearestFoe.Name}@{Mathf.RoundToInt(nearestFoe.GlobalPosition.X)},{Mathf.RoundToInt(nearestFoe.GlobalPosition.Y)} hp={PlaytestWorld.HealthOf(nearestFoe)} {state}";
                        }
                    }
                    if (Frame % 300 == 1) {
                        string puzzles = PuzzleSnapshot();
                        if (puzzles != _lastPuzzleSnapshot) {
                            _lastPuzzleSnapshot = puzzles;
                            AddEvent("puzzles", puzzles);
                        }
                    }
                }
            }

            if (Frame % 10 == 0) TrackObjectives(tree);

            var seen = new HashSet<ulong>();
            Godot.Collections.Array<Node> nodes = tree.GetNodesInGroup("Enemies");
            using (nodes.AsDisposable()) {
                foreach (Node node in nodes) {
                    if (node is not Node2D body || !GodotObject.IsInstanceValid(body)) continue;
                    int hp;
                    bool alive;
                    bool attacking = false;
                    string id;
                    string kind;
                    if (body is EnemyController enemy) {
                        hp = enemy.CurrentHP;
                        alive = enemy.IsAlive && body.IsVisibleInTree();
                        attacking = enemy.CurrentState == EnemyState.Attacking;
                        id = enemy.Data?.EnemyID ?? body.Name;
                        kind = enemy.Data?.Tier.ToString().ToLowerInvariant() ?? "standard";
                    } else if (body is BossController boss) {
                        hp = boss.CurrentHP;
                        alive = boss.IsAlive && body.IsVisibleInTree();
                        id = boss.Data?.BossID ?? body.Name;
                        kind = "boss";
                    } else {
                        continue;
                    }

                    ulong key = body.GetInstanceId();
                    _live.TryGetValue(key, out EnemyRecord record);
                    if (record != null && record.Closed && alive && hp > 0) record = null; // pool respawn
                    if (record == null) {
                        if (!alive || hp <= 0) continue;
                        record = new EnemyRecord {
                            Id = id, Kind = kind, MaxHP = hp, LastHP = hp,
                            SpawnFrame = Frame, SpawnX = body.GlobalPosition.X
                        };
                        _live[key] = record;
                        Enemies.Add(record);
                    }
                    if (record.Closed) continue;
                    seen.Add(key);
                    Observe(record, body, hp, alive, attacking, playerPosition, player != null);
                }
            }

            // A body that left the group (freed / pooled out) while alive counts as gone, not killed.
            foreach (KeyValuePair<ulong, EnemyRecord> entry in _live) {
                if (!seen.Contains(entry.Key) && !entry.Value.Closed && entry.Value.KilledFrame < 0) {
                    entry.Value.Closed = entry.Value.LastHP <= 0;
                    if (entry.Value.Closed) entry.Value.KilledFrame = Frame;
                }
            }
        }

        /// <summary>Breakables (generators, Extractors, crates) and the frame each broke, by object name.</summary>
        public readonly Dictionary<string, int> Objectives = new();

        private void TrackObjectives(SceneTree tree) {
            Godot.Collections.Array<Node> nodes = tree.GetNodesInGroup("damageable_environment");
            using var lifetime = nodes.AsDisposable();
            foreach (Node node in nodes) {
                if (node is not FTT.Environment.DamageableEnvironmentObject target || !GodotObject.IsInstanceValid(target)) continue;
                string key = $"{target.Name}@{Mathf.RoundToInt(target.GlobalPosition.X)}";
                if (!Objectives.TryGetValue(key, out int brokenFrame)) Objectives[key] = brokenFrame = -1;
                if (brokenFrame < 0 && target.IsDestroyed) Objectives[key] = Frame;
            }
        }

        private void Observe(EnemyRecord record, Node2D body, int hp, bool alive, bool attacking,
            Vector2 playerPosition, bool hasPlayer) {
            record.MaxHP = Math.Max(record.MaxHP, hp);
            if (attacking && !record.WasAttacking) record.AttacksStarted++;
            record.WasAttacking = attacking;

            int damage = record.LastHP - hp;
            if (damage > 0) {
                if (record.FirstDamagedFrame < 0) record.FirstDamagedFrame = Frame;
                record.DamageTaken += damage;
                string action = _bot?.CurrentAction ?? "unknown";
                record.DamageByBotAction[action] = record.DamageByBotAction.GetValueOrDefault(action) + damage;
                record.Window.Enqueue((Frame, damage));
            }
            while (record.Window.Count > 0 && record.Window.Peek().Frame <= Frame - 120) record.Window.Dequeue();
            record.MaxBurst120 = Math.Max(record.MaxBurst120, record.Window.Sum(entry => entry.Damage));
            record.LastHP = hp;

            if (hasPlayer) {
                float distance = playerPosition.DistanceTo(body.GlobalPosition);
                if (record.EngagedFrame < 0 && distance <= EngageDistance) record.EngagedFrame = Frame;
                if (record.PassedFrame < 0 && hp > 0 && playerPosition.X - body.GlobalPosition.X > PassDistance
                    && record.EngagedFrame >= 0) {
                    record.PassedFrame = Frame;
                }
            }

            if ((!alive || hp <= 0) && record.KilledFrame < 0) {
                record.KilledFrame = Frame;
                record.Closed = true;
            }
        }

        private void OnPlayerHP(PlayerHPPayload payload) {
            if (payload.PlayerIndex != 0 || payload.DamageAmount <= 0f) return;
            int damage = Mathf.RoundToInt(payload.DamageAmount);
            PlayerDamageTaken += damage;
            PlayerHitsTaken++;
            PlayerController player = _world?.Player;
            if (player == null) return;
            string source = HazardSourceAt(player);
            if (source != null) {
                Attribute(source, damage);
                return;
            }
            // Attribute the hit to the nearest live enemy — close enough for a report.
            EnemyRecord nearest = null;
            float best = AttributionDistance;
            foreach (KeyValuePair<ulong, EnemyRecord> entry in _live) {
                if (entry.Value.Closed) continue;
                if (GodotObject.InstanceFromId(entry.Key) is not Node2D body) continue;
                float distance = body.GlobalPosition.DistanceTo(player.GlobalPosition);
                if (distance < best) {
                    best = distance;
                    nearest = entry.Value;
                }
            }
            if (nearest == null) {
                Attribute("env", damage);
                return;
            }
            nearest.HitsOnPlayer++;
            nearest.DamageToPlayer += damage;
            Attribute(nearest.Id, damage);
        }

        private void Attribute(string source, int damage) {
            DamageBySource[source] = DamageBySource.GetValueOrDefault(source) + damage;
            _recentSources.Enqueue($"{source}:{damage}@{Frame}");
            while (_recentSources.Count > 4) _recentSources.Dequeue();
        }

        /// <summary>A hot hazard the hero is standing in, if any (cyclic hazards, escape front, Extractor discharge, searchlight).</summary>
        private string HazardSourceAt(PlayerController player) {
            Vector2 feet = player.GlobalPosition;
            foreach (PlaytestWorld.HazardZone zone in _world.LiveCyclicHazards()) {
                if (zone.Hazard.Phase != HazardPhase.Active) continue;
                Rect2 r = zone.Area.Grow(24f);
                if (feet.X > r.Position.X && feet.X < r.End.X && feet.Y > r.Position.Y && feet.Y - 64f < r.End.Y) {
                    return string.IsNullOrEmpty(zone.Hazard.HazardID) ? zone.Hazard.Name : zone.Hazard.HazardID;
                }
            }
            EscapeSequenceController escape = _world.RunningEscape();
            if (escape != null && feet.X - escape.FrontX < 80f) return escape.SequenceID;
            foreach (ChronalExtractor extractor in _world.Extractors) {
                if (GodotObject.IsInstanceValid(extractor) && !extractor.IsDestroyed && extractor.GlobalPosition.DistanceTo(feet) < 240f
                    && extractor.DischargeCount > 0 && !extractor.IsTelegraphing && extractor is not ResonanceHoldNode) {
                    return "extractor:" + extractor.ObjectID;
                }
            }
            foreach (SearchlightZone light in _world.Searchlights) {
                if (GodotObject.IsInstanceValid(light) && light.Enabled && light.Mode == SearchlightMode.DelayedStrike
                    && light.TrackedPlayerCount > 0 && light.ExposureFor(player) < 0.1f && light.StrikeCount > 0) {
                    return "searchlight:" + light.SearchlightID;
                }
            }
            return null;
        }

        private void OnPlayerDied(int playerIndex) {
            if (playerIndex != 0) return;
            PlayerDeaths++;
            PlayerController player = _world?.Player;
            var detail = new Dictionary<string, object> {
                ["f"] = Frame,
                ["x"] = player != null ? Mathf.RoundToInt(player.GlobalPosition.X) : 0,
                ["y"] = player != null ? Mathf.RoundToInt(player.GlobalPosition.Y) : 0,
                ["bot"] = _bot?.CurrentAction ?? "",
                ["recent_damage"] = string.Join(" ", _recentSources)
            };
            DeathDetails.Add(detail);
            AddEvent("death", $"{detail["x"]},{detail["y"]} {detail["recent_damage"]}");
        }

        private void OnBossDefeated(BossDefeatedPayload payload) {
            if (BossDefeatedFrame < 0) BossDefeatedFrame = Frame;
            AddEvent("boss_defeated", payload.BossID ?? "");
        }

        private void OnBossSpawned(BossSpawnedPayload payload) =>
            AddEvent("boss_spawned", $"{payload.BossID}@{Mathf.RoundToInt(payload.Position.X)},{Mathf.RoundToInt(payload.Position.Y)}");

        private void OnSealingAnchor(SealingAnchorPayload payload) {
            if (payload.State == SealingAnchorState.Sealed && SealedFrame < 0) SealedFrame = Frame;
            AddEvent("sealing_anchor", $"{payload.AnchorID}:{payload.State}");
        }

        private void OnCheckpointActivated(CheckpointReachedPayload payload) {
            if (payload.FirstActivation) CheckpointsActivated++;
            AddEvent("checkpoint", $"{payload.CheckpointID}{(payload.FirstActivation ? " (first)" : "")}");
        }

        private void OnDialogue(string dialogueID) => AddEvent("dialogue", dialogueID ?? "");
        private void OnRewind(Vector2 _) => Rewinds++;
        private void OnRoom(RoomTransitionPayload payload) {
            Rooms.Add((Frame, payload.RoomID));
            AddEvent("room", payload.RoomID ?? "");
        }
        private void OnCheckpoint(string id) => CheckpointFrames.Add(Frame);
        private void OnIntegrity(IntegrityPayload payload) => LastIntegrity = payload.Percent;
        private void OnLevelComplete(string _) { if (CompletedFrame < 0) CompletedFrame = Frame; }

        private void OnHitConfirm(HitConfirmPayload payload) {
            if (payload.PlayerIndex != 0) return;
            string key = string.IsNullOrEmpty(payload.AttackID) ? "unknown" : payload.AttackID;
            (int count, float damage) = HitConfirms.GetValueOrDefault(key);
            HitConfirms[key] = (count + 1, damage + payload.DamageApplied);
        }

        /// <summary>Plain dictionary tree for JSON serialization.</summary>
        public Dictionary<string, object> ToReport() {
            List<EnemyRecord> engaged = Enemies.Where(e => e.EngagedFrame >= 0).ToList();
            List<EnemyRecord> killed = Enemies.Where(e => e.KilledFrame >= 0 && e.DamageTaken > 0).ToList();
            List<EnemyRecord> passedAlive = Enemies.Where(e => e.PassedFrame >= 0
                && (e.KilledFrame < 0 || e.KilledFrame > e.PassedFrame)).ToList();
            List<int> ttk = killed.Where(e => e.FramesToKill.HasValue).Select(e => e.FramesToKill.Value).OrderBy(v => v).ToList();

            return new Dictionary<string, object> {
                ["frames"] = Frame,
                ["seconds"] = Math.Round(Frame / 60.0, 2),
                ["level_completed"] = LevelCompleted,
                ["completed_frame"] = CompletedFrame,
                ["progress_px"] = float.IsNaN(StartX) ? 0 : Math.Round(MaxX - StartX),
                ["rooms_entered"] = Rooms.Count,
                ["checkpoints"] = CheckpointFrames.Count,
                ["checkpoints_first_activations"] = CheckpointsActivated,
                ["boss_defeated_frame"] = BossDefeatedFrame,
                ["sealed_frame"] = SealedFrame,
                ["final_integrity"] = Math.Round(LastIntegrity, 2),
                ["player"] = new Dictionary<string, object> {
                    ["max_hp"] = PlayerMaxHP,
                    ["damage_taken"] = PlayerDamageTaken,
                    ["hits_taken"] = PlayerHitsTaken,
                    ["lowest_hp"] = LowestPlayerHP == int.MaxValue ? PlayerMaxHP : LowestPlayerHP,
                    ["deaths"] = PlayerDeaths,
                    ["rewinds"] = Rewinds,
                    ["damage_by_source"] = DamageBySource.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => (object)kv.Value)
                },
                ["deaths_detail"] = DeathDetails,
                ["events"] = Events,
                ["enemy_summary"] = new Dictionary<string, object> {
                    ["seen"] = Enemies.Count,
                    ["engaged"] = engaged.Count,
                    ["bot_gave_up_unreachable"] = _bot?.UnreachableTargets ?? 0,
                    ["killed"] = killed.Count,
                    ["passed_while_alive"] = passedAlive.Count,
                    ["passed_then_hit_player"] = passedAlive.Count(e => e.HitsOnPlayer > 0),
                    ["engaged_that_ever_hit_player"] = engaged.Count(e => e.HitsOnPlayer > 0),
                    ["median_frames_to_kill"] = ttk.Count == 0 ? -1 : ttk[ttk.Count / 2],
                    ["max_frames_to_kill"] = ttk.Count == 0 ? -1 : ttk[^1],
                    ["killed_within_2s_of_first_hit"] = ttk.Count(v => v <= 120),
                    ["killed_by_one_burst"] = killed.Count(e => e.MaxBurst120 >= e.MaxHP)
                },
                ["hit_confirms"] = HitConfirms.OrderByDescending(kv => kv.Value.Damage).ToDictionary(
                    kv => kv.Key,
                    kv => (object)new Dictionary<string, object> { ["count"] = kv.Value.Count, ["damage"] = Math.Round(kv.Value.Damage, 1) }),
                ["enemies"] = Enemies.Select(e => (object)new Dictionary<string, object> {
                    ["id"] = e.Id,
                    ["kind"] = e.Kind,
                    ["max_hp"] = e.MaxHP,
                    ["spawn_frame"] = e.SpawnFrame,
                    ["spawn_x"] = Math.Round(e.SpawnX),
                    ["engaged_frame"] = e.EngagedFrame,
                    ["first_damaged_frame"] = e.FirstDamagedFrame,
                    ["killed_frame"] = e.KilledFrame,
                    ["frames_to_kill"] = e.FramesToKill ?? -1,
                    ["max_burst_2s"] = e.MaxBurst120,
                    ["damage_taken"] = e.DamageTaken,
                    ["passed_frame"] = e.PassedFrame,
                    ["attacks_started"] = e.AttacksStarted,
                    ["hits_on_player"] = e.HitsOnPlayer,
                    ["damage_to_player"] = e.DamageToPlayer,
                    ["damage_by_bot_action"] = e.DamageByBotAction.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
                }).ToList(),
                ["objectives_broken_frame"] = Objectives.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["rooms"] = Rooms.Select(r => (object)new Dictionary<string, object> { ["frame"] = r.Frame, ["room"] = r.Room }).ToList(),
                ["trace"] = Trace
            };
        }
    }
}
