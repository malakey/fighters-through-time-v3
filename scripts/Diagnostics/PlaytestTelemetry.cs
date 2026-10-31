using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FTT.Characters;
using FTT.Combat;
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
        /// <summary>GU-BOT: the encounter lock that owned this body when it was first seen as a member ("" for none).</summary>
        public string LockID = "";
        public readonly Dictionary<string, int> DamageByBotAction = new();

        internal readonly Queue<(int Frame, int Damage)> Window = new();
        internal int LastHP;
        internal bool WasAttacking;
        internal bool Closed;

        /// <summary>The HP the telemetry last read (0 once the body was killed).</summary>
        public int LastSeenHP => LastHP;

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
            // D6d: hear the hero's melee contacts from the first frame on.
            PlayerController hero = world?.Player;
            if (hero != null && hero.PlayerIndex == 0) {
                EnsureMeleeSubscription(hero);
                // CM-BOT: and every HP loss it takes (the re-hit counter).
                EnsureDamageSubscription(hero);
            }
            EventBus bus = EventBus.Instance;
            if (bus == null) return;
            _unsubscribeLockoutRefused = PlaytestWorld.SubscribeLockoutRefused(OnLockoutRefused);
            _unsubscribeDefyExecuted = PlaytestWorld.SubscribeDefyExecuted(OnDefyExecuted);
            bus.OnUltimateActivation += OnUltimateActivation;
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
            UnsubscribeMelee();
            UnsubscribeDamage();
            CloseOpenMobSwings();
            UnsubscribeMobs();
            _unsubscribeLockoutRefused?.Invoke();
            _unsubscribeLockoutRefused = null;
            _unsubscribeDefyExecuted?.Invoke();
            _unsubscribeDefyExecuted = null;
            EventBus bus = EventBus.Instance;
            if (bus == null) return;
            bus.OnUltimateActivation -= OnUltimateActivation;
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
            // CM-BOT: the engine physics frame this Frame belongs to (the re-hit frame mapping).
            _tickedPhysicsFrame = Engine.GetPhysicsFrames();
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
                            : "",
                        // D6d: the Ultimate meter and the ability lockout.
                        ["meter"] = Math.Round(player.CurrentUltimateMeter, 1),
                        ["lock"] = player.AbilityLockoutFramesRemaining,
                        // MM-BOTS: the Momentum gauge in pips (trace.py prints it as mo=).
                        ["mo"] = Math.Round(PlaytestWorld.MomentumPips(player), 2)
                    });
                    if (PlaytestWorld.HasHitConfirmGate && Trace[^1] is Dictionary<string, object> sample) {
                        sample["sgate"] = PlaytestWorld.SpecialGateFrames(player);
                    }
                    // GU-BOT: the engaged encounter lock (ID tail : enemies left) and,
                    // from the previous frame's crowd sample, the mobs attacking near
                    // the hero and its slot holders.
                    if (Trace[^1] is Dictionary<string, object> crowdSample) {
                        string lockCell = EngagedLockTraceCell();
                        if (lockCell.Length > 0) crowdSample["enc"] = lockCell;
                        if (_lastEngagementFrame.AttackingNear > 0) crowdSample["atk"] = _lastEngagementFrame.AttackingNear;
                        if (_lastEngagementFrame.Holders > 0) crowdSample["slots"] = _lastEngagementFrame.Holders;
                    }
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

            // D6d combat telemetry (specs/C-meter-telemetry.md §1.4). The Mirror
            // clone leaves StoryPlayer, so this is the hero; the index check is
            // insurance.
            if (player != null && player.PlayerIndex == 0) {
                TrackPlayerInstance(player);
                TrackMeter(player);
                // Before anything this tick can close the finisher record.
                NoteFinisherTargetHealth();
                TrackSwing(player);
                TrackAbilitySerials(player);
                TrackMomentum(player);
                TrackPresses(player);
                SampleFinisherTarget(player);
                _finishers.Tick(Frame);
            }

            if (Frame % 10 == 0) TrackObjectives(tree);

            BeginEngagementFrame();
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
                        kind = KindOf(enemy);
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
                        // C3: hear this mob's melee contacts (once per instance; a
                        // pooled respawn keeps the same hitbox and subscription).
                        if (body is EnemyController mob) EnsureMobSubscription(mob);
                    }
                    if (record.Closed) continue;
                    seen.Add(key);
                    Observe(record, body, hp, alive, attacking, playerPosition, player != null);
                    if (body is EnemyController melee) TrackMobSwing(melee, player);
                    // GU-BOT: the hero's crowd (the engagement director's roles and
                    // the concurrent attackers), over living mobs only.
                    if (body is EnemyController crowd && alive && hp > 0) ObserveEngagement(crowd, player, playerPosition);
                }
            }
            // C3: a mob that died, despawned or left the group mid-swing cuts it.
            CloseUntrackedMobSwings();
            if (player != null) FinishEngagementFrame();
            // GU-BOT: the encounter locks (after the enemy loop, so every living
            // member already has its EnemyRecord).
            TrackLocks(player);

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
            _finishers.OnHeroHit();
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
            _finishers.OnHeroDied(Frame);
            _defy.RecordDeath(Frame);
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
            // D6d self-check: every basic confirm the bus carries is raised by the
            // hero's own melee handler, which runs before OnMeleeContact.
            if (!payload.IsHeavy) _busBasicConfirms++;
            string key = string.IsNullOrEmpty(payload.AttackID) ? "unknown" : payload.AttackID;
            (int count, float damage) = HitConfirms.GetValueOrDefault(key);
            HitConfirms[key] = (count + 1, damage + payload.DamageApplied);
        }

        // =================================================================
        // D6d combat telemetry (user direction 2026-10-11; specs/C-meter-telemetry.md
        // §1.4 (a)–(d), package C1). Observation only. Every read of a group-A
        // seam goes through PlaytestWorld's "Hero gates" region.
        // =================================================================

        private readonly List<SwingRecord> _swings = new();
        private SwingRecord _openSwing;
        private readonly FinisherReapproachTracker _finishers = new();
        private readonly AbilityPressTracker _presses = new();
        private readonly MeterTracker _meter = new();
        private readonly MomentumTracker _momentum = new();
        private readonly DefyTracker _defy = new();
        private Action _unsubscribeDefyExecuted;
        /// <summary>The hitbox ID of a swing that first landed since the last Momentum sample (the gain's source).</summary>
        private string _pendingMomentumSource;
        private readonly int[] _lastSerial = new int[3];
        private readonly int[] _serialNow = new int[3];
        private ulong _lastPlayerId;
        private PlayerController _trackedPlayer;
        private Hitbox _meleeHitbox;
        private ulong _meleeHitboxId;
        private int _busBasicConfirms;
        private int _meleeDamagingContacts;
        private int _lastPressSample = -1;
        private Action _unsubscribeLockoutRefused;
        private readonly int[] _lockoutEvents = new int[3];
        private readonly List<int> _ultimateFrames = new();
        /// <summary>The finisher target's EnemyRecord, captured at the connect (a pooled respawn is a new record).</summary>
        private EnemyRecord _finisherTargetRecord;
        private FinisherReapproachTracker.Record _finisherTargetFor;

        /// <summary>Every hero swing, in order (the open one last, with an empty outcome).</summary>
        public IReadOnlyList<SwingRecord> Swings => _swings;
        public FinisherReapproachTracker Finishers => _finishers;
        public AbilityPressTracker Presses => _presses;
        public MeterTracker Meter => _meter;
        public MomentumTracker Momentum => _momentum;
        public DefyTracker Defy => _defy;
        /// <summary>Basic hit confirms the bus carried minus the damaging melee contacts heard; 0 unless a hitbox was missed.</summary>
        public int OrphanBasicHits => _busBasicConfirms - _meleeDamagingContacts;
        /// <summary>Lockout refusals the hero reported per slot (group A's event; always 0 before it lands). C3 reports them.</summary>
        public IReadOnlyList<int> LockoutEvents => _lockoutEvents;

        /// <summary>
        /// Closes everything still open at the end of a run: pending presses
        /// become unresolved, an open finisher record is lost, an open swing is
        /// closed as it stands, and the melee subscription is dropped.
        /// </summary>
        public void FinishPending() {
            _presses.AbandonPending();
            _finishers.AbandonOpen(Frame);
            CloseOpenSwing();
            UnsubscribeMelee();
            UnsubscribeDamage();
            // C3: open mob swings close as they stand (cut), and every mob hitbox is let go.
            CloseOpenMobSwings();
            UnsubscribeMobs();
        }

        public static string KindOf(Node2D body) => body switch {
            EnemyController enemy => enemy.Data?.Tier.ToString().ToLowerInvariant() ?? "standard",
            ArenaGuardian => "guardian",
            _ => "boss"
        };

        private static string FoeIdOf(Node2D body) => body switch {
            EnemyController enemy => enemy.Data?.EnemyID ?? body.Name,
            BossController boss => boss.Data?.BossID ?? body.Name,
            _ => body.Name
        };

        private void EnsureMeleeSubscription(PlayerController player) {
            Hitbox melee = player.GetNodeOrNull<Hitbox>("MeleeHitbox");
            ulong id = melee?.GetInstanceId() ?? 0UL;
            if (id == _meleeHitboxId && id != 0UL) return;
            UnsubscribeMelee();
            if (melee == null) return;
            melee.HitConfirmed += OnMeleeContact;
            _meleeHitbox = melee;
            _meleeHitboxId = id;
        }

        private void UnsubscribeMelee() {
            if (_meleeHitbox != null && GodotObject.IsInstanceValid(_meleeHitbox)) {
                _meleeHitbox.HitConfirmed -= OnMeleeContact;
            }
            _meleeHitbox = null;
            _meleeHitboxId = 0UL;
        }

        private void TrackPlayerInstance(PlayerController player) {
            ulong id = player.GetInstanceId();
            if (id != _lastPlayerId) {
                if (_lastPlayerId != 0UL) {
                    // A rebuilt hero (an Anchor Snap, a Collapse resume): what the
                    // old instance had open cannot be resolved against the new one.
                    _presses.AbandonPending();
                    if (_openSwing != null) {
                        _openSwing.Outcome = "dropped";
                        _openSwing = null;
                    }
                    _finishers.AbandonOpen(Frame);
                }
                _lastPlayerId = id;
                _trackedPlayer = player;
                ReadSerials(player, _lastSerial);
                _meter.Rebaseline(Frame, player.CurrentUltimateMeter);
                _momentum.Rebaseline(PlaytestWorld.MomentumPips(player));
            }
            EnsureMeleeSubscription(player);
            EnsureDamageSubscription(player);
        }

        private void TrackMeter(PlayerController player) =>
            _meter.Sample(Frame, player.CurrentUltimateMeter);

        /// <summary>
        /// The Momentum gauge, read after <see cref="TrackAbilitySerials"/> (this tick's Special
        /// casts are the serial steps) and before <see cref="TrackPresses"/> copies the serials on.
        /// </summary>
        private void TrackMomentum(PlayerController player) {
            _momentum.Sample(PlaytestWorld.MomentumPips(player), PlaytestWorld.MomentumRegenPerTick(player),
                _serialNow[0] > _lastSerial[0], _serialNow[1] > _lastSerial[1], _pendingMomentumSource);
            _pendingMomentumSource = null;
        }

        /// <summary>A Defy blast: the hero's own (index 0), booked on the hit-frame clock.</summary>
        private void OnDefyExecuted(DefyExecutedPayload payload) =>
            _defy.RecordExecution(ObservationFrame(), payload.MeterBefore, payload.TargetsBlasted, payload.Healed);

        private static void ReadSerials(PlayerController player, int[] into) {
            into[0] = player.GetNodeOrNull<BaseSpecial>("Special1")?.ExecutionSerial ?? 0;
            into[1] = player.GetNodeOrNull<BaseSpecial>("Special2")?.ExecutionSerial ?? 0;
            into[2] = player.GetNodeOrNull<BaseSpecial>("Ultimate")?.ExecutionSerial ?? 0;
        }

        /// <summary>The §1.4(a) per-tick table: open, keep (lowering gmin), switch or close the hero's swing.</summary>
        private void TrackSwing(PlayerController player) {
            Hitbox melee = _meleeHitbox != null && GodotObject.IsInstanceValid(_meleeHitbox) ? _meleeHitbox : null;
            if (melee == null || !melee.IsActive) {
                CloseOpenSwing();
                return;
            }
            string hit = melee.HitboxID ?? "";
            if (_openSwing != null && _openSwing.Hit != hit) CloseOpenSwing();
            if (_openSwing == null) {
                OpenSwing(player, melee, Frame, hit, includeOpenRecords: false);
                return;
            }
            Rect2? box = PlaytestWorld.ShapeRect(melee);
            if (!box.HasValue) return;
            FoeGeometry? nearest = NearestFoeGeometry(box.Value, player.GlobalPosition, player.IsFacingRight, includeOpenRecords: false);
            if (nearest.HasValue && (_openSwing.GapMin < 0 || nearest.Value.Gap < _openSwing.GapMin)) {
                _openSwing.GapMin = nearest.Value.Gap;
            }
        }

        private void CloseOpenSwing() {
            if (_openSwing == null) return;
            _openSwing.Close();
            _openSwing = null;
        }

        private void OpenSwing(PlayerController player, Hitbox melee, int frame, string hit, bool includeOpenRecords) {
            var swing = new SwingRecord {
                F = frame,
                Hit = hit ?? "",
                Air = !player.IsOnFloor(),
                Act = _bot?.CurrentAction ?? ""
            };
            Rect2? box = PlaytestWorld.ShapeRect(melee);
            if (box.HasValue) {
                FoeGeometry? nearest = NearestFoeGeometry(box.Value, player.GlobalPosition, player.IsFacingRight, includeOpenRecords);
                if (nearest.HasValue) {
                    FoeGeometry g = nearest.Value;
                    swing.Gap = g.Gap;
                    swing.GapMin = g.Gap;
                    swing.Piv = g.Piv;
                    swing.Foe = g.Foe;
                    swing.Kind = g.Kind;
                    swing.Behind = g.Behind;
                    swing.TargetId = g.TargetId;
                    swing.TargetX = g.TargetX;
                }
            }
            _swings.Add(swing);
            _openSwing = swing;
            _finishers.OnSwingStart(frame, _swings.Count - 1);
        }

        private readonly struct FoeGeometry {
            public readonly int Gap;
            public readonly int Piv;
            public readonly string Foe;
            public readonly string Kind;
            public readonly bool Behind;
            public readonly ulong TargetId;
            public readonly float TargetX;
            public FoeGeometry(int gap, int piv, string foe, string kind, bool behind, ulong targetId, float targetX) {
                Gap = gap; Piv = piv; Foe = foe; Kind = kind; Behind = behind; TargetId = targetId; TargetX = targetX;
            }
        }

        /// <summary>
        /// The foe hurtbox nearest to the hitbox rect <paramref name="box"/>
        /// (ties to the smaller pivot gap). The candidates are the live foes, plus
        /// — for a swing opened inside the contact flush — every body whose
        /// EnemyRecord is still open, because a lethal contact earlier in the same
        /// flush has already zeroed its HP.
        /// </summary>
        private FoeGeometry? NearestFoeGeometry(Rect2 box, Vector2 pivot, bool facingRight, bool includeOpenRecords) {
            List<Node2D> candidates = _world.Foes();
            if (includeOpenRecords) {
                foreach (KeyValuePair<ulong, EnemyRecord> entry in _live) {
                    if (entry.Value.Closed) continue;
                    if (GodotObject.InstanceFromId(entry.Key) is Node2D body && GodotObject.IsInstanceValid(body)
                        && !candidates.Contains(body)) {
                        candidates.Add(body);
                    }
                }
            }
            FoeGeometry? best = null;
            foreach (Node2D foe in candidates) {
                Rect2? hurt = PlaytestWorld.HurtboxRect(foe);
                if (!hurt.HasValue) continue;
                Rect2 r = hurt.Value;
                int gap = (int)MathF.Round(PlaytestCombatMath.AabbGap(
                    box.Position.X, box.Position.Y, box.End.X, box.End.Y,
                    r.Position.X, r.Position.Y, r.End.X, r.End.Y));
                int piv = (int)MathF.Round(PlaytestCombatMath.HorizontalGap(pivot.X, r.Position.X, r.End.X));
                if (best.HasValue && (gap > best.Value.Gap || (gap == best.Value.Gap && piv >= best.Value.Piv))) continue;
                float centreX = r.GetCenter().X;
                bool behind = facingRight ? centreX < pivot.X : centreX > pivot.X;
                best = new FoeGeometry(gap, piv, FoeIdOf(foe), KindOf(foe), behind, foe.GetInstanceId(), foe.GlobalPosition.X);
            }
            return best;
        }

        /// <summary>
        /// The hero's MeleeHitbox delivered a contact (inside the physics flush,
        /// before this frame's Tick: read-only). A first-active-frame contact
        /// arrives before any Tick has seen the box active, so it opens the swing.
        /// </summary>
        private void OnMeleeContact(HitPayload payload, float dealt) {
            PlayerController player = _trackedPlayer != null && GodotObject.IsInstanceValid(_trackedPlayer)
                ? _trackedPlayer
                : _world?.Player;
            if (dealt > 0f) _meleeDamagingContacts++;
            if (player == null || _meleeHitbox == null || !GodotObject.IsInstanceValid(_meleeHitbox)) return;
            string hit = payload.HitboxID ?? "";
            if (_openSwing == null || _openSwing.Hit != hit) {
                CloseOpenSwing();
                OpenSwing(player, _meleeHitbox, Frame + 1, hit, includeOpenRecords: true);
            }
            SwingRecord swing = _openSwing;
            swing.Contacts++;
            if (!PlaytestWorld.IsLandedContact(in payload, dealt)) {
                swing.NoLanding++;
                return;
            }
            bool firstLanded = swing.Landed == 0;
            // Momentum: the gain is paid on a swing's first landed contact, so that tick's rise is this hit's.
            if (firstLanded) _pendingMomentumSource = hit;
            swing.Landed++;
            swing.Damage += dealt;
            if (firstLanded && hit == "combo_3" && swing.TargetId != 0UL) {
                // Since GU-A (2026-10-12) the string is grounded only, so a finisher
                // record never opens in the air (batches from before it can carry
                // aerial combo_3 records; compare finishers with that in mind).
                // The knockback is not integrated until the target's own update, so
                // its position now is its position at the connect.
                float targetX = GodotObject.InstanceFromId(swing.TargetId) is Node2D target && GodotObject.IsInstanceValid(target)
                    ? target.GlobalPosition.X
                    : swing.TargetX;
                _finishers.OnFinisherConnect(Frame + 1, swing.TargetId, swing.Kind, swing.Air, targetX, swing.Foe);
                _finisherTargetFor = _finishers.Open;
                _finisherTargetRecord = _live.TryGetValue(swing.TargetId, out EnemyRecord record) && !record.Closed ? record : null;
            }
        }

        private void TrackAbilitySerials(PlayerController player) {
            ReadSerials(player, _serialNow);
            if (_serialNow[0] > _lastSerial[0] || _serialNow[1] > _lastSerial[1]) _finishers.OnAbilityAccepted(Frame, ultimate: false);
            if (_serialNow[2] > _lastSerial[2]) _finishers.OnAbilityAccepted(Frame, ultimate: true);
        }

        /// <summary>
        /// Registers this tick's bot presses with the serials read now (after the
        /// hero's previous update, before the update that consumes them), then
        /// resolves the earlier ones. Reads <see cref="_serialNow"/>, filled by
        /// <see cref="TrackAbilitySerials"/> this tick. C3: each press carries the
        /// hero's pre-update <see cref="PressSnapshot"/>, so a refusal books every
        /// readable condition that held.
        /// </summary>
        private void TrackPresses(PlayerController player) {
            if (_bot != null && _bot.SampleCount != _lastPressSample) {
                _lastPressSample = _bot.SampleCount;
                GameplayButtons pressed = _bot.LastPressed;
                if ((pressed & GameplayButtons.Defy) != 0) {
                    _defy.RecordPress(PlaytestWorld.CanDefy(player), player.CurrentUltimateMeter);
                }
                if ((pressed & GameplayButtons.Special1) != 0) {
                    _presses.Register(Frame, PressSlot.Special1, _serialNow[0], SnapshotFor(player, PressSlot.Special1));
                }
                if ((pressed & GameplayButtons.Special2) != 0) {
                    _presses.Register(Frame, PressSlot.Special2, _serialNow[1], SnapshotFor(player, PressSlot.Special2));
                }
                if ((pressed & GameplayButtons.Ultimate) != 0) {
                    _presses.Register(Frame, PressSlot.Ultimate, _serialNow[2], SnapshotFor(player, PressSlot.Ultimate));
                }
            }
            _presses.Resolve(Frame, _serialNow[0], _serialNow[1], _serialNow[2]);
            Array.Copy(_serialNow, _lastSerial, _lastSerial.Length);
        }

        private static string SlotNodeName(PressSlot slot) => slot switch {
            PressSlot.Special1 => "Special1",
            PressSlot.Special2 => "Special2",
            _ => "Ultimate"
        };

        /// <summary>
        /// The hero's directly readable state for a press of <paramref name="slot"/>
        /// (spec C §1.4(d), C3). Read on the registering tick, before the hero's
        /// update consumes the press. The Ultimate reads the ability lockout, never
        /// the Special gate (D1: no hit-confirm for the Ultimate).
        /// </summary>
        internal static PressSnapshot SnapshotFor(PlayerController player, PressSlot slot) {
            bool ultimate = slot == PressSlot.Ultimate;
            AbilitySlot abilitySlot = slot switch {
                PressSlot.Special1 => AbilitySlot.Special1,
                PressSlot.Special2 => AbilitySlot.Special2,
                _ => AbilitySlot.Ultimate
            };
            return new PressSnapshot {
                Slot = slot,
                HitstopFrames = player.HitstopFramesRemaining,
                State = player.CurrentState.ToString(),
                TimeFrozen = player.TimeFrozen,
                Suppressed = player.HasStatusEffect(StatusType.Suppression),
                GateFrames = ultimate ? player.AbilityLockoutFramesRemaining : PlaytestWorld.SpecialGateFrames(player),
                SlotUnlocked = player.IsAbilityUnlocked(abilitySlot),
                ReuseSeconds = slot switch {
                    PressSlot.Special1 => PlaytestWorld.SpecialReuseRemaining(player, AbilitySlot.Special1),
                    PressSlot.Special2 => PlaytestWorld.SpecialReuseRemaining(player, AbilitySlot.Special2),
                    _ => 0f
                },
                MomentumPips = PlaytestWorld.MomentumPips(player),
                MeterFull = player.CurrentUltimateMeter >= InfluenceMeterRules.MaxValue,
                AbilityExecuting = player.GetNodeOrNull<BaseSpecial>(SlotNodeName(slot))?.IsExecuting ?? false
            };
        }

        // =================================================================
        // C3: mob melee swings (spec C §1.4(e)). EnemyController bodies only,
        // MeleeStrike and ChargeDash active windows only; bosses are excluded.
        // =================================================================

        private sealed class MobHitboxSubscription {
            public Hitbox Hitbox;
            public Action<HitPayload, float> Handler;
        }

        private sealed class OpenMobSwing {
            public MobSwingRecord Record;
            public EnemyAbilityData Ability;
        }

        private readonly List<MobSwingRecord> _mobSwings = new();
        private readonly Dictionary<ulong, OpenMobSwing> _openMobSwings = new();
        private readonly Dictionary<ulong, MobHitboxSubscription> _mobHitboxes = new();
        private readonly HashSet<ulong> _mobsTrackedThisTick = new();

        /// <summary>Every mob melee swing, in open order (open ones have an empty outcome).</summary>
        public IReadOnlyList<MobSwingRecord> MobSwings => _mobSwings;

        private static bool IsMobMelee(EnemyAbilityData ability) =>
            ability != null && ability.Archetype is EnemyAbilityArchetype.MeleeStrike or EnemyAbilityArchetype.ChargeDash;

        /// <summary>Subscribes once per enemy instance to its attack hitbox's contacts (the lookup EnemyController uses).</summary>
        private void EnsureMobSubscription(EnemyController enemy) {
            ulong id = enemy.GetInstanceId();
            if (_mobHitboxes.ContainsKey(id)) return;
            Hitbox hitbox = enemy.GetNodeOrNull<Hitbox>("Hitbox") ?? enemy.GetNodeOrNull<Hitbox>("AttackHitbox");
            if (hitbox == null) return;
            Action<HitPayload, float> handler = (payload, dealt) => OnMobContact(id, payload, dealt);
            hitbox.HitConfirmed += handler;
            _mobHitboxes[id] = new MobHitboxSubscription { Hitbox = hitbox, Handler = handler };
        }

        private void UnsubscribeMobs() {
            foreach (MobHitboxSubscription subscription in _mobHitboxes.Values) {
                if (subscription.Hitbox != null && GodotObject.IsInstanceValid(subscription.Hitbox)) {
                    subscription.Hitbox.HitConfirmed -= subscription.Handler;
                }
            }
            _mobHitboxes.Clear();
        }

        /// <summary>
        /// A mob's attack hitbox delivered a contact (inside the physics flush,
        /// before this frame's Tick: read-only). Only a contact on the hero's
        /// hurtbox (<c>TargetIndex == 0</c>, a block included) counts. A
        /// first-active-frame contact arrives before any Tick has seen the phase,
        /// so it opens the swing at <c>Frame + 1</c>.
        /// </summary>
        private void OnMobContact(ulong enemyId, HitPayload payload, float dealt) {
            if (payload.TargetIndex != 0) return;
            if (_openMobSwings.TryGetValue(enemyId, out OpenMobSwing open)) {
                open.Record.Contacts++;
                return;
            }
            if (GodotObject.InstanceFromId(enemyId) is not EnemyController enemy || !GodotObject.IsInstanceValid(enemy)) return;
            EnemyAbilityData ability = enemy.ActiveAbility;
            if (enemy.AbilityPhase != EnemyAbilityPhase.Active || !IsMobMelee(ability)) return;
            PlayerController player = _trackedPlayer != null && GodotObject.IsInstanceValid(_trackedPlayer)
                ? _trackedPlayer
                : _world?.Player;
            OpenMobSwing opened = OpenMobSwingFor(enemy, ability, Frame + 1, player);
            opened.Record.Contacts++;
        }

        /// <summary>The §1.4(e) per-tick rule for one mob: open, keep, or close its melee swing.</summary>
        private void TrackMobSwing(EnemyController enemy, PlayerController player) {
            ulong id = enemy.GetInstanceId();
            _mobsTrackedThisTick.Add(id);
            EnemyAbilityData ability = enemy.ActiveAbility;
            EnemyAbilityPhase phase = enemy.AbilityPhase;
            bool activeMelee = phase == EnemyAbilityPhase.Active && IsMobMelee(ability);
            if (_openMobSwings.TryGetValue(id, out OpenMobSwing open)) {
                if (activeMelee && ReferenceEquals(open.Ability, ability)) return;
                open.Record.Close(natural: phase == EnemyAbilityPhase.Recovery);
                _openMobSwings.Remove(id);
            }
            if (activeMelee) OpenMobSwingFor(enemy, ability, Frame, player);
        }

        private OpenMobSwing OpenMobSwingFor(EnemyController enemy, EnemyAbilityData ability, int frame, PlayerController player) {
            ulong id = enemy.GetInstanceId();
            var record = new MobSwingRecord {
                F = frame,
                Foe = FoeIdOf(enemy),
                Kind = KindOf(enemy),
                Ability = string.IsNullOrEmpty(ability.AbilityID) ? ability.ResourceName ?? "" : ability.AbilityID
            };
            Hitbox hitbox = _mobHitboxes.TryGetValue(id, out MobHitboxSubscription subscription)
                && subscription.Hitbox != null && GodotObject.IsInstanceValid(subscription.Hitbox)
                ? subscription.Hitbox
                : null;
            Rect2? box = hitbox != null ? PlaytestWorld.ShapeRect(hitbox) : null;
            Rect2? hurt = player != null ? PlaytestWorld.HurtboxRect(player) : null;
            if (box.HasValue && hurt.HasValue) {
                Rect2 a = box.Value;
                Rect2 b = hurt.Value;
                record.Gap = (int)MathF.Round(PlaytestCombatMath.AabbGap(
                    a.Position.X, a.Position.Y, a.End.X, a.End.Y,
                    b.Position.X, b.Position.Y, b.End.X, b.End.Y));
            }
            _mobSwings.Add(record);
            var open = new OpenMobSwing { Record = record, Ability = ability };
            _openMobSwings[id] = open;
            return open;
        }

        /// <summary>Open mob swings whose enemy this tick's loop did not reach (dead, despawned, out of the group) are cut.</summary>
        private void CloseUntrackedMobSwings() {
            if (_openMobSwings.Count > 0) {
                var gone = new List<ulong>();
                foreach (ulong id in _openMobSwings.Keys) {
                    if (!_mobsTrackedThisTick.Contains(id)) gone.Add(id);
                }
                foreach (ulong id in gone) {
                    _openMobSwings[id].Record.Close(natural: false);
                    _openMobSwings.Remove(id);
                }
            }
            _mobsTrackedThisTick.Clear();
        }

        /// <summary>The end of the run: every open mob swing closes as it stands, as a cut.</summary>
        private void CloseOpenMobSwings() {
            foreach (OpenMobSwing open in _openMobSwings.Values) open.Record.Close(natural: false);
            _openMobSwings.Clear();
        }

        // =================================================================
        // CM-BOT (decision 7.2, 2026-10-16; plan dossier 2.7.2): the re-hit
        // counter that measures whether Story needs post-hit mercy
        // invulnerability. Observation only: the hero raises DamageTaken after
        // every positive HP loss (read through PlaytestWorld's "Hero gates"
        // seam), and this records it. damage_by_source and the per-enemy
        // hits_on_player keep their nearest-enemy attribution unchanged, so
        // batches from before and after compare on them.
        // =================================================================

        private readonly RehitTracker _rehits = new();
        private Action _unsubscribeDamage;
        private ulong _damageSubscriptionId;
        private ulong _tickedPhysicsFrame = ulong.MaxValue;

        public RehitTracker Rehits => _rehits;

        /// <summary>
        /// HP-loss events the bus carried for the hero minus the observations
        /// heard: 0 unless a damage path bypasses the three raise sites. A
        /// non-hit death (<c>KillPlayerNonHit</c>, a lethal pit) is the one
        /// expected cause; no campaign level authors one today.
        /// </summary>
        public int UnobservedHeroHits => PlayerHitsTaken - (_rehits.Hits + _rehits.Ticks);

        private void EnsureDamageSubscription(PlayerController player) {
            ulong id = player?.GetInstanceId() ?? 0UL;
            if (id == _damageSubscriptionId && id != 0UL) return;
            UnsubscribeDamage();
            if (player == null) return;
            _unsubscribeDamage = PlaytestWorld.SubscribeDamageTaken(player, OnHeroDamageTaken);
            _damageSubscriptionId = id;
        }

        private void UnsubscribeDamage() {
            _unsubscribeDamage?.Invoke();
            _unsubscribeDamage = null;
            _damageSubscriptionId = 0UL;
        }

        /// <summary>
        /// The telemetry frame an observation belongs to. A physics frame runs the
        /// flush (hurtbox contacts) before the physics callbacks (this Tick, the
        /// hero's status ticks, hazard updates), so an observation made before this
        /// physics frame's Tick belongs to the frame that Tick is about to open
        /// (the melee-contact <c>Frame + 1</c> convention), and one made after it
        /// to the current Frame. Either way every observation in one physics frame
        /// gets one frame number.
        /// </summary>
        private int ObservationFrame() => Engine.GetPhysicsFrames() == _tickedPhysicsFrame ? Frame : Frame + 1;

        /// <summary>
        /// Records one hero HP loss (may run inside the physics flush: read-only).
        /// A <see cref="HitDelivery.Tick"/> observation — a status tick, a zone
        /// pulse, a periodic environmental tick (CM-FIXA-PRES) — is booked as a
        /// periodic tick, apart from the hits and out of every re-hit figure.
        /// </summary>
        private void OnHeroDamageTaken(PlayerDamageObservation observation) {
            (string source, string label, string kind) = RehitSourceOf(in observation);
            _rehits.Record(ObservationFrame(), source, kind, observation.Damage, label,
                tick: observation.Delivery == HitDelivery.Tick);
            if (observation.Delivery != HitDelivery.Tick) _defy.RecordHit(ObservationFrame());
        }

        /// <summary>
        /// The source key (one per owning body or hazard), a readable label and
        /// the <see cref="RehitTracker"/> kind of one observation. A hit's owner is
        /// its payload's <c>SourceActorId</c>: a mob's or boss's attack hitbox, the
        /// shooter of a pooled shot, the Mirror clone's own hitboxes. A shot by a
        /// standard or elite mob is <c>projectile</c>; a boss's or the clone's shot
        /// stays <c>boss</c> / <c>mirror</c>, so a rule that exempts bosses and the
        /// Mirror can be read off one partition. A hazard is keyed by the hot
        /// hazard the hero stands in (<c>env</c> when none is recognised); every
        /// status tick shares one key and is never part of a re-hit figure.
        /// </summary>
        private (string Source, string Label, string Kind) RehitSourceOf(in PlayerDamageObservation o) {
            if (o.Kind == DamageObservationKind.Status) return ("status", "status", RehitTracker.KindStatus);
            if (o.Kind == DamageObservationKind.Hazard) {
                PlayerController hero = _trackedPlayer != null && GodotObject.IsInstanceValid(_trackedPlayer)
                    ? _trackedPlayer
                    : _world?.Player;
                string hazard = (hero != null && _world != null ? HazardSourceAt(hero) : null) ?? "env";
                return ("hazard:" + hazard, hazard, RehitTracker.KindHazard);
            }
            bool shot = o.HitboxID == "projectile";
            string attack = string.IsNullOrEmpty(o.AttackID) ? o.HitboxID : o.AttackID;
            if (o.SourceActorId != 0UL) {
                string key = "actor:" + o.SourceActorId;
                GodotObject owner = GodotObject.InstanceFromId(o.SourceActorId);
                if (owner != null && GodotObject.IsInstanceValid(owner)) {
                    switch (owner) {
                        case EnemyController enemy:
                            return (key, FoeIdOf(enemy), shot ? RehitTracker.KindProjectile
                                : enemy.Data?.Tier == EnemyTier.Elite ? RehitTracker.KindElite : RehitTracker.KindStandard);
                        case BossController boss:
                            return (key, FoeIdOf(boss), RehitTracker.KindBoss);
                        case PlayerController clone when clone.IsInGroup(MirrorParadoxController.MirrorGroup):
                            return (key, "mirror", RehitTracker.KindMirror);
                    }
                }
                // An owner freed or recycled since it fired (a mob's shot outliving it).
                return (key, attack, shot ? RehitTracker.KindProjectile : RehitTracker.KindOther);
            }
            // No owner on the payload. The Jackal Priest's decoy bursts name their
            // boss only in the attack ID ("boss.<id>.decoy_burst").
            string kindOfUnowned = attack.StartsWith("boss.", StringComparison.Ordinal) ? RehitTracker.KindBoss
                : shot ? RehitTracker.KindProjectile : RehitTracker.KindOther;
            return ("attack:" + attack, attack, kindOfUnowned);
        }

        private static Dictionary<string, object> RehitKindRow(RehitTracker.KindCounts c) => new() {
            ["hits"] = c.Hits,
            ["damage"] = c.Damage,
            ["xsrc_30"] = c.Cross30,
            ["xsrc_20"] = c.Cross20,
            ["same_30"] = c.Same30,
            ["same_20"] = c.Same20,
            ["xsrc_damage_30"] = c.CrossDamage30,
            ["xsrc_30_combat"] = c.Cross30Combat
        };

        /// <summary>
        /// The <c>rehits</c> report block (CM-BOT, decision 7.2; amended by
        /// CM-FIXA-PRES). <c>hits</c> and every <c>xsrc_*</c> / <c>same_*</c>
        /// figure count hits only: a periodic tick (a status tick, a zone pulse,
        /// a drown tick, a stage-hazard DPS payout — any
        /// <see cref="HitDelivery.Tick"/> observation) is neither a re-hit nor a
        /// predecessor, and is counted apart in <c>ticks</c>, <c>tick_damage</c>
        /// and <c>ticks_by_kind</c> (<c>status_ticks</c> is its Venom subset).
        /// <c>by_kind</c> holds hits only and books a re-hit under the kind of
        /// the <i>second</i> hit, so it cannot remove a hazard predecessor; the
        /// <c>*_combat</c> figures leave hazard contacts out on both sides (the
        /// mob, boss and Mirror re-hits alone, over <c>combat_hits</c>). The log
        /// lists hits only, each with its unique source <c>key</c> (<c>src</c> is
        /// the readable label, shared by two mobs of one type) and its combat
        /// verdict <c>xc</c>.
        /// </summary>
        private Dictionary<string, object> RehitsReport() {
            RehitTracker r = _rehits;
            return new Dictionary<string, object> {
                ["window_frames"] = RehitTracker.WindowFrames,
                ["short_window_frames"] = RehitTracker.ShortWindowFrames,
                ["hits"] = r.Hits,
                ["hit_damage"] = r.HitDamage,
                ["status_ticks"] = r.StatusTicks,
                ["ticks"] = r.Ticks,
                ["tick_damage"] = r.TickDamage,
                ["xsrc_30"] = r.CrossSource30,
                ["xsrc_20"] = r.CrossSource20,
                ["same_30"] = r.SameSource30,
                ["same_20"] = r.SameSource20,
                ["xsrc_same_frame"] = r.CrossSourceSameFrame,
                ["xsrc_damage_30"] = r.CrossSourceDamage30,
                ["xsrc_pct"] = r.Hits > 0 ? Math.Round(100.0 * r.CrossSource30 / r.Hits, 1) : -1.0,
                ["combat_hits"] = r.CombatHits,
                ["xsrc_30_combat"] = r.CrossSource30Combat,
                ["xsrc_20_combat"] = r.CrossSource20Combat,
                ["xsrc_damage_30_combat"] = r.CrossSourceDamage30Combat,
                ["xsrc_combat_pct"] = r.CombatHits > 0 ? Math.Round(100.0 * r.CrossSource30Combat / r.CombatHits, 1) : -1.0,
                ["unobserved_hits"] = UnobservedHeroHits,
                ["xsrc_gap_histogram"] = r.CrossGapHistogram.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value),
                ["by_kind"] = r.ByKind.ToDictionary(kv => kv.Key, kv => (object)RehitKindRow(kv.Value)),
                ["ticks_by_kind"] = r.TicksByKind.ToDictionary(kv => kv.Key, kv => (object)new Dictionary<string, object> {
                    ["ticks"] = kv.Value.Ticks,
                    ["damage"] = kv.Value.Damage
                }),
                ["log"] = r.Log.Select(e => (object)new Dictionary<string, object> {
                    ["f"] = e.Frame,
                    ["kind"] = e.Kind,
                    ["src"] = e.Label,
                    ["key"] = e.Source,
                    ["dmg"] = e.Damage,
                    ["x"] = e.Cross30,
                    ["x20"] = e.Cross20,
                    ["xc"] = e.Cross30Combat,
                    ["s"] = e.Same30,
                    ["sf"] = e.CrossSameFrame,
                    ["xgap"] = e.CrossGap,
                    ["xk"] = e.CrossKind
                }).ToList()
            };
        }

        // =================================================================
        // GU-BOT (gameplay update 2026-10-12, plan §5): the hero's crowd under
        // the engagement director (mob_engagement) and the encounter locks
        // (locks). Observation only; every read of a GU-E / GU-F member goes
        // through PlaytestWorld's "Encounter locks and the engagement director"
        // region.
        // =================================================================

        private readonly MobEngagementTracker _engagement = new();
        private readonly LockTracker _locks = new();
        private EngagementFrameSample _engagementFrame;
        private EngagementFrameSample _lastEngagementFrame;
        private readonly List<(float Gap, int Side)> _waitersThisFrame = new();
        private readonly Dictionary<string, string> _lockStates = new();

        public MobEngagementTracker Engagement => _engagement;
        public LockTracker Locks => _locks;

        private void BeginEngagementFrame() {
            _engagementFrame = default;
            _waitersThisFrame.Clear();
        }

        /// <summary>One living mob's part in this frame's crowd sample (see <see cref="EngagementFrameSample"/>).</summary>
        private void ObserveEngagement(EnemyController mob, PlayerController hero, Vector2 heroPosition) {
            if (hero == null) return;
            bool onHero = PlaytestWorld.EngagesHero(mob, hero);
            StoryEngagementRules.EngagementRole role = onHero
                ? PlaytestWorld.EngagementRoleOf(mob)
                : StoryEngagementRules.EngagementRole.None;
            _engagement.NoteRole(mob.GetInstanceId(), PlaytestWorld.SpawnGenerationOf(mob), role);
            bool attacking = mob.CurrentState == EnemyState.Attacking;
            StoryEngagementRules.EngagementClass inFlight = attacking
                ? PlaytestWorld.InFlightClassOf(mob)
                : StoryEngagementRules.EngagementClass.Free;
            if (onHero) _engagementFrame.Engaged++;
            if (attacking && mob.GlobalPosition.DistanceTo(heroPosition) <= MobEngagementTracker.NearPixels) {
                _engagementFrame.AttackingNear++;
                if (inFlight == StoryEngagementRules.EngagementClass.MeleeSlot) {
                    _engagementFrame.MeleeAttackingNear++;
                    // FIX-ENEMY: a melee attacker with no slot is retaliating (never gated).
                    if (role != StoryEngagementRules.EngagementRole.Slot) _engagementFrame.MeleeRetaliatingNear++;
                }
            }
            if (role == StoryEngagementRules.EngagementRole.Slot) _engagementFrame.Holders++;
            if (inFlight == StoryEngagementRules.EngagementClass.RangedPermit) _engagementFrame.RangedInFlight++;
            if (onHero && PlaytestWorld.IsRingingWaiter(mob)) {
                float gap = PlaytestWorld.EngagementGapOf(mob);
                _waitersThisFrame.Add((gap, PlaytestWorld.RingSideOf(mob)));
                if (gap > MobEngagementTracker.FarWaiterGapPixels) {
                    _engagement.NoteFarWaiter(Frame, FoeIdOf(mob), mob.GlobalPosition.X, mob.GlobalPosition.Y, gap);
                }
            }
        }

        /// <summary>Books the frame's crowd sample and its waiter gaps (the nearest per side is the front one).</summary>
        private void FinishEngagementFrame() {
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            _engagement.SlotTotal = StoryEngagementRules.MeleeSlotsTotal(difficulty);
            _engagement.PermitTotal = StoryEngagementRules.RangedPermits(difficulty);
            _engagement.SampleFrame(_engagementFrame);
            _lastEngagementFrame = _engagementFrame;
            float frontLeft = float.MaxValue, frontRight = float.MaxValue;
            foreach ((float gap, int side) in _waitersThisFrame) {
                if (side < 0) frontLeft = MathF.Min(frontLeft, gap);
                else frontRight = MathF.Min(frontRight, gap);
            }
            bool leftTaken = false, rightTaken = false;
            foreach ((float gap, int side) in _waitersThisFrame) {
                bool front = false;
                if (side < 0 && !leftTaken && gap == frontLeft) front = leftTaken = true;
                else if (side >= 0 && !rightTaken && gap == frontRight) front = rightTaken = true;
                _engagement.NoteWaiterGap(gap, front);
            }
        }

        /// <summary>Reads every encounter lock: its state edges, counters, members and the hero's leaves.</summary>
        private void TrackLocks(PlayerController player) {
            if (_world == null) return;
            bool heroAlive = player != null && GodotObject.IsInstanceValid(player) && player.CurrentHP > 0
                && player.CurrentState != CharacterState.Dead;
            foreach (EncounterLock encounterLock in _world.EncounterLocks()) {
                var observation = new LockObservation {
                    LockID = encounterLock.LockID,
                    InstanceId = encounterLock.GetInstanceId(),
                    State = encounterLock.State,
                    Planned = encounterLock.PlannedTotal,
                    Registered = encounterLock.RegisteredCount,
                    Living = encounterLock.LivingMemberCount,
                    Remaining = encounterLock.RemainingCount,
                    Engages = encounterLock.EngageCount,
                    Reverts = encounterLock.RevertCount,
                    StallRelocations = encounterLock.StallRelocations,
                    Recycled = encounterLock.RecycledMembers,
                    ForceCleared = encounterLock.ForceCleared,
                    RestoredCleared = encounterLock.RestoredCleared,
                    EngagedSeconds = encounterLock.EngagedSeconds,
                    HeroInside = heroAlive && encounterLock.HeroStandsInside,
                    HeroAlive = heroAlive
                };
                LockRecord before = _locks.Current(observation.LockID);
                int leftBefore = before?.LeftWithMembersAlive ?? 0;
                LockRecord record = _locks.Observe(Frame, observation);
                // A lock a scene reload rebuilt is its own record (FIX-BOT): its
                // events carry the instance (#2, #3, ...).
                if (record != before) leftBefore = 0;
                string label = record.Instance > 1 ? $"{record.LockID}#{record.Instance}" : record.LockID;
                string stateName = record.State;
                if (!_lockStates.TryGetValue(label, out string previous) || previous != stateName) {
                    _lockStates[label] = stateName;
                    AddEvent("lock", $"{label}:{stateName} living={observation.Living} remaining={observation.Remaining}"
                        + (encounterLock.RestoredCleared ? " (restored)" : encounterLock.ForceCleared ? " (forced)" : ""));
                }
                if (record.LeftWithMembersAlive > leftBefore) {
                    AddEvent("lock_left_alive", $"{label} living={observation.Living}");
                }
                foreach (EnemyController member in encounterLock.LivingMembers) {
                    if (member == null || !GodotObject.IsInstanceValid(member)) continue;
                    ulong id = member.GetInstanceId();
                    EnemyRecord enemy = _live.TryGetValue(id, out EnemyRecord live) && !live.Closed ? live : null;
                    if (enemy != null && enemy.LockID.Length == 0) enemy.LockID = record.LockID;
                    (int relocations, int falls) = PlaytestWorld.MemberRelocations(member);
                    _locks.NoteMember(record, id, PlaytestWorld.SpawnGenerationOf(member), enemy, relocations, falls);
                }
            }
        }

        /// <summary>The trace's lock cell: the engaged lock's ID tail and what is left in it.</summary>
        private string EngagedLockTraceCell() {
            EncounterLock engaged = _world?.EngagedLock();
            if (engaged == null) return "";
            string id = engaged.LockID ?? "";
            int cut = id.IndexOf("_lock_", StringComparison.Ordinal);
            string shortId = cut >= 0 ? id[(cut + 6)..] : id;
            return $"{shortId}:{engaged.RemainingCount}";
        }

        private Dictionary<string, object> MobEngagementReport() {
            MobEngagementTracker e = _engagement;
            return new Dictionary<string, object> {
                ["near_px"] = (int)MobEngagementTracker.NearPixels,
                ["slot_total"] = e.SlotTotal,
                ["ranged_permits"] = e.PermitTotal,
                ["engaged_frames"] = e.EngagedFrames,
                ["attacking_near_max"] = e.MaxAttackingNear,
                ["attacking_near_mean"] = e.MeanOverEngaged(e.SumAttackingNear),
                ["melee_attacking_near_max"] = e.MaxMeleeAttackingNear,
                ["melee_attacking_near_mean"] = e.MeanOverEngaged(e.SumMeleeAttackingNear),
                ["frames_melee_over_slots"] = e.FramesMeleeOverSlots,
                ["melee_retaliating_near_max"] = e.MaxMeleeRetaliatingNear,
                ["frames_gated_melee_over_slots"] = e.FramesGatedMeleeOverSlots,
                ["slot_holders_max"] = e.MaxHolders,
                ["slot_holders_mean"] = e.MeanOverEngaged(e.SumHolders),
                ["frames_holders_over_slots"] = e.FramesHoldersOverSlots,
                ["slot_grants"] = e.SlotGrants,
                ["slot_handoffs"] = e.SlotHandoffs,
                ["waiter_gap_px_median"] = e.WaiterGapMedian,
                ["front_waiter_gap_px_median"] = e.FrontWaiterGapMedian,
                ["waiter_gap_samples"] = e.WaiterGapSamples,
                ["gap_bucket_px"] = MobEngagementTracker.GapBucketPixels,
                ["waiter_gap_histogram"] = e.WaiterGapBuckets.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value),
                ["front_waiter_gap_histogram"] = e.FrontWaiterGapBuckets.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value),
                ["ranged_in_flight_max"] = e.MaxRangedInFlight,
                ["ranged_in_flight_mean"] = e.MeanOverEngaged(e.SumRangedInFlight),
                ["frames_ranged_over_permits"] = e.FramesRangedOverPermits,
                // Ringing waiters more than 600 px from the hero (off their ring:
                // still walking in across a wide area, or held off it by a ledge,
                // wall or barrier): every sample counted, a few logged with where
                // they stood (a gap that never shrinks is a stuck waiter).
                ["far_waiter_samples"] = e.FarWaiterSamples,
                ["far_waiters"] = e.FarWaiters.Select(w => (object)new Dictionary<string, object> {
                    ["f"] = w.Frame, ["foe"] = w.Enemy, ["x"] = w.X, ["y"] = w.Y, ["gap"] = w.Gap
                }).ToList(),
                ["attacking_near_histogram"] = e.AttackingNearHistogram.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value),
                // Raw sums over the engaged frames, so batches pool exactly (compare_runs.py).
                ["sums"] = new Dictionary<string, object> {
                    ["attacking_near"] = e.SumAttackingNear,
                    ["melee_attacking_near"] = e.SumMeleeAttackingNear,
                    ["slot_holders"] = e.SumHolders,
                    ["ranged_in_flight"] = e.SumRangedInFlight
                }
            };
        }

        private Dictionary<string, object> LocksReport() {
            _locks.Summarize();
            List<LockRecord> records = _locks.Records;
            // FIX-BOT (2026-10-13): one log row per lock instance; count, engaged
            // and cleared are distinct lock IDs, reloads the instances a scene
            // reload rebuilt, and planned / killed sum per instance, so a lock
            // fought twice across an Anchor Snap never reads killed > planned.
            return new Dictionary<string, object> {
                ["count"] = _locks.LockCount,
                ["instances"] = records.Count,
                ["reloads"] = _locks.Reloads,
                ["engaged"] = _locks.EngagedLocks,
                ["cleared"] = _locks.ClearedLocks,
                ["restored_cleared"] = records.Count(r => r.RestoredCleared),
                ["force_cleared"] = records.Count(r => r.ForceCleared),
                ["reverts"] = records.Sum(r => r.Reverts),
                ["left_with_members_alive"] = _locks.LeftWithMembersAlive,
                ["members_planned"] = records.Sum(r => r.Planned),
                ["members_killed"] = records.Sum(r => r.Killed),
                ["stall_relocations"] = records.Sum(r => r.StallRelocations),
                ["member_relocations"] = records.Sum(r => r.MemberRelocations),
                ["fall_recoveries"] = records.Sum(r => r.FallRecoveries),
                ["frames_to_clear_median"] = PlaytestCombatMath.Median(records.Where(r => r.FramesToClear >= 0).Select(r => r.FramesToClear).ToList()),
                ["log"] = records.Select(r => (object)new Dictionary<string, object> {
                    ["id"] = r.LockID,
                    ["instance"] = r.Instance,
                    ["state"] = r.State,
                    ["engage_frame"] = r.EngageFrame,
                    ["clear_frame"] = r.ClearFrame,
                    ["frames_to_clear"] = r.FramesToClear,
                    ["engaged_seconds"] = Math.Round(r.EngagedSeconds, 1),
                    ["planned"] = r.Planned,
                    ["registered"] = r.Registered,
                    ["killed"] = r.Killed,
                    ["living"] = r.Living,
                    ["max_living"] = r.MaxLiving,
                    ["engages"] = r.Engages,
                    ["reverts"] = r.Reverts,
                    ["left_with_members_alive"] = r.LeftWithMembersAlive,
                    ["left_frames"] = Boxed(r.LeftFrames),
                    ["stall_relocations"] = r.StallRelocations,
                    ["member_relocations"] = r.MemberRelocations,
                    ["fall_recoveries"] = r.FallRecoveries,
                    ["recycled"] = r.Recycled,
                    ["force_cleared"] = r.ForceCleared,
                    ["restored_cleared"] = r.RestoredCleared
                }).ToList()
            };
        }

        /// <summary>The <c>PLAYTEST_RESULT</c> lock and crowd summary (plan §5 GU-BOT).</summary>
        public Dictionary<string, object> EncounterSummary() {
            _locks.Summarize();
            List<LockRecord> records = _locks.Records;
            MobEngagementTracker e = _engagement;
            return new Dictionary<string, object> {
                ["locks"] = _locks.LockCount,
                ["locks_cleared"] = _locks.ClearedLocks,
                ["lock_reloads"] = _locks.Reloads,
                ["lock_reverts"] = records.Sum(r => r.Reverts),
                ["left_lock_alive"] = _locks.LeftWithMembersAlive,
                ["lock_relocations"] = records.Sum(r => r.MemberRelocations),
                ["locks_forced"] = records.Count(r => r.ForceCleared),
                ["atk_max"] = e.MaxAttackingNear,
                ["atk_mean"] = e.MeanOverEngaged(e.SumAttackingNear),
                ["melee_max"] = e.MaxMeleeAttackingNear,
                ["melee_over_slots_f"] = e.FramesMeleeOverSlots,
                ["gated_over_slots_f"] = e.FramesGatedMeleeOverSlots,
                ["handoffs"] = e.SlotHandoffs,
                ["waiter_gap_med"] = e.WaiterGapMedian,
                ["ranged_max"] = e.MaxRangedInFlight,
                ["slot_total"] = e.SlotTotal
            };
        }

        /// <summary>
        /// The carry-sampled finisher target's HP at the start of this tick: the
        /// baseline while its record is open, and after a swing close the test
        /// for a later hit that voids the carry (<see cref="FinisherReapproachTracker.NoteTargetHealth"/>).
        /// </summary>
        private void NoteFinisherTargetHealth() {
            FinisherReapproachTracker.Record record = _finishers.Sampled;
            if (record == null) return;
            if (GodotObject.InstanceFromId(record.TargetId) is Node2D body && GodotObject.IsInstanceValid(body)) {
                _finishers.NoteTargetHealth(PlaytestWorld.HealthOf(body));
            }
        }

        private void SampleFinisherTarget(PlayerController player) {
            FinisherReapproachTracker.Record record = _finishers.Sampled;
            if (record == null) return;
            EnemyRecord captured = record == _finisherTargetFor ? _finisherTargetRecord : null;
            Node2D body = GodotObject.InstanceFromId(record.TargetId) as Node2D;
            bool alive = body != null && GodotObject.IsInstanceValid(body) && PlaytestWorld.HealthOf(body) > 0;
            if (alive && captured != null) {
                alive = !captured.Closed && _live.TryGetValue(record.TargetId, out EnemyRecord current) && current == captured;
            }
            if (!alive) {
                _finishers.OnTargetSample(Frame, alive: false, grounded: true, 0f, 0f);
                return;
            }
            bool grounded = body is not CharacterBody2D characterBody || characterBody.IsOnFloor();
            Rect2? hurt = PlaytestWorld.HurtboxRect(body);
            float landGap = hurt.HasValue
                ? PlaytestCombatMath.HorizontalGap(player.GlobalPosition.X, hurt.Value.Position.X, hurt.Value.End.X)
                : MathF.Abs(body.GlobalPosition.X - player.GlobalPosition.X);
            _finishers.OnTargetSample(Frame, alive: true, grounded, body.GlobalPosition.X, landGap);
        }

        private void OnUltimateActivation(UltimateActivationPayload payload) {
            if (payload.PlayerIndex == 0) _ultimateFrames.Add(Frame);
        }

        private void OnLockoutRefused(int playerIndex, PressSlot slot) {
            if (playerIndex == 0) _lockoutEvents[(int)slot]++;
        }

        private static List<object> Boxed(IEnumerable<int> values) => values.Select(v => (object)v).ToList();

        private Dictionary<string, object> SwingsReport() {
            List<SwingRecord> counted = _swings.Where(s => s.IsCounted).ToList();
            List<SwingRecord> combat = counted.Where(s => s.IsCombat).ToList();
            List<SwingRecord> whiffs = combat.Where(s => s.Outcome == "w").ToList();
            var byHit = new Dictionary<string, object>();
            foreach (IGrouping<string, SwingRecord> group in combat.GroupBy(s => PlaytestCombatMath.SwingHitKey(s.Hit, s.Air)).OrderBy(g => g.Key)) {
                List<SwingRecord> groupWhiffs = group.Where(s => s.Outcome == "w").ToList();
                byHit[group.Key] = new Dictionary<string, object> {
                    ["count"] = group.Count(),
                    ["connected"] = group.Count(s => s.Outcome == "c"),
                    ["contact_no_landing"] = group.Count(s => s.Outcome == "x"),
                    ["whiffed"] = groupWhiffs.Count,
                    ["whiff_gap_px_median"] = PlaytestCombatMath.Median(groupWhiffs.Select(s => s.GapMin).ToList())
                };
            }
            return new Dictionary<string, object> {
                ["count"] = counted.Count,
                ["combat"] = combat.Count,
                ["non_combat"] = counted.Count - combat.Count,
                ["connected"] = combat.Count(s => s.Outcome == "c"),
                ["contact_no_landing"] = combat.Count(s => s.Outcome == "x"),
                ["whiffed"] = whiffs.Count,
                ["whiff_in_reach"] = whiffs.Count(s => s.GapMin == 0),
                ["whiff_out_of_reach"] = whiffs.Count(s => s.GapMin > 0),
                ["orphan_basic_hits"] = OrphanBasicHits,
                ["connect_basis"] = PlaytestWorld.ConnectBasis,
                ["whiff_gap_px_median"] = PlaytestCombatMath.Median(whiffs.Select(s => s.GapMin).ToList()),
                ["connect_gap_px_median"] = PlaytestCombatMath.Median(combat.Where(s => s.Outcome == "c").Select(s => s.Gap).ToList()),
                ["by_hit"] = byHit,
                // GU-BOT: the same combat swings by basic-attack family (string,
                // air_attack — the single aerial strike since GU-A — up_attack, down_air).
                ["by_variant"] = combat.GroupBy(s => PlaytestCombatMath.HitVariant(s.Hit)).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => (object)new Dictionary<string, object> {
                        ["count"] = g.Count(),
                        ["connected"] = g.Count(s => s.Outcome == "c"),
                        ["whiffed"] = g.Count(s => s.Outcome == "w")
                    })
            };
        }

        private List<object> SwingLog() => _swings.Where(s => s.IsCounted).Take(2000).Select(s => (object)new Dictionary<string, object> {
            ["f"] = s.F,
            ["hit"] = s.Hit,
            ["air"] = s.Air,
            ["act"] = s.Act,
            ["out"] = s.Outcome,
            ["dmg"] = Math.Round(s.Damage, 1),
            ["n"] = s.Landed,
            ["n0"] = s.NoLanding,
            ["gap"] = s.Gap,
            ["gmin"] = s.GapMin,
            ["piv"] = s.Piv,
            ["foe"] = s.Foe,
            ["kind"] = s.Kind,
            ["behind"] = s.Behind
        }).ToList();

        /// <summary>Whether the swing that closed a <c>swing</c>-outcome record connected; null otherwise.</summary>
        private bool? NextConnected(FinisherReapproachTracker.Record record) {
            if (record.Outcome != "swing" || record.ClosedBySwingIndex < 0 || record.ClosedBySwingIndex >= _swings.Count) return null;
            SwingRecord next = _swings[record.ClosedBySwingIndex];
            if (next.Outcome == "dropped") return null;
            return next.IsOpen ? next.Landed > 0 : next.Outcome == "c";
        }

        private Dictionary<string, object> FinishersReport() {
            List<FinisherReapproachTracker.Record> records = _finishers.Records;
            var outcomes = new Dictionary<string, object>();
            foreach (string outcome in new[] { "swing", "killed", "special", "ultimate", "death", "lost" }) {
                outcomes[outcome] = records.Count(r => r.Outcome == outcome);
            }
            List<int> reapproach = _finishers.SwingFrames();
            List<bool> nextConnected = records.Select(NextConnected).Where(v => v.HasValue).Select(v => v.Value).ToList();
            var byKind = new Dictionary<string, object>();
            foreach (IGrouping<string, FinisherReapproachTracker.Record> group in records.GroupBy(r => r.TargetKind).OrderBy(g => g.Key)) {
                byKind[group.Key] = new Dictionary<string, object> {
                    ["n"] = group.Count(),
                    ["reapproach_median"] = PlaytestCombatMath.Median(group.Where(r => r.Outcome == "swing").Select(r => r.Frames).ToList()),
                    // §1.4(b): medians use swing outcomes only, like the re-approach.
                    ["carry_px_median"] = PlaytestCombatMath.Median(group.Where(r => r.Outcome == "swing" && r.CarryPx >= 0).Select(r => r.CarryPx).ToList()),
                    ["land_gap_px_median"] = PlaytestCombatMath.Median(group.Where(r => r.Outcome == "swing" && r.LandGapPx >= 0).Select(r => r.LandGapPx).ToList())
                };
            }
            return new Dictionary<string, object> {
                ["connected"] = records.Count,
                ["outcomes"] = outcomes,
                ["reapproach_frames_median"] = PlaytestCombatMath.Median(reapproach),
                ["reapproach_frames_p90"] = PlaytestCombatMath.Percentile(reapproach, 90),
                ["next_swing_connect_pct"] = nextConnected.Count == 0
                    ? -1.0
                    : Math.Round(100.0 * nextConnected.Count(v => v) / nextConnected.Count, 1),
                ["by_kind"] = byKind,
                ["log"] = records.Take(500).Select(r => (object)new Dictionary<string, object> {
                    ["f"] = r.ConnectFrame,
                    ["foe"] = r.Foe,
                    ["kind"] = r.TargetKind,
                    ["air"] = r.Air,
                    ["outcome"] = r.IsOpen ? "open" : r.Outcome,
                    ["frames"] = r.Frames,
                    ["carry"] = r.CarryPx,
                    ["land_gap"] = r.LandGapPx,
                    ["carry_void"] = r.CarryVoided,
                    ["hit_taken"] = r.HitTaken,
                    ["next_connected"] = NextConnected(r)
                }).ToList()
            };
        }

        private Dictionary<string, object> MeterReport() {
            int fillMedian = PlaytestCombatMath.Median(_meter.FillFrames);
            return new Dictionary<string, object> {
                ["level_start"] = Math.Round(_meter.LevelStart, 1),
                ["end"] = Math.Round(_meter.End, 1),
                ["gained"] = Math.Round(_meter.Gained, 1),
                ["spent"] = Math.Round(_meter.Spent, 1),
                ["gain_per_min"] = _meter.TrackedFrames > 0
                    ? Math.Round(_meter.Gained / (_meter.TrackedFrames / 3600.0), 1)
                    : -1.0,
                ["first_full_frame"] = _meter.FirstFullFrame,
                ["full_frames"] = _meter.FullFrames,
                ["fill_frames"] = Boxed(_meter.FillFrames),
                ["fills_dirty"] = _meter.FillsDirty,
                ["fill_seconds_median"] = fillMedian >= 0 ? Math.Round(fillMedian / 60.0, 2) : -1.0,
                ["ultimates_cast"] = _ultimateFrames.Count,
                ["ultimate_frames"] = Boxed(_ultimateFrames)
            };
        }

        /// <summary>
        /// The active Defy (plan 2.13): presses, executions and, per execution, the frame, the meter
        /// before, the targets blasted, the HP healed, the hits taken in the 180 frames before and
        /// after, and <c>died_within_3s</c>. Replaces the old passive <c>defy_procs</c>.
        /// </summary>
        private Dictionary<string, object> DefyReport() {
            List<DefyTracker.Resolved> resolved = _defy.Resolve(Frame);
            return new Dictionary<string, object> {
                ["policy"] = _bot is StoryBot story && PlaytestDefyPolicy.StyleDefies(story.Style) ? "play" : "never",
                ["window_frames"] = DefyTracker.WindowFrames,
                ["pressed"] = _defy.Pressed,
                ["executed"] = _defy.Executed,
                ["refused_by"] = Table(_defy.RefusedBy),
                ["died_within_3s"] = resolved.Count(r => r.DiedWithin3s),
                ["hits_before"] = resolved.Sum(r => r.HitsBefore),
                ["hits_after"] = resolved.Sum(r => r.HitsAfter),
                ["targets_blasted"] = resolved.Sum(r => r.Execution.Targets),
                ["healed"] = resolved.Sum(r => r.Execution.Healed),
                ["log"] = resolved.Select(r => (object)new Dictionary<string, object> {
                    ["f"] = r.Execution.Frame,
                    ["meter_before"] = Math.Round(r.Execution.MeterBefore, 1),
                    ["targets"] = r.Execution.Targets,
                    ["healed"] = r.Execution.Healed,
                    ["hits_before"] = r.HitsBefore,
                    ["hits_after"] = r.HitsAfter,
                    ["died_within_3s"] = r.DiedWithin3s,
                    ["after_truncated"] = r.AfterTruncated
                }).ToList()
            };
        }

        /// <summary>
        /// The Momentum gauge (plan 2.13): pips at level start (2.0 on a fresh level, like
        /// <c>meter.level_start</c>), gained by source (<c>regen</c>, or the landing swing's hitbox ID),
        /// spent per Special, Specials cast per minute, presses refused for want of a pip, and the
        /// frames spent under one whole pip and full.
        /// </summary>
        private Dictionary<string, object> MomentumReport() {
            double minutes = _momentum.TrackedFrames / 3600.0;
            int starvedPresses = _presses.For(PressSlot.Special1).RefusedWith.GetValueOrDefault("no_momentum")
                + _presses.For(PressSlot.Special2).RefusedWith.GetValueOrDefault("no_momentum");
            return new Dictionary<string, object> {
                ["pips_start"] = Math.Round(_momentum.Start, 2),
                ["pips_end"] = Math.Round(_momentum.End, 2),
                ["gained"] = Math.Round(_momentum.Gained, 2),
                ["gained_by"] = _momentum.GainedBy.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .ToDictionary(kv => kv.Key, kv => (object)Math.Round(kv.Value, 3)),
                ["spent"] = new Dictionary<string, object> {
                    ["special1"] = _momentum.SpentSpecial1,
                    ["special2"] = _momentum.SpentSpecial2
                },
                ["specials_cast"] = _momentum.SpecialsCast,
                ["specials_per_min"] = minutes > 0 ? Math.Round(_momentum.SpecialsCast / minutes, 2) : -1.0,
                ["starved_presses"] = starvedPresses,
                ["frames_starved"] = _momentum.FramesStarved,
                ["frames_full"] = _momentum.FramesFull,
                ["tracked_frames"] = _momentum.TrackedFrames
            };
        }

        /// <summary>The bot's Special press policy (<c>gated</c> | <c>confirm</c>), or null without a Story bot.</summary>
        private string PressPolicy => _bot is StoryBot story ? story.Policy.ToString().ToLowerInvariant() : null;

        private static Dictionary<string, object> Table(Dictionary<string, int> counts) =>
            counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value);

        private Dictionary<string, object> PressesReport() {
            var report = new Dictionary<string, object> {
                // C3: which Special press policy the bot ran (--special-press).
                ["policy"] = PressPolicy
            };
            foreach (PressSlot slot in new[] { PressSlot.Special1, PressSlot.Special2, PressSlot.Ultimate }) {
                AbilityPressTracker.Counts counts = _presses.For(slot);
                report[slot.ToString().ToLowerInvariant()] = new Dictionary<string, object> {
                    ["pressed"] = counts.Pressed,
                    ["accepted"] = counts.Accepted,
                    ["refused"] = counts.Refused,
                    ["unresolved"] = counts.Unresolved,
                    // C3 (§1.4(d)): every readable condition that held at a refused
                    // press's snapshot; conditions present, not the hero's check order.
                    ["refused_with"] = Table(counts.RefusedWith),
                    ["refused_sole"] = Table(counts.RefusedSole),
                    ["refused_none"] = counts.RefusedNone,
                    ["refused_state"] = Table(counts.RefusedState),
                    // The hero's own lockout-refusal event (group A's D10, agent A2); null before it lands.
                    ["lockout_events"] = PlaytestWorld.HasLockoutRefusedEvent ? (object)_lockoutEvents[(int)slot] : null
                };
            }
            return report;
        }

        /// <summary>C3: the mob melee swings block (§1.4(e)). Aggregates use natural closes only.</summary>
        private Dictionary<string, object> MobSwingsReport() {
            List<MobSwingRecord> closed = _mobSwings.Where(s => !s.IsOpen).ToList();
            List<MobSwingRecord> natural = closed.Where(s => s.Natural).ToList();
            List<MobSwingRecord> whiffs = natural.Where(s => s.Outcome == "w").ToList();
            var byEnemy = new Dictionary<string, object>();
            foreach (IGrouping<string, MobSwingRecord> group in natural.GroupBy(s => s.Foe).OrderBy(g => g.Key, StringComparer.Ordinal)) {
                List<MobSwingRecord> groupWhiffs = group.Where(s => s.Outcome == "w").ToList();
                byEnemy[group.Key] = new Dictionary<string, object> {
                    ["count"] = group.Count(),
                    ["whiffed"] = groupWhiffs.Count,
                    ["whiff_gap_px_median"] = PlaytestCombatMath.Median(groupWhiffs.Where(s => s.Gap >= 0).Select(s => s.Gap).ToList())
                };
            }
            return new Dictionary<string, object> {
                ["count"] = closed.Count,
                ["natural"] = natural.Count,
                ["cut"] = closed.Count - natural.Count,
                ["connected"] = natural.Count(s => s.Outcome == "c"),
                ["whiffed"] = whiffs.Count,
                ["whiff_out_of_reach"] = whiffs.Count(s => s.Gap > 0),
                ["whiff_gap_px_median"] = PlaytestCombatMath.Median(whiffs.Where(s => s.Gap >= 0).Select(s => s.Gap).ToList()),
                ["by_enemy"] = byEnemy,
                ["log"] = closed.Take(500).Select(s => (object)new Dictionary<string, object> {
                    ["f"] = s.F,
                    ["foe"] = s.Foe,
                    ["kind"] = s.Kind,
                    ["ability"] = s.Ability,
                    ["out"] = s.Outcome,
                    ["natural"] = s.Natural,
                    ["gap"] = s.Gap
                }).ToList()
            };
        }

        private double MobWhiffPercent() {
            List<MobSwingRecord> natural = _mobSwings.Where(s => !s.IsOpen && s.Natural).ToList();
            return natural.Count > 0 ? Math.Round(100.0 * natural.Count(s => s.Outcome == "w") / natural.Count, 1) : -1.0;
        }

        /// <summary>The runner's one-line <c>PLAYTEST_RESULT</c> combat summary.</summary>
        public Dictionary<string, object> CombatSummary() {
            List<SwingRecord> combat = _swings.Where(s => s.IsCounted && s.IsCombat).ToList();
            int whiffs = combat.Count(s => s.Outcome == "w");
            return new Dictionary<string, object> {
                ["swings"] = combat.Count,
                ["whiff_pct"] = combat.Count > 0 ? Math.Round(100.0 * whiffs / combat.Count, 1) : -1.0,
                ["reapproach_med"] = PlaytestCombatMath.Median(_finishers.SwingFrames()),
                ["ults"] = _ultimateFrames.Count,
                ["meter_start"] = Math.Round(_meter.LevelStart, 1),
                // Meter and Defy update (MM-BOTS): Defies executed, those followed by a death within
                // 3 s, Specials refused for want of a pip, the gauge at level start and Specials a minute.
                ["defies"] = _defy.Executed,
                ["defy_died3s"] = _defy.Resolve(Frame).Count(r => r.DiedWithin3s),
                ["starved"] = _presses.For(PressSlot.Special1).RefusedWith.GetValueOrDefault("no_momentum")
                    + _presses.For(PressSlot.Special2).RefusedWith.GetValueOrDefault("no_momentum"),
                ["momentum0"] = Math.Round(_momentum.Start, 2),
                ["specials_per_min"] = _momentum.TrackedFrames > 0
                    ? Math.Round(_momentum.SpecialsCast / (_momentum.TrackedFrames / 3600.0), 2)
                    : -1.0,
                ["s_refused"] = _presses.For(PressSlot.Special1).Refused + _presses.For(PressSlot.Special2).Refused,
                // C3: natural mob melee swings that whiffed, and the bot's Special press policy.
                ["mob_whiff_pct"] = MobWhiffPercent(),
                ["policy"] = PressPolicy,
                // CM-BOT (7.2): hero hits taken within 30 frames of another source's hit.
                ["xsrc_rehits"] = _rehits.CrossSource30,
                // CM-FIXA-PRES: the same with hazard contacts out on both sides.
                ["xsrc_rehits_combat"] = _rehits.CrossSource30Combat
            };
        }

        /// <summary>Plain dictionary tree for JSON serialization.</summary>
        public Dictionary<string, object> ToReport() {
            List<EnemyRecord> engaged = Enemies.Where(e => e.EngagedFrame >= 0).ToList();
            List<EnemyRecord> killed = Enemies.Where(e => e.KilledFrame >= 0 && e.DamageTaken > 0).ToList();
            List<EnemyRecord> passedAlive = Enemies.Where(e => e.PassedFrame >= 0
                && (e.KilledFrame < 0 || e.KilledFrame > e.PassedFrame)).ToList();
            List<int> ttk = killed.Where(e => e.FramesToKill.HasValue).Select(e => e.FramesToKill.Value).OrderBy(v => v).ToList();
            List<EnemyRecord> members = Enemies.Where(e => e.LockID.Length > 0).ToList();
            _locks.Summarize();

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
                    // The pre-lock meaning, kept for comparability: the hero got 250 px
                    // east of a live mob it had come within 450 px of.
                    ["passed_while_alive"] = passedAlive.Count,
                    ["passed_then_hit_player"] = passedAlive.Count(e => e.HitsOnPlayer > 0),
                    // GU-BOT (2026-10-12): the lock meaning — the hero left an
                    // uncleared lock's area with members alive (a revert: a
                    // rewind landing, a barrier failure) — and the old passes split
                    // into lock members and mobs no lock owns.
                    ["left_lock_with_members_alive"] = _locks.LeftWithMembersAlive,
                    ["passed_alive_lock_members"] = passedAlive.Count(e => e.LockID.Length > 0),
                    ["passed_alive_outside_locks"] = passedAlive.Count(e => e.LockID.Length == 0),
                    ["lock_members_seen"] = members.Count,
                    ["lock_members_killed"] = members.Count(e => e.KilledFrame >= 0 && e.LastSeenHP <= 0),
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
                    ["lock"] = e.LockID,
                    ["damage_by_bot_action"] = e.DamageByBotAction.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
                }).ToList(),
                ["objectives_broken_frame"] = Objectives.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["rooms"] = Rooms.Select(r => (object)new Dictionary<string, object> { ["frame"] = r.Frame, ["room"] = r.Room }).ToList(),
                // D6d combat telemetry (specs/C-meter-telemetry.md §4.1 step 3.8).
                ["swings"] = SwingsReport(),
                ["swing_log"] = SwingLog(),
                ["finishers"] = FinishersReport(),
                ["meter"] = MeterReport(),
                // Meter and Defy update (MM-BOTS): the active Defy and the Momentum gauge.
                ["defy"] = DefyReport(),
                ["momentum"] = MomentumReport(),
                ["ability_presses"] = PressesReport(),
                // C3 (§4.3 step 2.4): mob MeleeStrike/ChargeDash active windows.
                ["mob_swings"] = MobSwingsReport(),
                // GU-BOT (2026-10-12): the hero's crowd under the engagement
                // director, and every encounter lock.
                ["mob_engagement"] = MobEngagementReport(),
                ["locks"] = LocksReport(),
                // CM-BOT (decision 7.2, 2026-10-16): hits on the hero by source, and
                // the cross-source re-hits within 30 (and 20) frames.
                ["rehits"] = RehitsReport(),
                ["trace"] = Trace
            };
        }
    }
}
