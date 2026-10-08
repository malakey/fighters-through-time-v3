using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;

namespace FTT.Diagnostics {

    public struct BotIntent {
        public float MoveX;
        public float MoveY;
        /// <summary>Buttons held this frame regardless of the previous frame.</summary>
        public GameplayButtons Held;
        /// <summary>Buttons that should produce a fresh press edge (dropped if already held).</summary>
        public GameplayButtons Tap;
    }

    /// <summary>
    /// Base class for scripted Story-mode playtest bots. A bot is an
    /// <see cref="IPlayerInputSource"/> installed on player 0 through
    /// <see cref="InputManager.SetInputSource"/> — the same seam the Mirror Paradox
    /// clone and the Calibration Drill dummy use — so it drives the real
    /// <see cref="PlayerController"/> exactly as a pad would. It never touches
    /// gameplay state directly; an Interact press is turned into the matching
    /// <c>InteractionArea.TryInteract</c> call by the runner, exactly as the raw
    /// key press would be.
    /// </summary>
    public abstract class PlaytestBot : IPlayerInputSource {
        protected readonly PlaytestWorld World;
        protected readonly NavAgent Agent;

        /// <summary>Label of what the bot is doing this frame; telemetry attributes damage to it.</summary>
        public string CurrentAction { get; protected set; } = "idle";

        /// <summary>What the bot is steering at this frame (enemy or objective), for the trace.</summary>
        public Node2D CurrentTarget { get; protected set; }

        /// <summary>Enemies the bot gave up on because it could not damage them.</summary>
        public int UnreachableTargets { get; protected set; }

        /// <summary>The interactable an Interact press this frame is aimed at (null = whatever is in range).</summary>
        public Node InteractTarget { get; protected set; }

        /// <summary>Buttons held / newly pressed on the latest sample.</summary>
        public GameplayButtons LastHeld { get; private set; }
        public GameplayButtons LastPressed { get; private set; }

        /// <summary>The navigation step being executed, for the trace.</summary>
        public string NavDescription => Agent?.Description ?? "";

        /// <summary>Navigation notes (rope releases and the like) for the report.</summary>
        public List<string> NavNotes => Agent.Notes;

        protected int Frame;

        /// <summary>How many input frames the bot has produced (the runner pairs presses with samples).</summary>
        public int SampleCount => Frame;

        protected PlaytestBot(PlaytestWorld world) {
            World = world;
            Agent = new NavAgent(world.Nav);
        }

        public abstract string Name { get; }

        public PlayerInputFrame Sample(uint tick, in PlayerInputFrame previousFrame) {
            Frame++;
            World.Tick();
            InteractTarget = null;
            PlayerController player = World.Player;
            if (player == null || player.CurrentState == CharacterState.Dead) {
                CurrentAction = "idle";
                LastHeld = GameplayButtons.None;
                LastPressed = GameplayButtons.None;
                Agent.Reset();
                return PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.None, previousFrame.Held);
            }

            var intent = new BotIntent();
            Decide(player, ref intent);
            Agent.NoteSentMoveX(intent.MoveX);

            GameplayButtons held = intent.Held;
            // A tap only registers as a press edge if the button was up last frame.
            held |= intent.Tap & ~previousFrame.Held;
            LastHeld = held;
            LastPressed = held & ~previousFrame.Held;
            return PlayerInputFrame.Create(tick, Mathf.Clamp(intent.MoveX, -1f, 1f), Mathf.Clamp(intent.MoveY, -1f, 1f), held, previousFrame.Held);
        }

        protected abstract void Decide(PlayerController player, ref BotIntent intent);

        /// <summary>Face the target without walking into it: a 1-frame nudge only when turned away.</summary>
        protected static void FaceTarget(PlayerController player, Node2D target, ref BotIntent intent) {
            bool targetRight = target.GlobalPosition.X > player.GlobalPosition.X;
            if (targetRight != player.IsFacingRight) intent.MoveX = targetRight ? 1f : -1f;
        }

        protected static void FaceX(PlayerController player, float x, ref BotIntent intent) {
            bool right = x > player.GlobalPosition.X;
            if (right != player.IsFacingRight) intent.MoveX = right ? 1f : -1f;
        }
    }

    public enum FightStyle {
        /// <summary>Never attacks an enemy unless the run cannot progress otherwise (bosses, a blocker).</summary>
        Bypass,
        /// <summary>Mashes the 3-hit string, then Special 1 and Special 2 as soon as they are ready. Never defends.</summary>
        Spam,
        /// <summary>Basic string only; blocks enemy swings.</summary>
        Basics,
        /// <summary>A competent player: string + specials when ready + Ultimate at full meter, blocks swings, avoids projectiles.</summary>
        Play
    }

    /// <summary>
    /// The Story campaign bot. Every frame it picks one job, in priority order:
    /// seal an armed anchor; get out of a hot hazard; outrun an escape front;
    /// fight (its <see cref="FightStyle"/>); work the nearest reachable level
    /// task (strike a checkpoint, rescue, press the expected glyph, turn a coil,
    /// weigh a plate, break a puzzle breakable); otherwise travel toward the
    /// boss arena. Movement always goes through the <see cref="NavAgent"/>.
    /// </summary>
    public sealed class StoryBot : PlaytestBot, PlaytestRouteHints.IHintDriver {
        int PlaytestRouteHints.IHintDriver.Frame => Frame;

        bool PlaytestRouteHints.IHintDriver.GoTo(PlayerController player, Vector2 goal, float arriveRadius, float maxBelow, ref BotIntent intent) =>
            Go(player, goal, ref intent, arriveRadius, maxBelow);

        private readonly FightStyle _style;
        private readonly PlaytestRouteHints _hints;

        public StoryBot(PlaytestWorld world, FightStyle style, CampaignLevel level) : base(world) {
            _style = style;
            _hints = PlaytestRouteHints.For(level);
        }

        public FightStyle Style => _style;
        public override string Name => _style switch {
            FightStyle.Bypass => "bypass",
            FightStyle.Spam => "spam",
            FightStyle.Basics => "basics",
            _ => "play"
        };

        private bool Fights => _style != FightStyle.Bypass;
        private bool Defends => _style is FightStyle.Basics or FightStyle.Play;

        protected override void Decide(PlayerController player, ref BotIntent intent) {
            CurrentTarget = null;
            UpdateSwingCounter(player);

            if (player.CurrentState == CharacterState.LedgeHanging) {
                // On a swinging rope the agent times the release toward the
                // route (Level 7's rigging crossing, or a rope caught by
                // accident mid-jump).
                CurrentAction = "rope";
                if (Agent.HandleHang(player, ref intent)) return;
                // A static ledge: jump off it at once and keep flying the route.
                CurrentAction = "ledge";
                // (No horizontal input: toward-stage input would "pull up" onto
                // a swinging rope's stand anchor in mid-air.)
                if ((Frame & 1) == 0) intent.Tap |= GameplayButtons.Jump;
                return;
            }

            bool done = WorkSealAnchor(player, ref intent)
                || EvadeHazards(player, ref intent)
                || OutrunEscape(player, ref intent)
                || CollectHealing(player, ref intent)
                || FightNearby(player, ref intent)
                || WorkTasks(player, ref intent)
                || WorkRouteHints(player, ref intent);
            if (!done) Travel(player, ref intent);
            FilterHazards(player, ref intent);
        }

        // =================================================================
        // Seal
        // =================================================================

        private bool WorkSealAnchor(PlayerController player, ref BotIntent intent) {
            TemporalCoreAnchor anchor = World.ArmedSealAnchor();
            if (anchor == null) return false;
            // Not reachable yet (a gate still stands between): the level tasks come first.
            if (!Reachable(player, anchor.GlobalPosition, 200f)) return false;
            // Defend first if something is still swinging nearby.
            if (FightNearby(player, ref intent, 260f)) return true;
            CurrentTarget = anchor;
            return GoInteract(player, anchor, ref intent, "seal", 40f);
        }

        // =================================================================
        // Hazards
        // =================================================================

        private float _travelDir = 1f;

        private bool EvadeHazards(PlayerController player, ref BotIntent intent) {
            Vector2 feet = player.GlobalPosition;
            if (!player.IsOnFloor()) return false;
            ChronalExtractor extractor = World.TelegraphingExtractorNear(feet, DischargeSafeRadius);
            if (extractor != null) {
                CurrentAction = "evade_discharge";
                CurrentTarget = extractor;
                intent.MoveX = feet.X >= extractor.GlobalPosition.X ? 1f : -1f;
                return true;
            }
            foreach (PlaytestWorld.HazardZone zone in World.LiveCyclicHazards()) {
                Rect2 r = zone.Area;
                bool overlap = feet.X + 20f > r.Position.X && feet.X - 20f < r.End.X && feet.Y > r.Position.Y && feet.Y - 64f < r.End.Y;
                if (!overlap) continue;
                HazardPhase phase = zone.Hazard.Phase;
                float remaining = World.PhaseRemaining(zone.Hazard);
                bool hot = phase is HazardPhase.Warning or HazardPhase.Active
                    || (phase == HazardPhase.Cooldown && remaining < 0.45f);
                if (!hot || zone.Hazard.IsSheltered(feet)) continue;
                float exitLeft = feet.X - (r.Position.X - 44f);
                float exitRight = (r.End.X + 44f) - feet.X;
                float dir = exitLeft < exitRight ? -1f : 1f;
                if (Mathf.Abs(exitLeft - exitRight) < 50f) dir = _travelDir;
                CurrentAction = "evade_hazard";
                intent.MoveX = dir;
                return true;
            }
            float exposure = World.SearchlightExposure(player, out SearchlightZone light);
            if (exposure > 0.55f && light != null) {
                CurrentAction = "evade_light";
                intent.MoveX = _travelDir;
                return true;
            }
            return false;
        }

        /// <summary>Extractor discharge circle (190) plus the hero's half-body, measured to the chest.</summary>
        private const float DischargeSafeRadius = 222f;

        /// <summary>Never walk into a hazard column that will be hot while crossing it.</summary>
        private void FilterHazards(PlayerController player, ref BotIntent intent) {
            if (!player.IsOnFloor() || Mathf.Abs(intent.MoveX) < 0.1f) return;
            if (CurrentAction is "evade_hazard" or "evade_discharge") return;
            Vector2 feet = player.GlobalPosition;
            float dir = Mathf.Sign(intent.MoveX);
            float speed = Mathf.Max(120f, World.Nav.Hero.RunSpeed);
            // Extractors: do not step into a discharge circle that is (about to be) live.
            Vector2 chest = feet + new Vector2(0f, -32f);
            foreach (ChronalExtractor extractor in World.Extractors) {
                if (!GodotObject.IsInstanceValid(extractor) || extractor.IsDestroyed || extractor.IsSealedShutdown || extractor is ResonanceHoldNode) continue;
                float now = chest.DistanceTo(extractor.GlobalPosition);
                float next = (chest + new Vector2(dir * 14f, 0f)).DistanceTo(extractor.GlobalPosition);
                if (now < DischargeSafeRadius || next >= DischargeSafeRadius || next >= now) continue;
                float safe = World.ExtractorSafeRemaining(extractor);
                bool live = extractor.IsTelegraphing || (!float.IsNaN(safe) && safe < 1.8f);
                if (!live) continue;
                intent.MoveX = 0f;
                intent.Held &= ~GameplayButtons.Jump;
                CurrentAction = "wait_discharge";
                return;
            }
            foreach (PlaytestWorld.HazardZone zone in World.LiveCyclicHazards()) {
                Rect2 r = zone.Area;
                if (!(feet.Y > r.Position.Y && feet.Y - 64f < r.End.Y)) continue;
                float near = dir > 0 ? r.Position.X - (feet.X + 20f) : (feet.X - 20f) - r.End.X;
                if (near < -1f || near > 70f) continue; // inside (handled above) or not adjacent
                HazardPhase phase = zone.Hazard.Phase;
                float remaining = World.PhaseRemaining(zone.Hazard);
                float cross = (near + r.Size.X + 50f) / speed + 0.25f;
                bool safe = phase == HazardPhase.Cooldown && remaining > cross;
                if (safe || zone.Hazard.IsSheltered(feet + new Vector2(dir * (near + 20f), 0f))) continue;
                intent.MoveX = near < 24f ? -dir * 0.5f : 0f;
                intent.Held &= ~GameplayButtons.Jump;
                CurrentAction = "wait_hazard";
                return;
            }
        }

        private bool OutrunEscape(PlayerController player, ref BotIntent intent) {
            EscapeSequenceController escape = World.RunningEscape();
            if (escape == null) return false;
            _travelDir = 1f;
            CurrentAction = "escape";
            Vector2 goal = new(escape.FinishX + 80f, player.GlobalPosition.Y - 10f);
            // Only stop to fight something standing right in the way.
            Node2D blocker = NearestFoe(player, 90f, ahead: true);
            if (blocker != null && player.GlobalPosition.X - escape.FrontX > 260f) {
                CurrentTarget = blocker;
                FaceTarget(player, blocker, ref intent);
                if (Frame % 6 == 0) intent.Tap |= GameplayButtons.BasicAttack;
                return true;
            }
            Go(player, goal, ref intent, 20f, 400f);
            return true;
        }

        // =================================================================
        // Healing
        // =================================================================

        private bool CollectHealing(PlayerController player, ref BotIntent intent) {
            if (player.CurrentHP >= player.MaximumHP * 0.75f) return false;
            StoryPickup best = null;
            float bestDistance = 520f;
            foreach (StoryPickup pickup in World.HealingPickups()) {
                float distance = pickup.GlobalPosition.DistanceTo(player.GlobalPosition);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = pickup;
                }
            }
            if (best == null) return false;
            CurrentAction = "heal";
            CurrentTarget = best;
            Go(player, best.GlobalPosition, ref intent, 12f, 300f);
            return true;
        }

        // =================================================================
        // Combat
        // =================================================================

        private const float EngageRadius = 620f;
        private const float BossEngageRadius = 1500f;
        private const float StrikeRange = 92f;
        private const int TargetGiveUpFrames = 720;
        private readonly Dictionary<ulong, int> _ignoredUntil = new();
        private ulong _targetId;
        private int _targetLastHP;
        private int _targetProgressFrame;

        private enum Phase { Idle, String, Specials }
        private Phase _phase = Phase.Idle;
        private int _phaseStart;
        private int _swingsSeen;
        private bool _lastHitboxActive;
        private bool _special1Done, _special2Done;
        /// <summary>
        /// The meter node is what the Ultimate cast reads; the controller's
        /// public field can lag it (it is not resynced after a cast).
        /// </summary>
        private static bool MeterFull(PlayerController player) =>
            player.GetNodeOrNull<UltimateMeter>("UltimateMeter")?.IsFull ?? player.CurrentUltimateMeter >= 100f;

        private int _blockFrames;
        private int _ultimateTapFrames;
        private int _ultimateRefusedUntil;
        private int _airStrikeFrame = -1000;

        private void UpdateSwingCounter(PlayerController player) {
            bool hitboxActive = player.GetNodeOrNull<FTT.Combat.Hitbox>("MeleeHitbox")?.IsActive ?? false;
            if (hitboxActive && !_lastHitboxActive) _swingsSeen++;
            _lastHitboxActive = hitboxActive;
        }

        private bool Ignored(Node2D body) =>
            _ignoredUntil.TryGetValue(body.GetInstanceId(), out int until) && until > Frame;

        private Node2D NearestFoe(PlayerController player, float radius, bool ahead = false) {
            Node2D best = null;
            float bestDistance = radius;
            Vector2 at = player.GlobalPosition + new Vector2(0f, -32f);
            foreach (Node2D foe in World.Foes()) {
                Vector2 delta = foe.GlobalPosition - player.GlobalPosition;
                if (ahead && Mathf.Sign(delta.X) != Mathf.Sign(_travelDir)) continue;
                float distance = at.DistanceTo(foe.GlobalPosition + new Vector2(0f, -30f));
                if (distance < bestDistance && Mathf.Abs(delta.Y) < 120f) {
                    bestDistance = distance;
                    best = foe;
                }
            }
            return best;
        }

        /// <summary>The foe to fight now, or null. Bosses are never given up on.</summary>
        private Node2D SelectTarget(PlayerController player, float radiusOverride, out bool attacking) {
            attacking = false;
            Node2D best = null;
            float bestScore = float.MaxValue;
            Vector2 chest = player.GlobalPosition + new Vector2(0f, -36f);
            foreach (Node2D foe in World.Foes()) {
                bool bossLike = PlaytestWorld.IsBossLike(foe);
                if (foe is BossController boss && (boss.IsMechanicInvulnerable || boss.IsHistoricalRecoverySuspended)) {
                    // Guarded: fight the guardians (they are foes too), not the boss.
                    if (boss.IsGuarded) continue;
                }
                if (!bossLike && Ignored(foe)) continue;
                if (_style == FightStyle.Bypass && !bossLike) continue;
                float radius = radiusOverride > 0f ? radiusOverride : bossLike ? BossEngageRadius : EngageRadius;
                Vector2 center = foe.GlobalPosition + new Vector2(0f, -30f);
                float distance = chest.DistanceTo(center);
                if (distance > radius) continue;
                if (distance > 110f && !World.LineOfSight(chest, center)) continue;
                // Far above with nothing to stand on under it: not reachable now.
                float dy = player.GlobalPosition.Y - foe.GlobalPosition.Y;
                if (dy > 330f && World.Nav.SegmentUnder(foe.GlobalPosition, 360f) == null) continue;
                float score = distance + (PlaytestWorld.IsAttacking(foe) ? -80f : 0f) + Mathf.Max(0f, dy - 120f);
                if (foe is ArenaGuardian) score -= 200f;
                if (score < bestScore) {
                    bestScore = score;
                    best = foe;
                    attacking = PlaytestWorld.IsAttacking(foe);
                }
            }
            return best;
        }

        private bool FightNearby(PlayerController player, ref BotIntent intent) => FightNearby(player, ref intent, 0f);

        private bool FightNearby(PlayerController player, ref BotIntent intent, float radiusOverride) {
            Node2D target = SelectTarget(player, radiusOverride, out bool enemyAttacking);
            if (target == null) {
                _phase = Phase.Idle;
                return false;
            }
            bool bossLike = PlaytestWorld.IsBossLike(target);
            int targetHP = PlaytestWorld.HealthOf(target);
            if (target.GetInstanceId() != _targetId) {
                _targetId = target.GetInstanceId();
                _targetLastHP = targetHP;
                _targetProgressFrame = Frame;
            } else if (targetHP < _targetLastHP) {
                _targetLastHP = targetHP;
                _targetProgressFrame = Frame;
            } else if (!bossLike && Frame - _targetProgressFrame > TargetGiveUpFrames) {
                // A player who cannot reach an enemy moves on; record it so the
                // report can tell "skipped by choice" from "skipped by necessity".
                _ignoredUntil[_targetId] = Frame + 1500;
                UnreachableTargets++;
                _phase = Phase.Idle;
                return false;
            }
            CurrentTarget = target;
            Fight(player, target, enemyAttacking, ref intent);
            return true;
        }

        private void Fight(PlayerController player, Node2D target, bool enemyAttacking, ref BotIntent intent) {
            Vector2 feet = player.GlobalPosition;
            Vector2 delta = target.GlobalPosition - feet;
            float distance = Mathf.Abs(delta.X);
            bool busy = player.CurrentState is CharacterState.Attacking or CharacterState.UsingSpecial or CharacterState.UsingUltimate;
            bool lowHP = player.CurrentHP < player.MaximumHP * 0.3f;

            // Defence (Basics and Play): hold Block through an enemy swing, roll out
            // when the shield is empty; Play also answers incoming projectiles.
            if (Defends && !busy && player.IsOnFloor()) {
                bool threat = enemyAttacking && distance < 240f && Mathf.Abs(delta.Y) < 140f;
                if (_style == FightStyle.Play && !threat) threat = World.ProjectileIncoming(player, 260f);
                if (threat && _blockFrames < 80) {
                    _blockFrames++;
                    if (player.CurrentBlockCharges > 0) {
                        CurrentAction = "block";
                        FaceTarget(player, target, ref intent);
                        intent.Held |= GameplayButtons.Block;
                    } else {
                        CurrentAction = "evade";
                        intent.MoveX = Mathf.Sign(feet.X - target.GlobalPosition.X);
                        if (intent.MoveX == 0f) intent.MoveX = -_travelDir;
                        intent.Tap |= GameplayButtons.Roll;
                    }
                    return;
                }
                if (!threat) _blockFrames = 0;
            }

            if (player.CurrentState == CharacterState.UsingUltimate) _ultimateTapFrames = 0;
            if (_style == FightStyle.Play && MeterFull(player) && player.IsAbilityUnlocked(AbilitySlot.Ultimate) && !busy && distance < 320f && Mathf.Abs(delta.Y) < 200f
                && Frame >= _ultimateRefusedUntil) {
                // A press that never starts the cast is refused by the game:
                // record it once and fight on instead of pressing forever.
                if (++_ultimateTapFrames > 40) {
                    _ultimateTapFrames = 0;
                    _ultimateRefusedUntil = Frame + 600;
                    BaseSpecial ultimate = player.GetNodeOrNull<BaseSpecial>("Ultimate");
                    Agent.Notes.Add($"ultimate refused at {Mathf.RoundToInt(player.GlobalPosition.X)},{Mathf.RoundToInt(player.GlobalPosition.Y)}: meter={player.CurrentUltimateMeter:0.##}/node={player.GetNodeOrNull<UltimateMeter>("UltimateMeter")?.CurrentValue.ToString("0.##") ?? "none"} state={player.CurrentState} phase={ultimate?.CurrentPhase.ToString() ?? "none"} statuses={player.ActiveStatuses.Damage.Type}+{player.ActiveStatuses.Control.Type} frozen={player.TimeFrozen}");
                } else {
                    CurrentAction = "ultimate";
                    intent.Tap |= GameplayButtons.Ultimate;
                    return;
                }
            }

            // Low HP (Play): give ground while the enemy is mid-swing.
            if (_style == FightStyle.Play && lowHP && enemyAttacking && distance < 200f && !busy && player.CurrentBlockCharges == 0) {
                CurrentAction = "retreat";
                intent.MoveX = Mathf.Sign(feet.X - target.GlobalPosition.X);
                return;
            }

            // Above us: jump and up-attack (drones, platform shooters).
            float above = -delta.Y; // positive: target above the feet
            if (above > 95f && above < 330f && distance < 150f) {
                AirStrike(player, target, ref intent);
                return;
            }

            if (_phase == Phase.Idle) {
                bool inRange = distance <= StrikeRange && Mathf.Abs(delta.Y) < 80f;
                if (!inRange) {
                    CurrentAction = "approach";
                    float side = feet.X <= target.GlobalPosition.X ? -1f : 1f;
                    Vector2 standAt = new(target.GlobalPosition.X + side * 60f, target.GlobalPosition.Y);
                    if (Mathf.Abs(delta.Y) < 60f && distance < 300f && player.IsOnFloor()) {
                        // Same level and close: just walk in.
                        intent.MoveX = Mathf.Sign(delta.X);
                    } else {
                        Go(player, standAt, ref intent, 30f, 300f);
                    }
                    return;
                }
                _phase = Phase.String;
                _phaseStart = Frame;
                _swingsSeen = 0;
                _special1Done = _special2Done = false;
            }

            FaceTarget(player, target, ref intent);
            int phaseFrame = Frame - _phaseStart;
            if (_phase == Phase.String) {
                CurrentAction = "string";
                if (phaseFrame % 6 == 0) intent.Tap |= GameplayButtons.BasicAttack;
                bool hitboxActive = player.GetNodeOrNull<FTT.Combat.Hitbox>("MeleeHitbox")?.IsActive ?? false;
                bool stringDone = (_swingsSeen >= 3 && !hitboxActive) || phaseFrame > 130;
                if (stringDone) {
                    _phase = _style is FightStyle.Basics or FightStyle.Bypass ? Phase.Idle : Phase.Specials;
                    _phaseStart = Frame;
                }
                // Target slid out of range mid-string: chase.
                if (distance > StrikeRange + 70f) _phase = Phase.Idle;
                return;
            }

            // Phase.Specials: each Special as soon as it is off cooldown and the
            // hero is free to cast; a Special cancels a swing, so Spam does not wait.
            CurrentAction = "specials";
            bool free = player.CurrentState != CharacterState.UsingSpecial;
            // The campaign kit (Legacy Unlock Schedule) may not have restored a slot yet.
            if (!player.IsAbilityUnlocked(AbilitySlot.Special1)) _special1Done = true;
            if (!player.IsAbilityUnlocked(AbilitySlot.Special2)) _special2Done = true;
            if (_special1Done && _special2Done) {
                _phase = Phase.Idle;
                return;
            }
            if (!_special1Done && free && player.SpecialOneCooldownTimer <= 0f) {
                intent.Tap |= GameplayButtons.Special1;
                _special1Done = true;
                CurrentAction = "special1";
            } else if (_special1Done && !_special2Done && free && phaseFrame > 4 && player.SpecialTwoCooldownTimer <= 0f) {
                intent.Tap |= GameplayButtons.Special2;
                _special2Done = true;
                CurrentAction = "special2";
            } else if ((_special1Done || player.SpecialOneCooldownTimer > 0f)
                && (_special2Done || player.SpecialTwoCooldownTimer > 0f) && free) {
                _phase = Phase.Idle;
            } else if (phaseFrame > 120) {
                _phase = Phase.Idle;
            }
            if (player.SpecialOneCooldownTimer > 0f && !_special1Done) _special1Done = true;
        }

        /// <summary>Jump under a target overhead and swing the up-attack near the top of the jump.</summary>
        private void AirStrike(PlayerController player, Node2D target, ref BotIntent intent) {
            CurrentAction = "air_strike";
            Vector2 feet = player.GlobalPosition;
            float dx = target.GlobalPosition.X - feet.X;
            if (player.IsOnFloor()) {
                if (Mathf.Abs(dx) > 40f) {
                    intent.MoveX = Mathf.Sign(dx);
                    return;
                }
                if (Frame - _airStrikeFrame > 50) {
                    _airStrikeFrame = Frame;
                    intent.Held |= GameplayButtons.Jump;
                }
                return;
            }
            intent.MoveX = Mathf.Abs(dx) > 20f ? Mathf.Sign(dx) * 0.6f : 0f;
            int t = Frame - _airStrikeFrame;
            if (t < 26) intent.Held |= GameplayButtons.Jump;
            else if (t == 27 && feet.Y - target.GlobalPosition.Y > 160f && player.RemainingJumps > 0) intent.Held |= GameplayButtons.Jump;
            float gap = feet.Y - 150f - target.GlobalPosition.Y;
            if (gap < 40f && player.Velocity.Y > -260f) {
                intent.MoveY = -1f;
                if (Frame % 4 == 0) intent.Tap |= GameplayButtons.BasicAttack;
            }
        }

        // =================================================================
        // Level tasks
        // =================================================================

        private Node2D _task;
        private string _taskKind = "";
        private int _taskStart;
        private int _taskLastProgress;
        private int _taskProgressValue;
        private float _taskBestDistance = float.MaxValue;
        private readonly Dictionary<ulong, int> _taskBlockedUntil = new();
        private int _interactFrame = -100;
        private int _lastUnblockFrame = -1000;

        private bool TaskBlocked(Node node) =>
            _taskBlockedUntil.TryGetValue(node.GetInstanceId(), out int until) && until > Frame;

        private void BlockTask(Node node, int frames) {
            if (node != null && GodotObject.IsInstanceValid(node)) _taskBlockedUntil[node.GetInstanceId()] = Frame + frames;
        }

        private bool WorkTasks(PlayerController player, ref BotIntent intent) {
            bool currentValid = _task != null && GodotObject.IsInstanceValid(_task) && !TaskDone(_task) && !TaskBlocked(_task);
            if (Frame % 20 == 0 || !currentValid) {
                (Node2D task, string kind) = PickTask(player);
                // Hysteresis: keep working the current task unless the new one is clearly closer.
                if (currentValid && task != null && task != _task
                    && TaskScore(player, task) > TaskScore(player, _task) - 250f) {
                    task = _task;
                    kind = _taskKind;
                }
                if (task != _task) {
                    _task = task;
                    _taskKind = kind;
                    _taskStart = Frame;
                    _taskLastProgress = Frame;
                    _taskProgressValue = TaskProgress(task);
                    _taskBestDistance = float.MaxValue;
                }
            }
            if (_task == null) return false;
            // Give up (for a while) on a task that is not progressing: its
            // state does not change and the hero is not getting any closer.
            int progress = TaskProgress(_task);
            float distance = player.GlobalPosition.DistanceTo(_task.GlobalPosition);
            if (progress != _taskProgressValue || distance < _taskBestDistance - 40f) {
                _taskProgressValue = progress;
                _taskBestDistance = Mathf.Min(_taskBestDistance, distance);
                _taskLastProgress = Frame;
            } else if (Frame - _taskLastProgress > 900) {
                BlockTask(_task, 1800);
                _task = null;
                return false;
            }
            CurrentTarget = _task;
            switch (_task) {
                case CheckpointTrigger checkpoint:
                    StrikeRect(player, new Rect2(checkpoint.GlobalPosition + new Vector2(-30f, -120f), new Vector2(60f, 120f)), ref intent, "checkpoint");
                    return true;
                case SequenceGlyph glyph:
                    return GoInteract(player, glyph, ref intent, "glyph", 22f);
                case ConductiveCoil coil:
                    return GoInteract(player, coil, ref intent, "coil", 26f, 30);
                case RescuableNPC npc:
                    return GoInteract(player, npc, ref intent, "rescue", 26f);
                case PressurePlate plate: {
                    CurrentAction = "plate";
                    Vector2 standOn = PlatePoint(player, plate) ?? plate.GlobalPosition;
                    Go(player, standOn, ref intent, 6f, 60f);
                    return true;
                }
                case DamageableEnvironmentObject breakable: {
                    Rect2? area = PlaytestWorld.ShapeRect(breakable);
                    if (area == null) {
                        BlockTask(breakable, 600);
                        _task = null;
                        return false;
                    }
                    StrikeRect(player, area.Value, ref intent, breakable is ShieldGeneratorTower ? "tower" : "break");
                    return true;
                }
            }
            return false;
        }

        private static float TaskScore(PlayerController player, Node2D task) {
            Vector2 delta = task.GlobalPosition - player.GlobalPosition;
            return Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) * 1.5f;
        }

        private bool TaskDone(Node2D task) => task switch {
            CheckpointTrigger c => c.IsActivated,
            SequenceGlyph g => g.IsLit || g.Lock?.IsCompleted == true || g.Lock?.ExpectedGlyph != g,
            RescuableNPC n => n.IsRescued,
            DamageableEnvironmentObject d => d.IsDestroyed,
            ConductiveCoil coil => _coilPlan == null || !_coilPlan.TryGetValue(coil, out int turns) || coil.QuarterTurns == turns,
            PressurePlate plate => plate.ResolvePuzzleManager()?.IsCompleted != false
                || plate.CurrentWeight + plate.PlayerWeight + 0.001f < plate.RequiredWeight,
            _ => false
        };

        private static int TaskProgress(Node2D task) => task switch {
            DamageableEnvironmentObject d => d.CurrentHP * 10 + d.HitCount,
            ConductiveCoil c => c.QuarterTurns,
            SequenceGlyph g => g.Lock?.Progress ?? 0,
            _ => 0
        };

        private Dictionary<ConductiveCoil, int> _coilPlan;
        private int _coilPlanFrame = -1000;

        private (Node2D, string) PickTask(PlayerController player) {
            Vector2 feet = player.GlobalPosition;
            Node2D best = null;
            string bestKind = "";
            float bestScore = float.MaxValue;

            void Consider(Node2D node, string kind, float bias, float maxDistance) {
                if (node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree() || TaskBlocked(node)) return;
                Vector2 delta = node.GlobalPosition - feet;
                float score = Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) * 1.5f + bias;
                if (Mathf.Abs(delta.X) > maxDistance || score >= bestScore) return;
                bool reachable;
                if (node is DamageableEnvironmentObject && PlaytestWorld.ShapeRect(node) is Rect2 r) {
                    // A breakable occupies its own floor: stand beside it, or under it.
                    reachable = Reachable(player, new Vector2(r.Position.X - 20f, r.End.Y - 2f), 46f)
                        || Reachable(player, new Vector2(r.End.X + 20f, r.End.Y - 2f), 46f)
                        || Reachable(player, new Vector2(r.GetCenter().X, r.End.Y + 2f), JumpStrikeReach - 10f)
                        || Reachable(player, new Vector2(r.Position.X - 36f, r.Position.Y - 110f), 110f)
                        || Reachable(player, new Vector2(r.End.X + 36f, r.Position.Y - 110f), 110f);
                } else {
                    reachable = Reachable(player, node.GlobalPosition, kind == "checkpoint" ? 120f : 320f);
                }
                if (!reachable) return;
                bestScore = score;
                best = node;
                bestKind = kind;
            }

            foreach (CheckpointTrigger checkpoint in World.Checkpoints) {
                if (!GodotObject.IsInstanceValid(checkpoint) || checkpoint.IsActivated || checkpoint.Inert || checkpoint.SelfActivating) continue;
                Consider(checkpoint, "checkpoint", -200f, 1100f);
            }
            foreach (RescuableNPC npc in World.Rescuables) {
                if (!GodotObject.IsInstanceValid(npc) || npc.IsRescued || npc.IsDespawned) continue;
                Consider(npc, "rescue", 0f, 1600f);
            }
            foreach (SequenceLock sequenceLock in World.SequenceLocks) {
                if (!GodotObject.IsInstanceValid(sequenceLock) || sequenceLock.IsCompleted) continue;
                Consider(sequenceLock.ExpectedGlyph, "glyph", 0f, 2400f);
            }
            if (World.Coils.Count > 0) {
                if (Frame - _coilPlanFrame > 240 || _coilPlan == null) {
                    _coilPlan = World.CoilSolution();
                    _coilPlanFrame = Frame;
                }
                bool receiverPowered = false;
                foreach (BeamReceiver receiver in World.Receivers) if (GodotObject.IsInstanceValid(receiver) && receiver.IsPowered) receiverPowered = true;
                if (!receiverPowered) {
                    foreach (var pair in _coilPlan) {
                        if (GodotObject.IsInstanceValid(pair.Key) && pair.Key.QuarterTurns != pair.Value) Consider(pair.Key, "coil", 0f, 2400f);
                    }
                }
            }
            foreach (PressurePlate plate in World.Plates) {
                if (!GodotObject.IsInstanceValid(plate) || plate.IsPressed || !plate.AcceptsPlayerOccupancy) continue;
                PuzzleManager manager = plate.ResolvePuzzleManager();
                if (manager == null || manager.IsCompleted) continue;
                if (plate.CurrentWeight + plate.PlayerWeight + 0.001f < plate.RequiredWeight) continue;
                if (PlatePoint(player, plate) == null) continue;
                Consider(plate, "plate", -100f, 2000f);
            }
            foreach (DamageableEnvironmentObject breakable in World.Breakables) {
                if (!GodotObject.IsInstanceValid(breakable) || breakable.IsDestroyed || breakable is ChronalExtractor) continue;
                bool puzzle = breakable is ShieldGeneratorTower || breakable.IsInGroup("puzzle_object");
                if (!puzzle) continue;
                Consider(breakable, breakable is ShieldGeneratorTower ? "tower" : "break", 0f, 2600f);
            }
            return (best, bestKind);
        }

        /// <summary>
        /// A reachable place to stand that overlaps a pressure plate: its
        /// centre, or either end when a prop sits in the middle of it.
        /// </summary>
        private Vector2? PlatePoint(PlayerController player, PressurePlate plate) {
            if (PlaytestWorld.ShapeRect(plate) is not Rect2 rect) return plate.GlobalPosition;
            float y = rect.End.Y - 1f;
            foreach (float x in new[] { rect.GetCenter().X, rect.Position.X + 4f, rect.End.X - 4f, rect.Position.X - 12f, rect.End.X + 12f }) {
                var point = new Vector2(x, y);
                if (Reachable(player, point, 40f)) return point;
            }
            return null;
        }

        private readonly Dictionary<long, (int Frame, bool Ok)> _reachCache = new();

        private bool Reachable(PlayerController player, Vector2 point, float maxBelow) {
            long key = ((long)Mathf.RoundToInt(point.X / 16f) << 32) ^ (uint)Mathf.RoundToInt(point.Y / 16f);
            if (_reachCache.TryGetValue(key, out var cached) && Frame - cached.Frame < 120) return cached.Ok;
            NavSegment here = World.Nav.Locate(player) ?? Agent.Current;
            bool ok;
            if (here == null) {
                ok = true; // airborne: optimistic, re-evaluated shortly
            } else {
                PlaytestNav.Plan plan = World.Nav.FindPath(here, player.GlobalPosition.X, point, maxBelow);
                ok = plan.ReachesGoal;
            }
            _reachCache[key] = (Frame, ok);
            return ok;
        }

        /// <summary>Walk next to an interactable and press Interact (the runner routes the press to its InteractionArea).</summary>
        private bool GoInteract(PlayerController player, Node2D target, ref BotIntent intent, string action, float radius, int pressEvery = 14) {
            CurrentAction = action;
            Vector2 feet = player.GlobalPosition;
            InteractionArea area = PlaytestWorld.InteractionAreaOf(target);
            bool inside = area != null && GodotObject.IsInstanceValid(area) && area.OverlapsBody(player);
            if (inside && player.IsOnFloor()) {
                if (Frame - _interactFrame >= pressEvery) {
                    _interactFrame = Frame;
                    intent.Tap |= GameplayButtons.Interact;
                    InteractTarget = target;
                }
                return true;
            }
            Go(player, target.GlobalPosition, ref intent, radius, 300f);
            if (area == null && Mathf.Abs(target.GlobalPosition.X - feet.X) < 60f && Frame - _interactFrame >= pressEvery) {
                _interactFrame = Frame;
                intent.Tap |= GameplayButtons.Interact;
                InteractTarget = target;
            }
            return true;
        }

        /// <summary>
        /// Hit a world rectangle (a breakable, a checkpoint's strike surface):
        /// the grounded string when it overlaps the standing swing, the
        /// grounded up-attack when it is just overhead, a jumping up-attack
        /// when it is higher, the down-air when the hero stands above it.
        /// Otherwise walk to a reachable stand point — beside it, under it,
        /// or on a surface above it, nearest first.
        /// </summary>
        private void StrikeRect(PlayerController player, Rect2 area, ref BotIntent intent, string action) {
            CurrentAction = action;
            Vector2 feet = player.GlobalPosition;
            float centerX = area.GetCenter().X;
            bool grounded = player.IsOnFloor();
            float gapLeft = area.Position.X - feet.X;   // > 0: hero left of it
            float gapRight = feet.X - area.End.X;       // > 0: hero right of it
            bool overlapsX = gapLeft < 0f && gapRight < 0f;
            int t = Frame - _airStrikeFrame;

            if (!grounded && t < 90 && _airStrikeArea == area) {
                // An airborne strike in progress.
                if (_airStrikeDown) {
                    // A full jump, then the down-air at the apex: the spike
                    // dives, and its startup must finish before it lands
                    // (Down only on the press frame: held Down would fast-fall).
                    if (player.Velocity.Y < -40f || t < 3) intent.Held |= GameplayButtons.Jump;
                    if (!_airStrikeSwung && t > 2 && player.Velocity.Y > -40f) {
                        // The down-air reads the Down *button* on the press frame.
                        intent.MoveY = 1f;
                        intent.Held |= GameplayButtons.Down;
                        intent.Tap |= GameplayButtons.BasicAttack;
                        _airStrikeSwung = true;
                    }
                    intent.MoveX = 0f;
                } else {
                    // Full jump to the apex; a second full jump when the first
                    // apex is still short; the up-attack once the box reaches.
                    bool reaches = feet.Y - 150f < area.End.Y - 4f;
                    switch (_airStrikeStage) {
                        case 0:
                            intent.Held |= GameplayButtons.Jump;
                            if (player.Velocity.Y >= 0f) _airStrikeStage = !reaches && player.RemainingJumps > 0 ? 1 : 3;
                            break;
                        case 1:
                            _airStrikeStage = 2; // one frame released, so the next press is an edge
                            break;
                        case 2:
                            intent.Held |= GameplayButtons.Jump;
                            if (player.Velocity.Y >= 0f) _airStrikeStage = 3;
                            break;
                    }
                    if (reaches && player.Velocity.Y > -330f) {
                        intent.MoveY = -1f;
                        if (Frame % 3 == 0) intent.Tap |= GameplayButtons.BasicAttack;
                    }
                    intent.MoveX = Mathf.Abs(centerX - feet.X) > 12f ? Mathf.Sign(centerX - feet.X) * 0.5f : 0f;
                }
                return;
            }

            if (grounded) {
                // Standing swing: 5..55 px ahead, feet-54 .. feet-10.
                bool band = area.Position.Y < feet.Y - 10f && area.End.Y > feet.Y - 54f;
                bool reach = overlapsX || (gapLeft >= -4f && gapLeft <= 44f) || (gapRight >= -4f && gapRight <= 44f);
                if (band && reach) {
                    FaceX(player, centerX, ref intent);
                    if (Frame % 10 == 0) intent.Tap |= GameplayButtons.BasicAttack;
                    return;
                }
                // Up-attack box: +-75 px, feet-150 .. feet.
                bool upX = area.Position.X < feet.X + 66f && area.End.X > feet.X - 66f;
                if (upX && area.End.Y > feet.Y - 144f && area.Position.Y < feet.Y - 6f) {
                    intent.MoveY = -1f;
                    if (Frame % 12 == 0) intent.Tap |= GameplayButtons.BasicAttack;
                    return;
                }
                if (upX && area.End.Y > feet.Y - JumpStrikeReach && area.End.Y <= feet.Y - 144f
                    && Mathf.Abs(feet.X - Mathf.Clamp(feet.X, area.Position.X + 8f, area.End.X - 8f)) < 30f) {
                    if (Frame - _airStrikeFrame > 70) {
                        _airStrikeFrame = Frame;
                        _airStrikeDown = false;
                        _airStrikeStage = 0;
                        _airStrikeArea = area;
                        intent.Held |= GameplayButtons.Jump;
                    }
                    return;
                }
                // Down-air box: +-62 px, feet .. feet+125.
                bool downX = area.Position.X < feet.X + 50f && area.End.X > feet.X - 50f;
                if (downX && area.Position.Y >= feet.Y - 8f && area.Position.Y < feet.Y + 110f) {
                    if (Frame - _airStrikeFrame > 40) {
                        _airStrikeFrame = Frame;
                        _airStrikeDown = true;
                        _airStrikeSwung = false;
                        _airStrikeArea = area;
                        intent.Held |= GameplayButtons.Jump;
                    }
                    return;
                }
            }

            // Walk to a stand point.
            (Vector2 point, float maxBelow) = StrikePoint(player, area);
            GoRaw(player, point, ref intent, 8f, maxBelow);
        }

        private Rect2 _airStrikeArea;
        private bool _airStrikeDown;
        private int _airStrikeStage;

        /// <summary>How far above the feet a jumping up-attack reaches (double-jump rise + the 150 px box).</summary>
        private float JumpStrikeReach => World.Nav.Hero.SingleRise(1f) * (World.Nav.Hero.MaxJumps >= 2 ? 1.9f : 0.95f) + 140f;
        private bool _airStrikeSwung;
        private Rect2 _strikePointArea;
        private (Vector2, float) _strikePoint;
        private int _strikePointFrame = -1000;

        /// <summary>
        /// The nearest reachable place to hit <paramref name="area"/> from:
        /// beside it at its base, under it (up-attack reach), or on a surface
        /// above it (down-air). Cached for a second and a half.
        /// </summary>
        private (Vector2 Point, float MaxBelow) StrikePoint(PlayerController player, Rect2 area) {
            if (_strikePointArea == area && Frame - _strikePointFrame < 90) return _strikePoint;
            Vector2 feet = player.GlobalPosition;
            var candidates = new List<(Vector2 Point, float MaxBelow)> {
                (new Vector2(area.Position.X - 20f, area.End.Y - 2f), 46f),
                (new Vector2(area.End.X + 20f, area.End.Y - 2f), 46f),
                (new Vector2(area.GetCenter().X, area.End.Y + 2f), JumpStrikeReach - 10f),
                (new Vector2(Mathf.Clamp(feet.X, area.Position.X - 36f, area.End.X + 36f), area.Position.Y - 110f), 110f),
                (new Vector2(area.Position.X - 36f, area.Position.Y - 110f), 110f),
                (new Vector2(area.End.X + 36f, area.Position.Y - 110f), 110f)
            };
            candidates.Sort((a, b) => feet.DistanceTo(a.Point).CompareTo(feet.DistanceTo(b.Point)));
            (Vector2, float) chosen = candidates[0];
            foreach ((Vector2 point, float maxBelow) in candidates) {
                if (Reachable(player, point, maxBelow)) {
                    chosen = (point, maxBelow);
                    break;
                }
            }
            _strikePointArea = area;
            _strikePoint = chosen;
            _strikePointFrame = Frame;
            return chosen;
        }

        // =================================================================
        // Route hints and travel
        // =================================================================

        private bool WorkRouteHints(PlayerController player, ref BotIntent intent) {
            if (_hints == null) return false;
            PlaytestRouteHints.Hint hint = _hints.Active(World, player);
            if (hint == null) return false;
            CurrentAction = "hint:" + hint.Name;
            if (hint.Script != null) return hint.Script(World, player, this, ref intent);
            if (hint.Interact != null) {
                Node2D node = hint.Interact(World);
                if (node != null) return GoInteract(player, node, ref intent, CurrentAction, 24f);
            }
            if (Go(player, hint.Goal, ref intent, hint.Radius, hint.MaxBelow)) return true;
            hint.Arrived(World, player, Frame);
            if (hint.HoldFrames > 0) {
                intent.MoveX = 0f;
                return true;
            }
            return false;
        }

        private void Travel(PlayerController player, ref BotIntent intent) {
            CurrentAction = "travel";
            Vector2 goal = TravelGoal(player);
            _travelDir = Mathf.Sign(goal.X - player.GlobalPosition.X) == 0 ? _travelDir : Mathf.Sign(goal.X - player.GlobalPosition.X);
            if (!Go(player, goal, ref intent, 30f, 300f)) {
                // At the travel goal (or the closest reachable point to it) with
                // nothing to do: wait for the boss / anchor, hopping now and then.
                CurrentAction = Agent.GoalReachable ? "wait" : "stuck";
                if (!Agent.GoalReachable && Frame % 90 == 0) intent.Tap |= GameplayButtons.Interact;
                // Nothing left to try from here: give every shelved task another chance.
                if (!Agent.GoalReachable && Frame - _lastUnblockFrame > 240) {
                    _lastUnblockFrame = Frame;
                    _taskBlockedUntil.Clear();
                    _reachCache.Clear();
                }
            }
        }

        private Vector2 TravelGoal(PlayerController player) {
            BossEncounterController encounter = null;
            foreach (BossEncounterController e in World.BossEncounters) {
                if (GodotObject.IsInstanceValid(e) && !e.IsDefeated) { encounter = e; break; }
            }
            if (encounter != null) {
                // The boss itself, not the encounter node (which can sit at the
                // far end of the arena, out of engage range of a boss that holds
                // its ground).
                Node2D boss = null;
                float best = float.MaxValue;
                foreach (Node2D foe in World.Foes()) {
                    if (!PlaytestWorld.IsBossLike(foe) || PlaytestWorld.HealthOf(foe) <= 0) continue;
                    float d = foe.GlobalPosition.DistanceTo(encounter.GlobalPosition);
                    if (d < best && d < 3000f) {
                        best = d;
                        boss = foe;
                    }
                }
                return boss?.GlobalPosition ?? encounter.GlobalPosition;
            }
            Rect2 bounds = World.Level?.LevelBounds ?? new Rect2(0, 0, 12000, 1200);
            return new Vector2(bounds.End.X - 200f, player.GlobalPosition.Y);
        }

        /// <summary>Navigate toward a point; a Break step attacks the obstacle. Returns false on arrival.</summary>
        private bool Go(PlayerController player, Vector2 goal, ref BotIntent intent, float arriveRadius, float maxBelow) {
            bool moving = Agent.Drive(player, goal, ref intent, arriveRadius, maxBelow);
            if (Agent.BreakTarget is DamageableEnvironmentObject obstacle && GodotObject.IsInstanceValid(obstacle)) {
                Rect2? area = PlaytestWorld.ShapeRect(obstacle);
                if (area.HasValue) StrikeRect(player, area.Value, ref intent, "break_path");
                return true;
            }
            if (moving && player.IsOnFloor() && Mathf.Abs(intent.MoveX) > 0.1f) _travelDir = Mathf.Sign(intent.MoveX);
            return moving;
        }

        /// <summary>
        /// Navigation for positioning inside a strike (no recursion into
        /// <see cref="StrikeRect"/>): a Break step on the way just swings at
        /// whatever is directly ahead.
        /// </summary>
        private bool GoRaw(PlayerController player, Vector2 goal, ref BotIntent intent, float arriveRadius, float maxBelow) {
            bool moving = Agent.Drive(player, goal, ref intent, arriveRadius, maxBelow);
            if (Agent.BreakTarget is DamageableEnvironmentObject obstacle && GodotObject.IsInstanceValid(obstacle)) {
                FaceX(player, obstacle.GlobalPosition.X, ref intent);
                if (Frame % 10 == 0) intent.Tap |= GameplayButtons.BasicAttack;
                return true;
            }
            return moving;
        }
    }

    public static class PlaytestBotFactory {
        public static PlaytestBot Create(string name, PlaytestWorld world, CampaignLevel level, string enemyID = "chrono_slasher") => name switch {
            "bypass" => new StoryBot(world, FightStyle.Bypass, level),
            "spam" => new StoryBot(world, FightStyle.Spam, level),
            "basics" => new StoryBot(world, FightStyle.Basics, level),
            "play" => new StoryBot(world, FightStyle.Play, level),
            "feel" => new FeelProbeBot(world, enemyID),
            _ => throw new ArgumentException($"Unknown playtest bot '{name}' (bypass | spam | basics | play | feel).")
        };
    }
}
