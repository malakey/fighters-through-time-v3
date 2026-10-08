using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;

namespace FTT.Diagnostics {

    /// <summary>
    /// Read-only view of the Story world a playtest bot steers by: the hero,
    /// live enemies, hazards with their phases, the level's puzzle pieces and
    /// its navigation graph. Node lists are rescanned periodically and every
    /// node is validated on use, so nothing stale survives a death rewind, a
    /// pool recycle or a door being freed. Pure observation — nothing here
    /// writes gameplay state.
    /// </summary>
    public sealed class PlaytestWorld {
        private readonly SceneTree _tree;
        public readonly PlaytestNav Nav;
        public int Frame { get; private set; }

        public PlaytestWorld(SceneTree tree) {
            _tree = tree;
            Nav = new PlaytestNav(tree);
            EventBus bus = EventBus.Instance;
            if (bus != null) bus.OnHazardStateChanged += OnHazardState;
        }

        public void Detach() {
            EventBus bus = EventBus.Instance;
            if (bus != null) bus.OnHazardStateChanged -= OnHazardState;
        }

        public SceneTree Tree => _tree;

        public PlayerController Player {
            get {
                Godot.Collections.Array<Node> nodes = _tree.GetNodesInGroup("StoryPlayer");
                using var lifetime = nodes.AsDisposable();
                foreach (Node node in nodes) {
                    if (node is PlayerController player && GodotObject.IsInstanceValid(player)) return player;
                }
                return null;
            }
        }

        private ulong _tickedPhysicsFrame = ulong.MaxValue;

        /// <summary>Once per unpaused physics frame, before the bot decides. Idempotent within a frame.</summary>
        public void Tick() {
            ulong physicsFrame = Engine.GetPhysicsFrames();
            if (physicsFrame == _tickedPhysicsFrame) return;
            _tickedPhysicsFrame = physicsFrame;
            Frame++;
            PlayerController player = Player;
            Nav.Update(player);
            Node scene = _tree.CurrentScene;
            if (scene != null && (scene.GetInstanceId() != _scanSceneId || Frame - _scanFrame >= 120)) Scan(scene);
        }

        // =================================================================
        // Typed node scan
        // =================================================================

        private ulong _scanSceneId;
        private int _scanFrame = -1000;
        public readonly List<StoryCyclicHazard> CyclicHazards = new();
        public readonly List<SearchlightZone> Searchlights = new();
        public readonly List<ChronalExtractor> Extractors = new();
        public readonly List<CheckpointTrigger> Checkpoints = new();
        public readonly List<SequenceLock> SequenceLocks = new();
        public readonly List<ConductiveCoil> Coils = new();
        public readonly List<BeamEmitter> Emitters = new();
        public readonly List<BeamReceiver> Receivers = new();
        public readonly List<RescuableNPC> Rescuables = new();
        public readonly List<PressurePlate> Plates = new();
        public readonly List<DamageableEnvironmentObject> Breakables = new();
        public readonly List<EscapeSequenceController> Escapes = new();
        public readonly List<BossEncounterController> BossEncounters = new();
        public readonly List<NearCaptureBeat> NearCaptures = new();
        public readonly List<RisingWaterZone> RisingWater = new();
        public readonly List<PendulumAnchor> Pendulums = new();
        public readonly List<Rect2> Spikes = new();
        public StoryLevelControllerBase Level { get; private set; }

        private void Scan(Node scene) {
            _scanSceneId = scene.GetInstanceId();
            _scanFrame = Frame;
            CyclicHazards.Clear(); Searchlights.Clear(); Extractors.Clear(); Checkpoints.Clear();
            SequenceLocks.Clear(); Coils.Clear(); Emitters.Clear(); Receivers.Clear(); Rescuables.Clear();
            Plates.Clear(); Breakables.Clear(); Escapes.Clear(); BossEncounters.Clear(); NearCaptures.Clear();
            RisingWater.Clear(); Pendulums.Clear(); Spikes.Clear();
            Level = scene as StoryLevelControllerBase;
            ScanNode(scene);
        }

        private void ScanNode(Node node) {
            switch (node) {
                case CharacterBody2D: return;
                case StoryCyclicHazard h: CyclicHazards.Add(h); break;
                case SearchlightZone s: Searchlights.Add(s); break;
                case ChronalExtractor x: Extractors.Add(x); Breakables.Add(x); break;
                case DamageableEnvironmentObject d: Breakables.Add(d); break;
                case CheckpointTrigger c: Checkpoints.Add(c); break;
                case SequenceLock l: SequenceLocks.Add(l); break;
                case ConductiveCoil coil: Coils.Add(coil); break;
                case BeamEmitter e: Emitters.Add(e); break;
                case BeamReceiver r: Receivers.Add(r); break;
                case RescuableNPC n: Rescuables.Add(n); break;
                case PressurePlate p: Plates.Add(p); break;
                case EscapeSequenceController esc: Escapes.Add(esc); break;
                case BossEncounterController b: BossEncounters.Add(b); break;
                case NearCaptureBeat beat: NearCaptures.Add(beat); break;
                case RisingWaterZone w: RisingWater.Add(w); break;
                case PendulumAnchor pa: Pendulums.Add(pa); break;
                case Area2D area when area.Name.ToString().StartsWith("Spikes_", StringComparison.Ordinal): {
                    Rect2? r = ShapeRect(area);
                    if (r.HasValue) Spikes.Add(r.Value);
                    break;
                }
            }
            Godot.Collections.Array<Node> children = node.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) ScanNode(child);
        }

        /// <summary>World AABB of the first rectangle collision shape under <paramref name="node"/>.</summary>
        public static Rect2? ShapeRect(Node node) {
            Godot.Collections.Array<Node> children = node.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is CollisionShape2D cs && cs.Shape is RectangleShape2D rs) {
                    Transform2D xf = cs.GlobalTransform;
                    Vector2 ex = (xf.X * rs.Size.X).Abs() / 2f + (xf.Y * rs.Size.Y).Abs() / 2f;
                    return new Rect2(xf.Origin - ex, ex * 2f);
                }
                if (child is CollisionShape2D cc && cc.Shape is CircleShape2D circle) {
                    float r = circle.Radius * cc.GlobalTransform.X.Length();
                    return new Rect2(cc.GlobalPosition - new Vector2(r, r), new Vector2(r * 2f, r * 2f));
                }
            }
            return null;
        }

        // =================================================================
        // Enemies
        // =================================================================

        public static int HealthOf(Node2D body) => body switch {
            EnemyController enemy => enemy.CurrentHP,
            BossController boss => boss.CurrentHP,
            ArenaGuardian guardian => guardian.CurrentHP,
            PlayerController clone => clone.CurrentHP,
            _ => 0
        };

        public static bool IsBossLike(Node2D body) => body is BossController or ArenaGuardian
            || (body is PlayerController p && p.IsInGroup(MirrorParadoxController.MirrorGroup));

        public static bool IsAttacking(Node2D body) => body switch {
            EnemyController enemy => enemy.CurrentState == EnemyState.Attacking,
            BossController boss => boss.CurrentState == BossState.Attacking,
            PlayerController clone => clone.CurrentState is CharacterState.Attacking or CharacterState.UsingSpecial or CharacterState.UsingUltimate,
            _ => false
        };

        private static bool IsAliveFoe(Node2D body) {
            if (!GodotObject.IsInstanceValid(body) || !body.IsVisibleInTree()) return false;
            return body switch {
                EnemyController enemy => enemy.IsAlive && enemy.CurrentHP > 0,
                BossController boss => boss.IsAlive && boss.CurrentHP > 0,
                ArenaGuardian guardian => guardian.IsAlive,
                PlayerController clone => clone.CurrentHP > 0 && clone.CurrentState != CharacterState.Dead,
                _ => false
            };
        }

        /// <summary>Every live foe: enemies, bosses, arena guardians and the Mirror clone.</summary>
        public List<Node2D> Foes() {
            var list = new List<Node2D>();
            AddGroup(list, "Enemies");
            AddGroup(list, ArenaGuardian.GroupName);
            AddGroup(list, MirrorParadoxController.MirrorGroup);
            return list;
        }

        private void AddGroup(List<Node2D> list, string group) {
            Godot.Collections.Array<Node> nodes = _tree.GetNodesInGroup(group);
            using var lifetime = nodes.AsDisposable();
            foreach (Node node in nodes) {
                if (node is Node2D body && !list.Contains(body) && IsAliveFoe(body)) list.Add(body);
            }
        }

        /// <summary>Solid level geometry (Environment layer) between two points.</summary>
        public bool LineOfSight(Vector2 from, Vector2 to) {
            PlayerController player = Player;
            PhysicsDirectSpaceState2D space = player?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return true;
            // Breakables do not count as "behind a wall" (the bot can strike
            // through them), so the ray skips them and keeps looking.
            var exclude = new Godot.Collections.Array<Rid>();
            using var excludeLifetime = exclude.AsDisposable();
            for (int attempt = 0; attempt < 4; attempt++) {
                var query = PhysicsRayQueryParameters2D.Create(from, to, CollisionLayers.Environment, exclude);
                Godot.Collections.Dictionary hit = space.IntersectRay(query);
                using var lifetime = hit;
                if (hit.Count == 0) return true;
                if (!hit.TryGetValue("collider", out Variant collider) || collider.AsGodotObject() is not DamageableEnvironmentObject) return false;
                if (!hit.TryGetValue("rid", out Variant rid)) return false;
                exclude.Add(rid.AsRid());
            }
            return true;
        }

        /// <summary>Seconds of an Extractor's current safe window left (NaN when unknown).</summary>
        public float ExtractorSafeRemaining(ChronalExtractor extractor) {
            if (extractor.IsTelegraphing) return 0f;
            if (_hazardClock.TryGetValue(extractor.ObjectID ?? "", out var clock) && clock.Phase == HazardPhase.Active) {
                return extractor.SafeWindowSeconds - (Frame - clock.Frame) / 60f;
            }
            return float.NaN;
        }

        /// <summary>An enemy projectile within <paramref name="radius"/> that is moving toward the hero.</summary>
        public bool ProjectileIncoming(PlayerController player, float radius) {
            Vector2 at = player.GlobalPosition + new Vector2(0f, -32f);
            Godot.Collections.Array<Node> nodes = _tree.GetNodesInGroup("enemy_projectile");
            using var lifetime = nodes.AsDisposable();
            foreach (Node node in nodes) {
                if (node is not EnemyProjectile projectile || !GodotObject.IsInstanceValid(projectile) || !projectile.IsVisibleInTree()) continue;
                Vector2 toHero = at - projectile.GlobalPosition;
                if (toHero.Length() > radius) continue;
                if (projectile.Velocity.Dot(toHero) > 0f) return true;
            }
            return false;
        }

        // =================================================================
        // Hazards
        // =================================================================

        private readonly Dictionary<string, (int Frame, float Duration, HazardPhase Phase)> _hazardClock = new();

        private void OnHazardState(HazardStatePayload payload) {
            if (string.IsNullOrEmpty(payload.HazardID)) return;
            _hazardClock[payload.HazardID] = (Frame, payload.Duration, payload.Phase);
        }

        /// <summary>Seconds left in a cyclic hazard's current phase (estimated from its state events).</summary>
        public float PhaseRemaining(StoryCyclicHazard hazard) {
            if (_hazardClock.TryGetValue(hazard.HazardID ?? "", out var clock) && clock.Phase == hazard.Phase) {
                return Mathf.Max(0f, clock.Duration - (Frame - clock.Frame) / 60f);
            }
            return hazard.Phase switch {
                HazardPhase.Cooldown => Mathf.Max(0f, hazard.CooldownDuration - Frame / 60f),
                HazardPhase.Warning => hazard.WarningDuration,
                _ => hazard.ActiveDuration
            };
        }

        public readonly struct HazardZone {
            public readonly StoryCyclicHazard Hazard;
            public readonly Rect2 Area;
            public HazardZone(StoryCyclicHazard hazard, Rect2 area) { Hazard = hazard; Area = area; }
        }

        public IEnumerable<HazardZone> LiveCyclicHazards() {
            foreach (StoryCyclicHazard hazard in CyclicHazards) {
                if (!GodotObject.IsInstanceValid(hazard) || !hazard.IsInsideTree()) continue;
                // A disabled hazard can still be driven by its level through
                // ForcePhase (Level 10's audience throw): only a cold one is safe.
                if (!hazard.Enabled && hazard.Phase == HazardPhase.Cooldown) continue;
                Rect2? area = ShapeRect(hazard);
                if (area.HasValue) yield return new HazardZone(hazard, area.Value);
            }
        }

        /// <summary>The DelayedStrike searchlight exposure the hero has accumulated (seconds), worst light.</summary>
        public float SearchlightExposure(PlayerController player, out SearchlightZone light) {
            light = null;
            float worst = 0f;
            foreach (SearchlightZone zone in Searchlights) {
                if (!GodotObject.IsInstanceValid(zone) || !zone.Enabled || zone.Mode != SearchlightMode.DelayedStrike) continue;
                float exposure = zone.ExposureFor(player);
                if (exposure > worst) {
                    worst = exposure;
                    light = zone;
                }
            }
            return worst;
        }

        /// <summary>A live Extractor that is telegraphing its discharge within <paramref name="radius"/>.</summary>
        public ChronalExtractor TelegraphingExtractorNear(Vector2 point, float radius) {
            foreach (ChronalExtractor extractor in Extractors) {
                if (!GodotObject.IsInstanceValid(extractor) || extractor.IsDestroyed || extractor.IsSealedShutdown) continue;
                if (extractor is ResonanceHoldNode) continue;
                if (!extractor.IsTelegraphing) continue;
                if (extractor.GlobalPosition.DistanceTo(point + new Vector2(0f, -32f)) < radius) return extractor;
            }
            return null;
        }

        public EscapeSequenceController RunningEscape() {
            foreach (EscapeSequenceController escape in Escapes) {
                if (GodotObject.IsInstanceValid(escape) && escape.IsRunning && !escape.IsCompleted) return escape;
            }
            return null;
        }

        // =================================================================
        // Objectives
        // =================================================================

        /// <summary>The level's armed, unsealed N01 sealing anchor, if any.</summary>
        public TemporalCoreAnchor ArmedSealAnchor() {
            Godot.Collections.Array<Node> nodes = _tree.GetNodesInGroup("puzzle_object");
            using var lifetime = nodes.AsDisposable();
            foreach (Node node in nodes) {
                if (node is TemporalCoreAnchor anchor && GodotObject.IsInstanceValid(anchor) && anchor.IsArmed && !anchor.IsInserted) return anchor;
            }
            return null;
        }

        /// <summary>Healing pickups (orbs) currently lying in the level.</summary>
        public IEnumerable<StoryPickup> HealingPickups() {
            Godot.Collections.Array<Node> nodes = _tree.GetNodesInGroup("story_loot");
            using var lifetime = nodes.AsDisposable();
            var list = new List<StoryPickup>();
            foreach (Node node in nodes) {
                if (node is StoryPickup pickup && GodotObject.IsInstanceValid(pickup) && pickup.IsVisibleInTree() && pickup.HealingAmount > 0) list.Add(pickup);
            }
            return list;
        }

        /// <summary>
        /// The quarter-turn each coil needs so a beam reaches a receiver: a
        /// search over the routing graph from every emitter, choosing each
        /// coil's facing (east/south/west/north outputs) along the way.
        /// </summary>
        public Dictionary<ConductiveCoil, int> CoilSolution() {
            var solution = new Dictionary<ConductiveCoil, int>();
            foreach (BeamEmitter emitter in Emitters) {
                if (!GodotObject.IsInstanceValid(emitter)) continue;
                var visiting = new HashSet<PowerRoutingNode>();
                var choice = new Dictionary<ConductiveCoil, int>();
                if (RouteToReceiver(emitter, visiting, choice, 0)) {
                    foreach (var pair in choice) solution[pair.Key] = pair.Value;
                    return solution;
                }
            }
            return solution;
        }

        private static bool RouteToReceiver(PowerRoutingNode node, HashSet<PowerRoutingNode> visiting,
            Dictionary<ConductiveCoil, int> choice, int depth) {
            if (node is BeamReceiver) return true;
            if (depth > 16 || !visiting.Add(node)) return false;
            if (node is ConductiveCoil coil) {
                Godot.Collections.Array<NodePath>[] outputs = {
                    coil.EastOutputPaths, coil.SouthOutputPaths, coil.WestOutputPaths, coil.NorthOutputPaths
                };
                for (int turns = 0; turns < 4; turns++) {
                    if (outputs[turns] == null) continue;
                    foreach (NodePath path in outputs[turns]) {
                        if (coil.GetNodeOrNull<PowerRoutingNode>(path) is not PowerRoutingNode next || next == coil) continue;
                        choice[coil] = turns;
                        if (RouteToReceiver(next, visiting, choice, depth + 1)) return true;
                        choice.Remove(coil);
                    }
                }
            } else if (node.OutputPaths != null) {
                foreach (NodePath path in node.OutputPaths) {
                    if (node.GetNodeOrNull<PowerRoutingNode>(path) is PowerRoutingNode next && next != node
                        && RouteToReceiver(next, visiting, choice, depth + 1)) return true;
                }
            }
            visiting.Remove(node);
            return false;
        }

        /// <summary>The InteractionArea that would trigger <paramref name="target"/>.</summary>
        public static InteractionArea InteractionAreaOf(Node target) {
            if (target == null || !GodotObject.IsInstanceValid(target)) return null;
            Godot.Collections.Array<Node> children = target.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is InteractionArea area) return area;
            }
            return null;
        }
    }
}
