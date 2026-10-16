using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
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
        public readonly List<RotatingGear> Gears = new();
        public readonly List<Rect2> Spikes = new();
        public StoryLevelControllerBase Level { get; private set; }

        private void Scan(Node scene) {
            _scanSceneId = scene.GetInstanceId();
            _scanFrame = Frame;
            CyclicHazards.Clear(); Searchlights.Clear(); Extractors.Clear(); Checkpoints.Clear();
            SequenceLocks.Clear(); Coils.Clear(); Emitters.Clear(); Receivers.Clear(); Rescuables.Clear();
            Plates.Clear(); Breakables.Clear(); Escapes.Clear(); BossEncounters.Clear(); NearCaptures.Clear();
            RisingWater.Clear(); Pendulums.Clear(); Gears.Clear(); Spikes.Clear();
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
                case RotatingGear gear: Gears.Add(gear); break;
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

        /// <summary>
        /// World AABB of <paramref name="body"/>'s <c>Hurtbox</c> child (enemies,
        /// bosses, arena guardians, decoys and the Mirror clone all author one by
        /// that name), or null without one. <see cref="ShapeRect"/> dereferences
        /// its argument, so this is the null-safe way in.
        /// </summary>
        public static Rect2? HurtboxRect(Node2D body) {
            if (body == null || !GodotObject.IsInstanceValid(body)) return null;
            Node hurtbox = body.GetNodeOrNull<Node>("Hurtbox");
            return hurtbox != null ? ShapeRect(hurtbox) : null;
        }

        #region Hero gates — group A seams; the only place in scripts/Diagnostics/ that names a group-A member
        // D6d telemetry (specs/C-meter-telemetry.md §4.1, P6). Group A landed in two
        // agents, A1 (D1 hit-confirm cancel, D1a landed contacts) and A2 (the D10
        // "not yet" flash, EventBus.OnAbilityLockoutRefused), both 2026-10-11, so
        // every member below has its post-A body. No other file under
        // scripts/Diagnostics/ names HitPayload.ContactLanded, PlayerController.SwingLanded,
        // PlayerController.SpecialGateFramesRemaining or EventBus.OnAbilityLockoutRefused
        // (nor, since CM-BOT, PlayerController.DamageTaken).

        /// <summary>True once the hero has a hit-confirm Special gate distinct from the ability lockout (group A's D1).</summary>
        public static bool HasHitConfirmGate => true;

        /// <summary>True once the hero raises its own lockout-refusal event (group A's D10, agent A2).</summary>
        public static bool HasLockoutRefusedEvent => true;

        /// <summary>Frames until the hero's Specials are legal again: the D1 Special gate.</summary>
        public static int SpecialGateFrames(PlayerController p) => p.SpecialGateFramesRemaining;

        // D1a: damage applied or absorbed by a shield or barrier layer; a block, an
        // invulnerability/ward/freeze refusal and every strike surface or prop do not
        // land; a zero-authored-damage payload never lands.
        /// <summary>The one telemetry seam for "this contact landed".</summary>
        public static bool IsLandedContact(in HitPayload p, float dealt) => p.ContactLanded;

        /// <summary>Reported as <c>swings.connect_basis</c>: which "landed" rule <see cref="IsLandedContact"/> applies.</summary>
        public static string ConnectBasis => "d1a";

        /// <summary>Whether the hero's current swing has landed (D1's latch).</summary>
        public static bool? SwingLanded(PlayerController p) => p.SwingLanded;

        /// <summary>Subscribes to the hero's lockout refusals (D10); returns the unsubscribe action.</summary>
        public static Action SubscribeLockoutRefused(Action<int, PressSlot> handler) {
            EventBus bus = EventBus.Instance;
            if (bus == null || handler == null) return () => { };
            Action<AbilityLockoutRefusedPayload> forward = e => handler(e.PlayerIndex, SlotOf(e.Slot));
            bus.OnAbilityLockoutRefused += forward;
            return () => { if (EventBus.Instance != null) EventBus.Instance.OnAbilityLockoutRefused -= forward; };
        }

        private static PressSlot SlotOf(AbilitySlot slot) => slot switch {
            AbilitySlot.Special1 => PressSlot.Special1,
            AbilitySlot.Special2 => PressSlot.Special2,
            _ => PressSlot.Ultimate
        };

        // CM-BOT (decision 7.2, 2026-10-16): the hero's damage observations, the one
        // place in scripts/Diagnostics/ that names PlayerController.DamageTaken. The
        // event is observation only and may run inside the physics flush, so the
        // handler must only record.
        /// <summary>Subscribes to one hero's <c>DamageTaken</c> observations; returns the unsubscribe action.</summary>
        public static Action SubscribeDamageTaken(PlayerController hero, Action<PlayerDamageObservation> handler) {
            if (hero == null || handler == null || !GodotObject.IsInstanceValid(hero)) return () => { };
            hero.DamageTaken += handler;
            return () => hero.DamageTaken -= handler;
        }
        #endregion

        #region Encounter locks and the engagement director — the 2026-10-12 gameplay update's seams (GU-BOT)
        // The only place in scripts/Diagnostics/ that names the encounter-lock
        // framework (GU-E: EncounterLock, its group, the member hooks on
        // EnemyController, CheckpointTrigger.IsHeldByEncounterLock) or the
        // engagement director's per-mob state (GU-F: EnemyController.Engagement.cs).
        // Every member read here is a plain getter: the director is never asked
        // (asking prunes its registry), so the bots and the telemetry stay pure
        // observers.

        private readonly List<EncounterLock> _locks = new();
        private ulong _locksPhysicsFrame = ulong.MaxValue;

        /// <summary>
        /// Every encounter lock in the tree (group <see cref="EncounterLock.Group"/>:
        /// base levels and the standalone Level 0 / Florence locks alike), once per
        /// physics frame.
        /// </summary>
        public IReadOnlyList<EncounterLock> EncounterLocks() {
            ulong physicsFrame = Engine.GetPhysicsFrames();
            if (physicsFrame == _locksPhysicsFrame) return _locks;
            _locksPhysicsFrame = physicsFrame;
            _locks.Clear();
            Godot.Collections.Array<Node> nodes = _tree.GetNodesInGroup(EncounterLock.Group);
            using var lifetime = nodes.AsDisposable();
            foreach (Node node in nodes) {
                if (node is EncounterLock encounterLock && GodotObject.IsInstanceValid(encounterLock)
                    && encounterLock.IsInsideTree() && !encounterLock.IsQueuedForDeletion()) {
                    _locks.Add(encounterLock);
                }
            }
            return _locks;
        }

        /// <summary>The lock the hero is fighting in now (Engaged), or null.</summary>
        public EncounterLock EngagedLock() {
            foreach (EncounterLock encounterLock in EncounterLocks()) {
                if (encounterLock.State == EncounterLockState.Engaged) return encounterLock;
            }
            return null;
        }

        /// <summary>
        /// The nearest Armed lock whose area lies between <paramref name="fromX"/>
        /// and <paramref name="goalX"/> (or holds <paramref name="fromX"/>), or
        /// null: the next area the hero has to walk into and clear on the way.
        /// </summary>
        public EncounterLock ArmedLockOnTheWay(float fromX, float goalX) {
            EncounterLock best = null;
            float bestDistance = float.MaxValue;
            float lo = Mathf.Min(fromX, goalX), hi = Mathf.Max(fromX, goalX);
            foreach (EncounterLock encounterLock in EncounterLocks()) {
                if (encounterLock.State != EncounterLockState.Armed) continue;
                Rect2 area = encounterLock.Area;
                if (area.End.X < lo || area.Position.X > hi) continue;
                float distance = PlaytestCombatMath.HorizontalGap(fromX, area.Position.X, area.End.X);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = encounterLock;
                }
            }
            return best;
        }

        /// <summary>
        /// A point well inside <paramref name="encounterLock"/>'s area, on its entry
        /// side as seen from <paramref name="fromX"/>: where a hero walks to engage
        /// it (past the engage inset by a comfortable margin).
        /// </summary>
        public static float EntryPointX(EncounterLock encounterLock, float fromX) {
            Rect2 area = encounterLock.Area;
            float inset = Mathf.Min(area.Size.X * 0.5f,
                HeroPhysics.BodyHalfWidth + EncounterLockRules.EngageInsetPixels + LockEntryMarginPixels);
            return fromX <= area.GetCenter().X ? area.Position.X + inset : area.End.X - inset;
        }

        /// <summary>How far past the engage line the bot aims when it walks into an Armed lock.</summary>
        public const float LockEntryMarginPixels = 96f;

        /// <summary>True for a lock member a lock holds dormant: it refuses every hit, so it is never a target.</summary>
        public static bool IsDormantMember(Node2D body) => body is EnemyController enemy && enemy.IsEncounterDormant;

        /// <summary>True for a woken member of <paramref name="encounterLock"/>.</summary>
        public static bool IsActiveMemberOf(Node2D body, EncounterLock encounterLock) =>
            encounterLock != null && body is EnemyController enemy && enemy.IsEncounterMember
            && !enemy.IsEncounterDormant && enemy.EncounterToken == encounterLock.GetInstanceId();

        /// <summary>The lock ID of a member body, or "" for a body no lock owns.</summary>
        public static string LockIdOf(Node2D body) =>
            body is EnemyController enemy && enemy.IsEncounterMember ? enemy.EncounterLockID ?? "" : "";

        /// <summary>True while an uncleared encounter lock holds this fracture shut (FIX-E): a strike only posts a notice.</summary>
        public static bool IsHeldByLock(CheckpointTrigger checkpoint) =>
            checkpoint != null && GodotObject.IsInstanceValid(checkpoint) && checkpoint.IsHeldByEncounterLock;

        /// <summary>A member's relocation counters (fall net + stall watchdog, and the fall net alone) since it last spawned.</summary>
        public static (int Relocations, int FallRecoveries) MemberRelocations(EnemyController enemy) =>
            (enemy.EncounterRelocations, enemy.EncounterFallRecoveries);

        /// <summary>The mob's place in its target's rotation (GU-F).</summary>
        public static StoryEngagementRules.EngagementRole EngagementRoleOf(EnemyController enemy) => enemy.EngagementRole;

        /// <summary>True when the mob's rotation is on <paramref name="hero"/>.</summary>
        public static bool EngagesHero(EnemyController enemy, PlayerController hero) =>
            hero != null && enemy.EngagementRole != StoryEngagementRules.EngagementRole.None
            && enemy.EngagementTargetId == hero.GetInstanceId();

        /// <summary>A waiter holding on the wait ring (in Chase, its next attack a melee one).</summary>
        public static bool IsRingingWaiter(EnemyController enemy) => enemy.IsRingingWaiter && !enemy.IsEngagementCrossing;

        /// <summary>The side of its aim a waiter rings (−1 left, +1 right).</summary>
        public static int RingSideOf(EnemyController enemy) => enemy.EngagementRingSide;

        /// <summary>The mob's horizontal gap to its aim, pivot to pivot.</summary>
        public static float EngagementGapOf(EnemyController enemy) => enemy.EngagementGapPixels;

        /// <summary>The class of the attack an Attacking mob has in flight (or is about to throw from its reaction delay).</summary>
        public static StoryEngagementRules.EngagementClass InFlightClassOf(EnemyController enemy) => enemy.EngagementInFlightClass;

        /// <summary>A pool spawn counter: the same instance at a new generation is a new body.</summary>
        public static int SpawnGenerationOf(EnemyController enemy) => enemy.SpawnGeneration;
        #endregion

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

        /// <summary>
        /// Nothing solid at all between two points: unlike <see cref="LineOfSight"/>,
        /// a breakable (a cell lock, a wedge), a prop (a boulder), a lock barrier
        /// or a low wall of cover counts. A straight walk-in at a foe stops at any
        /// of them, so the bot only walks in when this is clear (FIX-BOT, 2026-10-13).
        /// </summary>
        public bool ClearWalk(Vector2 from, Vector2 to) {
            PhysicsDirectSpaceState2D space = Player?.GetWorld2D()?.DirectSpaceState;
            if (space == null) return true;
            var query = PhysicsRayQueryParameters2D.Create(from, to, CollisionLayers.Environment);
            Godot.Collections.Dictionary hit = space.IntersectRay(query);
            using var lifetime = hit;
            return hit.Count == 0;
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

        /// <summary>
        /// The InteractionArea that would trigger <paramref name="target"/>: a child,
        /// or (FIX-BOT) a sibling aimed at it through its <c>TargetPath</c> —
        /// Florence's gears keep theirs beside them so the prompt does not turn
        /// with the gear.
        /// </summary>
        public static InteractionArea InteractionAreaOf(Node target) {
            if (target == null || !GodotObject.IsInstanceValid(target)) return null;
            Godot.Collections.Array<Node> children = target.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is InteractionArea area) return area;
            }
            Node parent = target.GetParent();
            if (parent == null) return null;
            Godot.Collections.Array<Node> siblings = parent.GetChildren();
            using var siblingsLifetime = siblings.AsDisposable();
            foreach (Node sibling in siblings) {
                if (sibling is InteractionArea area && area.TargetPath != null && !area.TargetPath.IsEmpty
                    && area.GetNodeOrNull<Node>(area.TargetPath) == target) return area;
            }
            return null;
        }

        // =================================================================
        // Gear trains (FIX-BOT, 2026-10-13)
        // =================================================================

        /// <summary>The puzzle a gear reports to, or null.</summary>
        public static PuzzleManager PuzzleOf(RotatingGear gear) =>
            gear == null || !GodotObject.IsInstanceValid(gear) || gear.PuzzleManagerPath == null || gear.PuzzleManagerPath.IsEmpty
                ? null
                : gear.GetNodeOrNull<PuzzleManager>(gear.PuzzleManagerPath);

        /// <summary>
        /// How many more presses each gear of an unsolved gear train needs, from
        /// the gears' current turns: one train per <see cref="PuzzleManager"/>,
        /// solved by <see cref="PlaytestPuzzleMath.GearPresses"/>. A press turns
        /// the gear and every gear it drives (<c>RotatingGear.Interact</c>), so the
        /// plan is recomputed from the live turns every time it is read — a death
        /// rewind that turns the gears back simply re-plans.
        /// </summary>
        public Dictionary<RotatingGear, int> GearPlan() {
            var plan = new Dictionary<RotatingGear, int>();
            var trains = new Dictionary<PuzzleManager, List<RotatingGear>>();
            foreach (RotatingGear gear in Gears) {
                if (!GodotObject.IsInstanceValid(gear) || !gear.IsInsideTree() || string.IsNullOrWhiteSpace(gear.ConditionID)) continue;
                PuzzleManager manager = PuzzleOf(gear);
                if (manager == null || manager.IsCompleted) continue;
                if (!trains.TryGetValue(manager, out List<RotatingGear> train)) trains[manager] = train = new List<RotatingGear>();
                train.Add(gear);
            }
            foreach (List<RotatingGear> train in trains.Values) {
                int count = train.Count;
                var turns = new int[count];
                var targets = new int[count];
                var links = new List<int>[count];
                for (int i = 0; i < count; i++) {
                    turns[i] = train[i].QuarterTurns;
                    targets[i] = train[i].TargetQuarterTurns;
                    links[i] = new List<int>();
                    foreach (NodePath path in train[i].LinkedGearPaths) {
                        int linked = train.IndexOf(train[i].GetNodeOrNull<RotatingGear>(path));
                        if (linked >= 0 && linked != i) links[i].Add(linked);
                    }
                }
                int[] presses = PlaytestPuzzleMath.GearPresses(turns, targets, links);
                if (presses == null) continue;
                for (int i = 0; i < count; i++) plan[train[i]] = presses[i];
            }
            return plan;
        }
    }

    /// <summary>Godot-free puzzle arithmetic the bots plan with (FIX-BOT, 2026-10-13).</summary>
    public static class PlaytestPuzzleMath {
        /// <summary>Larger trains are not searched (4^n combinations).</summary>
        public const int MaxGearTrain = 6;

        /// <summary>
        /// The fewest presses per gear that align a whole gear train: pressing gear
        /// <c>i</c> turns it and every gear in <paramref name="links"/>[i] one quarter
        /// turn forward, turns wrap at four, and a gear is aligned when its turns
        /// equal its target. Null when no combination aligns every gear, or the
        /// train is longer than <see cref="MaxGearTrain"/>.
        /// </summary>
        public static int[] GearPresses(int[] turns, int[] targets, IReadOnlyList<int>[] links) {
            int count = turns?.Length ?? 0;
            if (count == 0 || targets == null || targets.Length != count || links == null || links.Length != count
                || count > MaxGearTrain) return null;
            int[] best = null;
            int bestTotal = int.MaxValue;
            var presses = new int[count];
            var resulting = new int[count];
            int combinations = 1 << (2 * count);
            for (int code = 0; code < combinations; code++) {
                int total = 0;
                for (int i = 0; i < count; i++) {
                    presses[i] = (code >> (2 * i)) & 3;
                    total += presses[i];
                }
                if (total >= bestTotal) continue;
                for (int i = 0; i < count; i++) resulting[i] = turns[i] + presses[i];
                for (int i = 0; i < count; i++) {
                    if (links[i] == null) continue;
                    foreach (int linked in links[i]) {
                        if (linked >= 0 && linked < count && linked != i) resulting[linked] += presses[i];
                    }
                }
                bool aligned = true;
                for (int i = 0; i < count && aligned; i++) aligned = Wrap(resulting[i]) == Wrap(targets[i]);
                if (!aligned) continue;
                bestTotal = total;
                best = (int[])presses.Clone();
            }
            return best;
        }

        private static int Wrap(int turns) => ((turns % 4) + 4) % 4;
    }
}
