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
        /// <summary>
        /// Fights only what the run requires (since the 2026-10-12 encounter locks):
        /// the members of the lock it stands in, bosses, and a blocker in an
        /// escape. Never defends. A lock-regression probe: it must still clear
        /// every lock to finish.
        /// </summary>
        Bypass,
        /// <summary>Mashes the 3-hit string, then Special 1 and Special 2 as soon as they are ready. Never defends.</summary>
        Spam,
        /// <summary>Basic string only; blocks enemy swings.</summary>
        Basics,
        /// <summary>A competent player: string + specials when ready + Ultimate at full meter, blocks swings, avoids projectiles.</summary>
        Play
    }

    /// <summary>
    /// How the Spam and Play bots press their Specials (<c>--special-press</c>;
    /// spec C §1.4(f), package C3; diagnostics only).
    /// </summary>
    public enum SpecialPressPolicy {
        /// <summary>
        /// The default: Specials only in the Specials phase after the string, and
        /// only while the hero's own Special gate reads open — so lockout refusals
        /// are about 0 by construction.
        /// </summary>
        Gated,
        /// <summary>
        /// Also, once per swing, a Special on hit-confirm timing: on the first
        /// sample after the swing has landed and the impact freeze is over, the
        /// first ready Special is tapped instead of that frame's basic attack,
        /// without reading the lockout or the gate. Before group A's D1 these
        /// presses are refused by the lockout; after it they are accepted.
        /// </summary>
        Confirm
    }

    /// <summary>
    /// The Story campaign bot. Every frame it picks one job, in priority order:
    /// seal an armed anchor; get out of a hot hazard; outrun an escape front;
    /// fight (its <see cref="FightStyle"/>); hunt the members of the encounter
    /// lock it is engaged in that it cannot target from here; work the nearest
    /// reachable level task (strike a checkpoint the locks do not hold, rescue,
    /// press the expected glyph, turn a coil, weigh a plate, break a puzzle
    /// breakable); otherwise travel toward the boss arena — walking into the
    /// next Armed lock on the way, and holding inside an engaged one rather
    /// than pushing at its closed barrier. Movement always goes through the
    /// <see cref="NavAgent"/>.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, user request 3,
    /// plan §5 GU-BOT). The woken members of the lock the hero is engaged in get
    /// boss-like targeting: no engage-radius cap, no line-of-sight filter and no
    /// give-up (an unprogressing member is only deprioritized). Dormant members
    /// refuse every hit (the lock ward), so they are never targets. A checkpoint
    /// an uncleared lock holds is never a task. A lock that makes no progress
    /// for <see cref="LockStallFrames"/> while no member is reachable, or for
    /// twice that otherwise, is reported through <see cref="StuckLockID"/> and a
    /// nav note, and the runner ends the run <c>stuck_lock:&lt;id&gt;</c>.</para>
    ///
    /// <para><b>Getting to them</b> (FIX-BOT, 2026-10-13). A lock member is walked
    /// in on only when nothing solid stands between (a breakable, a prop, cover,
    /// a step stops the walk-in; the agent's Break, jump and hop edges take over),
    /// a member on a reachable ledge is fought up there rather than from below,
    /// and the puzzles an engaged lock needs are worked: its gear trains are
    /// planned and pressed, and its plates count as reachable through any stand
    /// point. Outside locks the old approach stands unchanged.</para>
    /// </summary>
    public sealed class StoryBot : PlaytestBot, PlaytestRouteHints.IHintDriver {
        int PlaytestRouteHints.IHintDriver.Frame => Frame;

        bool PlaytestRouteHints.IHintDriver.GoTo(PlayerController player, Vector2 goal, float arriveRadius, float maxBelow, ref BotIntent intent) =>
            Go(player, goal, ref intent, arriveRadius, maxBelow);

        private readonly FightStyle _style;
        private readonly PlaytestRouteHints _hints;
        private readonly SpecialPressPolicy _policy;

        public StoryBot(PlaytestWorld world, FightStyle style, CampaignLevel level,
            SpecialPressPolicy policy = SpecialPressPolicy.Gated) : base(world) {
            _style = style;
            _hints = PlaytestRouteHints.For(level);
            _policy = policy;
        }

        public FightStyle Style => _style;
        public SpecialPressPolicy Policy => _policy;
        public override string Name => _style switch {
            FightStyle.Bypass => "bypass",
            FightStyle.Spam => "spam",
            FightStyle.Basics => "basics",
            _ => "play"
        };

        // =================================================================
        // Defy (meter and Defy update 2026-10-31, O24, P26; PROVISIONAL-BOT-DEFY-POLICY)
        // =================================================================

        private readonly PlaytestDefyPolicy _defy = new();
        private ulong _defyHeroId;
        private System.Action _defyUnsubscribe;

        /// <summary>
        /// The <c>play</c> bot's Defy: pressed once per qualifying moment (see
        /// <see cref="PlaytestDefyPolicy"/>). Every other style does nothing here, and the press goes
        /// through the same <see cref="GameplayButtons.Defy"/> bit a player's button does.
        /// </summary>
        private void PressDefyIfNeeded(PlayerController player, ref BotIntent intent) {
            if (!PlaytestDefyPolicy.StyleDefies(_style)) return;
            ulong id = player.GetInstanceId();
            if (id != _defyHeroId) {
                _defyUnsubscribe?.Invoke();
                _defyHeroId = id;
                _defyUnsubscribe = PlaytestWorld.SubscribeDamageTaken(player, o => {
                    if (o.Delivery != HitDelivery.Tick) _defy.NoteHit(Frame);
                });
            }
            bool canDefy = PlaytestWorld.CanDefy(player);
            if (!canDefy) return;
            bool hitstun = player.CurrentState == CharacterState.Stunned;
            int near = hitstun ? World.FoesWithin(player.GlobalPosition, PlaytestDefyPolicy.EnemyRadiusPixels) : 0;
            if (!_defy.ShouldDefy(_style, Frame, canDefy, hitstun, near)) return;
            _defy.NoteDefyPressed(Frame);
            intent.Tap |= GameplayButtons.Defy;
            CurrentAction = "defy";
        }

        private bool Fights => _style != FightStyle.Bypass;
        private bool Defends => _style is FightStyle.Basics or FightStyle.Play;

        protected override void Decide(PlayerController player, ref BotIntent intent) {
            // The target the swings now in flight were thrown at (the confirm
            // policy's HP fallback reads it before this frame re-chooses).
            Node2D swingTarget = CurrentTarget;
            CurrentTarget = null;
            UpdateSwingCounter(player, swingTarget);
            PressDefyIfNeeded(player, ref intent);

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

            WatchEngagedLock(player);
            bool done = WorkSealAnchor(player, ref intent)
                || EvadeHazards(player, ref intent)
                || OutrunEscape(player, ref intent)
                || CollectHealing(player, ref intent)
                || FightNearby(player, ref intent)
                || HuntLockMembers(player, ref intent)
                || WorkTasks(player, ref intent)
                || WorkRouteHints(player, ref intent);
            if (!done) Travel(player, ref intent);
            FilterHazards(player, ref intent);
        }

        // =================================================================
        // Encounter locks (gameplay update 2026-10-12, plan §5 GU-BOT)
        // =================================================================

        /// <summary>
        /// <i>Diagnostics bound.</i> An engaged lock with no progress (no member HP
        /// lost, no member or wave count change, no required breakable hit, no
        /// step on the bot's current task) for this many of the bot's live frames
        /// (60 s) while none of its living members is reachable ends the run
        /// <c>stuck_lock:&lt;id&gt;</c>; with a member reachable, or with no living
        /// member left (waiting on a wave, a breakable or a puzzle), twice this.
        /// </summary>
        public const int LockStallFrames = 60 * 60;

        /// <summary>A lock member the bot has not hurt for this long is fought last, never given up on.</summary>
        private const int LockTargetDeprioritizeFrames = TargetGiveUpFrames;
        private const float LockTargetDeprioritizedPenalty = 2000f;

        /// <summary>The lock this run is stuck in (set once; the runner ends the run on it), or null.</summary>
        public string StuckLockID { get; private set; }

        private ulong _watchedLockId;
        private int _lockProgressFrame;
        private int _lockWatchFrame = -1;
        private int _lockLastHp = -1;
        private int _lockLastLiving = -1;
        private int _lockLastRemaining = -1;
        private int _lockLastBreakables = -1;
        private ulong _lockLastTaskId;
        private int _lockLastTaskProgress;
        private bool _lockStallNoted;
        private readonly Dictionary<ulong, int> _deprioritizedUntil = new();

        /// <summary>True for a woken member of the lock the hero is engaged in: a target the run requires.</summary>
        private bool IsLockTarget(Node2D foe, EncounterLock engaged) => PlaytestWorld.IsActiveMemberOf(foe, engaged);

        /// <summary>
        /// The stuck-lock watchdog. Frames the bot did not decide (the hero dead,
        /// the tree paused) do not count toward a stall.
        /// </summary>
        private void WatchEngagedLock(PlayerController player) {
            EncounterLock engaged = World.EngagedLock();
            if (engaged == null) {
                _watchedLockId = 0;
                _lockWatchFrame = Frame;
                return;
            }
            if (_lockWatchFrame >= 0 && Frame - _lockWatchFrame > 1) _lockProgressFrame += Frame - _lockWatchFrame - 1;
            _lockWatchFrame = Frame;
            IReadOnlyList<EnemyController> living = engaged.LivingMembers;
            int hp = 0;
            foreach (EnemyController member in living) hp += Math.Max(0, member.CurrentHP);
            int remaining = engaged.RemainingCount;
            // A lock that waits on a breakable or a puzzle progresses through it:
            // the required breakables' HP, and the bot's own task progress
            // (a coil turned, a glyph lit, a breakable hit).
            int breakables = 0;
            if (engaged.Spec?.RequiredBreakables != null) {
                foreach (DamageableEnvironmentObject breakable in engaged.Spec.RequiredBreakables) {
                    if (breakable != null && GodotObject.IsInstanceValid(breakable) && !breakable.IsDestroyed) breakables += Math.Max(0, breakable.CurrentHP);
                }
            }
            ulong taskId = _task != null && GodotObject.IsInstanceValid(_task) ? _task.GetInstanceId() : 0UL;
            int taskProgress = taskId != 0UL ? TaskProgress(_task) : 0;
            bool taskAdvanced = taskId != 0UL && taskId == _lockLastTaskId && taskProgress != _lockLastTaskProgress;
            if (engaged.GetInstanceId() != _watchedLockId) {
                _watchedLockId = engaged.GetInstanceId();
                _lockProgressFrame = Frame;
                _lockStallNoted = false;
            } else if (hp < _lockLastHp || living.Count != _lockLastLiving || remaining != _lockLastRemaining
                       || breakables < _lockLastBreakables || taskAdvanced) {
                _lockProgressFrame = Frame;
                _lockStallNoted = false;
            }
            _lockLastHp = hp;
            _lockLastLiving = living.Count;
            _lockLastRemaining = remaining;
            _lockLastBreakables = breakables;
            _lockLastTaskId = taskId;
            _lockLastTaskProgress = taskProgress;
            if (StuckLockID != null) return;
            int stalled = Frame - _lockProgressFrame;
            if (stalled < LockStallFrames) return;
            if (living.Count == 0) {
                // Nothing left to fight (a wave still to come, a breakable or a
                // puzzle the lock needs): only the longer bound applies.
                if (stalled < LockStallFrames * 2) return;
                StuckLockID = engaged.LockID;
                Agent.Notes.Add($"lock {engaged.LockID}: no progress for {stalled / 60} s with no living member,"
                    + $" {remaining} remaining (waves, a required breakable or puzzle)");
                return;
            }
            string unreachable = "";
            bool anyReachable = false;
            foreach (EnemyController member in living) {
                if (MemberReachable(player, member)) {
                    anyReachable = true;
                    break;
                }
                if (unreachable.Length < 200) {
                    unreachable += $" {member.Data?.EnemyID ?? member.Name}@{Mathf.RoundToInt(member.GlobalPosition.X)},{Mathf.RoundToInt(member.GlobalPosition.Y)}";
                }
            }
            if (!anyReachable || stalled >= LockStallFrames * 2) {
                StuckLockID = engaged.LockID;
                Agent.Notes.Add($"lock {engaged.LockID}: no progress for {stalled / 60} s, {living.Count} living,"
                    + $" {remaining} remaining, {(anyReachable ? "a member is reachable" : "no member reachable:" + unreachable)}");
            } else if (!_lockStallNoted) {
                _lockStallNoted = true;
                Agent.Notes.Add($"lock {engaged.LockID}: no progress for {stalled / 60} s though a member is reachable ({living.Count} living)");
            }
        }

        /// <summary>A member the hero can get to: its feet, or the floor under it within a jumping strike's reach.</summary>
        private bool MemberReachable(PlayerController player, EnemyController member) {
            Vector2 at = member.GlobalPosition;
            if (Reachable(player, at, 120f)) return true;
            NavSegment under = World.Nav.SegmentUnder(at, JumpStrikeReach);
            return under != null && Reachable(player, new Vector2(under.Clamp(at.X, 14f), under.TopAt(at.X)), 40f);
        }

        /// <summary>
        /// Engaged in a lock whose living members none of the fight rules picked
        /// (all far overhead, say): go to the nearest one, under it if it flies.
        /// </summary>
        private bool HuntLockMembers(PlayerController player, ref BotIntent intent) {
            EncounterLock engaged = World.EngagedLock();
            if (engaged == null) return false;
            EnemyController nearest = null;
            float best = float.MaxValue;
            foreach (EnemyController member in engaged.LivingMembers) {
                if (member.IsEncounterDormant) continue;
                float distance = member.GlobalPosition.DistanceTo(player.GlobalPosition);
                if (distance < best) {
                    best = distance;
                    nearest = member;
                }
            }
            if (nearest == null) return false;
            CurrentAction = "lock_hunt";
            CurrentTarget = nearest;
            Vector2 at = nearest.GlobalPosition;
            NavSegment under = World.Nav.SegmentUnder(at, 2000f);
            Vector2 goal = under != null ? new Vector2(under.Clamp(at.X, 14f), under.TopAt(at.X)) : at;
            if (!Go(player, goal, ref intent, 30f, 400f)) FaceTarget(player, nearest, ref intent);
            return true;
        }

        // --- Reaching a lock member (FIX-BOT, 2026-10-13) ---------------------
        // The lock member is never given up on, so the approach has to get there:
        // a straight walk-in pinned the bot against Level 4's cell lock B, Level 6's
        // basalt boulder, Level 11's cover and Level 5's stair steps for the rest
        // of the run, and a member posted on a ledge was jumped at from below.

        /// <summary>A plan to a lock member's surface may end on that surface only (the stand point is on its top).</summary>
        private const float PerchMaxBelow = 40f;
        /// <summary>A walk-in that has not moved the hero for this many frames is blocked by something the ray missed (a step, a low prop).</summary>
        private const int LockWalkInStallFrames = 30;
        /// <summary>How long a blocked walk-in hands the approach to the agent before walking in is tried again.</summary>
        private const int LockWalkInDetourFrames = 180;

        private int _walkInStalled;
        private int _walkInLastFrame = -1000;
        private float _walkInLastX;
        private int _walkInBlockedUntil = -1;

        /// <summary>The nav surface a body stands on (its feet within 16 px of the top), or null in the air.</summary>
        private NavSegment StandingSurface(Node2D body) =>
            World.Nav.SegmentUnder(body.GlobalPosition, maxBelow: 16f, minAbove: -16f, xMargin: 24f);

        /// <summary>
        /// Where to stand to fight a lock member that is not on the hero's level:
        /// on its own surface, on the hero's side of it, when the hero can get
        /// there; null when it stands on nothing (a flyer, a body in the air), on
        /// the hero's own surface, or somewhere unreachable (the old jumping
        /// up-attack from below is then all that is left).
        /// </summary>
        private Vector2? LockTargetPerch(PlayerController player, Node2D target) {
            Vector2 feet = player.GlobalPosition;
            Vector2 at = target.GlobalPosition;
            if (Mathf.Abs(at.Y - feet.Y) < 60f) return null;
            NavSegment surface = StandingSurface(target);
            if (surface == null) return null;
            NavSegment here = World.Nav.Locate(player);
            if (here != null && here == surface) return null;
            float side = feet.X <= at.X ? -1f : 1f;
            float x = surface.Clamp(at.X + side * 60f, 14f);
            var point = new Vector2(x, surface.TopAt(x));
            return Reachable(player, point, PerchMaxBelow) ? point : null;
        }

        /// <summary>
        /// The point on a lock member's own surface under it, a plan to which may
        /// end on that surface only; for a member on no surface (a flyer, a body
        /// in the air), its feet, with the old 300 px of floor under them.
        /// </summary>
        private Vector2 LockTargetGoal(Node2D target, out float maxBelow) {
            NavSegment surface = StandingSurface(target);
            Vector2 at = target.GlobalPosition;
            if (surface == null) {
                maxBelow = 300f;
                return at;
            }
            maxBelow = PerchMaxBelow;
            float x = surface.Clamp(at.X, 14f);
            return new Vector2(x, surface.TopAt(x));
        }

        /// <summary>
        /// A straight walk-in at a lock member is clear: nothing solid on the line
        /// from the hero's chest to the member's (<see cref="PlaytestWorld.ClearWalk"/>,
        /// which, unlike <see cref="PlaytestWorld.LineOfSight"/>, stops at breakables
        /// and props), both stand on the one surface, and the last walk-in did not
        /// stall against something the ray missed.
        /// </summary>
        private bool LockWalkInClear(PlayerController player, Node2D target) {
            if (Frame < _walkInBlockedUntil) return false;
            Vector2 chest = player.GlobalPosition + new Vector2(0f, -36f);
            Vector2 center = target.GlobalPosition + new Vector2(0f, -30f);
            if (!World.ClearWalk(chest, center)) return false;
            // Two static surfaces are two because an obstacle or a gap splits them;
            // a lift or prop under the hero (Level 12's spire lift, flush with the
            // floor at its stop) says nothing about what lies between.
            NavSegment here = World.Nav.Locate(player);
            NavSegment there = StandingSurface(target);
            return here == null || there == null || here.Temporary || here.Platform != null || here == there;
        }

        /// <summary>A walk-in that leaves the hero standing still hands the approach to the agent for a while.</summary>
        private void WatchLockWalkIn(PlayerController player) {
            float x = player.GlobalPosition.X;
            bool still = Mathf.Abs(x - _walkInLastX) < 2f && Mathf.Abs(player.Velocity.X) < 20f
                && player.CurrentState is CharacterState.Idle or CharacterState.Running;
            if (Frame - _walkInLastFrame > 1 || !still) {
                _walkInStalled = 0;
            } else if (++_walkInStalled > LockWalkInStallFrames) {
                _walkInStalled = 0;
                _walkInBlockedUntil = Frame + LockWalkInDetourFrames;
            }
            _walkInLastFrame = Frame;
            _walkInLastX = x;
        }

        /// <summary>True while the lock the hero is engaged in needs <paramref name="puzzleID"/> solved to clear.</summary>
        private bool EngagedLockRequires(string puzzleID) {
            string[] required = World.EngagedLock()?.Spec?.RequiredPuzzleIDs;
            return !string.IsNullOrEmpty(puzzleID) && required != null && Array.IndexOf(required, puzzleID) >= 0;
        }

        /// <summary>
        /// Inside an engaged lock with nothing to fight or work (a wave still to
        /// come, a breakable the tasks cannot reach): hold near the area's middle
        /// instead of pushing at the closed exit.
        /// </summary>
        private bool HoldInEngagedLock(PlayerController player, ref BotIntent intent) {
            EncounterLock engaged = World.EngagedLock();
            if (engaged == null) return false;
            CurrentAction = "lock_wait";
            Rect2 area = engaged.Area;
            float x = Mathf.Clamp(player.GlobalPosition.X, area.Position.X + area.Size.X * 0.3f, area.End.X - area.Size.X * 0.3f);
            if (!Go(player, new Vector2(x, player.GlobalPosition.Y), ref intent, 60f, 300f)) intent.MoveX = 0f;
            return true;
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
                CurrentTarget = extractor;
                float away = feet.X >= extractor.GlobalPosition.X ? 1f : -1f;
                // FIX-BOT (2026-10-13): never run off a ledge to dodge a discharge.
                // On Level 12's mast head the dodge ran the hero off the highline
                // launch into the dead end under the curtain, for good; the hit
                // costs far less. Brace (Block) instead.
                if (RunsOffLedge(player, away)) {
                    CurrentAction = "brace_discharge";
                    intent.Held |= GameplayButtons.Block;
                    return true;
                }
                CurrentAction = "evade_discharge";
                intent.MoveX = away;
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
                // FIX-BOT: not off a ledge (Level 12's mast-head vent ran the hero
                // off the highline launch): the other way, or brace where it stands.
                if (RunsOffLedge(player, dir)) {
                    if (RunsOffLedge(player, -dir)) {
                        CurrentAction = "brace_hazard";
                        intent.Held |= GameplayButtons.Block;
                        return true;
                    }
                    dir = -dir;
                }
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

        /// <summary>A dodge with less than this much surface left before a drop would run off it.</summary>
        private const float DodgeLedgeRoomPixels = 64f;

        /// <summary>
        /// True when walking <paramref name="dir"/> would take the hero off its
        /// surface's end within <see cref="DodgeLedgeRoomPixels"/> onto nothing
        /// standable at about the same height (a ledge, not a seam).
        /// </summary>
        internal bool RunsOffLedge(PlayerController player, float dir) {
            NavSegment here = World.Nav.Locate(player);
            if (here == null || here.Temporary || here.Platform != null) return false;
            float x = player.GlobalPosition.X;
            float room = dir > 0f ? here.Right - x : x - here.Left;
            if (room > DodgeLedgeRoomPixels) return false;
            float endX = dir > 0f ? here.Right : here.Left;
            var past = new Vector2(endX + dir * (HeroPhysics.BodyHalfWidth + 16f), here.TopAt(endX));
            return World.Nav.SegmentUnder(past, maxBelow: 40f, minAbove: -24f, xMargin: 4f) == null;
        }

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

        private ulong _escapePastFinishNoted;

        private bool OutrunEscape(PlayerController player, ref BotIntent intent) {
            EscapeSequenceController escape = World.RunningEscape();
            if (escape == null) return false;
            // An engaged encounter lock cannot be outrun (its barriers are shut):
            // fight it first, then run (plan §5: escapes start on a lock's clear;
            // this keeps the bot from pushing at a barrier if one does not).
            if (World.EngagedLock() != null) return false;
            // FIX-BOT (2026-10-13): past the finish line there is nothing left to
            // outrun, so go on as a player would. Level 6's run never completes for
            // a hero who crosses its finish trigger in one go (the trigger refuses
            // a body that enters short of FinishX and never fires again: the game
            // bug GU-L12 fixed on Level 15), and the bot used to idle past the line
            // for the rest of the run. The note keeps that bug visible.
            if (player.GlobalPosition.X >= escape.FinishX + HeroPhysics.BodyHalfWidth) {
                if (_escapePastFinishNoted != escape.GetInstanceId()) {
                    _escapePastFinishNoted = escape.GetInstanceId();
                    Agent.Notes.Add($"escape {escape.SequenceID}: still running with the hero past its finish "
                        + $"(x {Mathf.RoundToInt(player.GlobalPosition.X)} >= {Mathf.RoundToInt(escape.FinishX)}); going on");
                }
                return false;
            }
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
        /// <summary>Extra gap past the strike range before a mid-string target counts as slid away.</summary>
        private const float StringChaseSlack = 70f;

        /// <summary>
        /// The feet-to-feet gap from which this hero's string opener reaches
        /// this target (2026-10-11; it was a fixed 92 px for every hero and
        /// foe): hit 1's grounded box far edge, from the one cross-mode table
        /// through <see cref="PlayerController.ResolveGroundStringBoxPixels"/>
        /// with the hero's reach profile and AttackRange minor, plus the
        /// target's hurtbox half-width, less the CPU's melee-band margin in
        /// pixels (<see cref="FTT.FighterSim.FighterCpuController.MeleeBandMarginUnits"/>).
        /// </summary>
        internal static float StrikeRangeFor(PlayerController player, Node2D target) {
            PlayerController.ResolveGroundStringBoxPixels(
                0,
                BasicComboRules.StringProfileFor(player.Data?.CharacterID),
                player.StoryAttackRangeMultiplier,
                out Vector2 size,
                out Vector2 offset);
            float farEdge = offset.X + size.X * 0.5f;
            float targetHalfWidth = PlayerController.HurtboxWidthPixels(target) * 0.5f;
            float margin = (float)(FTT.FighterSim.FighterCpuController.MeleeBandMarginUnits * KitMotionRules.StoryPixelsPerUnit);
            return farEdge + targetHalfWidth - margin;
        }

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
            player.GetNodeOrNull<UltimateMeter>("UltimateMeter")?.IsFull
                ?? player.CurrentUltimateMeter >= InfluenceMeterRules.MaxValue;

        private int _blockFrames;
        private int _ultimateTapFrames;
        private int _ultimateRefusedUntil;
        private int _airStrikeFrame = -1000;

        // C3 (--special-press=confirm): one hit-confirm Special per swing, and the
        // pre-group-A "landed" fallback (the swing's target lost HP since its box
        // went active).
        private bool _confirmPressedThisSwing;
        private int _swingTargetHp = -1;
        private ulong _swingTargetId;
        private int _preSwingTargetHp = -1;
        private ulong _preSwingTargetId;

        private void UpdateSwingCounter(PlayerController player, Node2D target) {
            bool hitboxActive = player.GetNodeOrNull<FTT.Combat.Hitbox>("MeleeHitbox")?.IsActive ?? false;
            bool hasTarget = target != null && GodotObject.IsInstanceValid(target);
            ulong targetId = hasTarget ? target.GetInstanceId() : 0UL;
            int targetHp = hasTarget ? PlaytestWorld.HealthOf(target) : -1;
            if (hitboxActive && !_lastHitboxActive) {
                _swingsSeen++;
                _confirmPressedThisSwing = false;
                // The box went active in the hero's update after the previous
                // sample, and a contact on its first active frame was delivered
                // in this frame's physics flush, before this sample: the HP
                // baseline is the previous sample's reading, not this one.
                _swingTargetId = targetId;
                _swingTargetHp = hasTarget && targetId == _preSwingTargetId ? _preSwingTargetHp : targetHp;
            }
            if (!hitboxActive) {
                _preSwingTargetId = targetId;
                _preSwingTargetHp = targetHp;
            }
            _lastHitboxActive = hitboxActive;
        }

        /// <summary>
        /// Whether the swing now in flight (or just ended) has landed: the hero's
        /// own record once group A is in (<see cref="PlaytestWorld.SwingLanded"/>),
        /// else whether the bot's target lost HP since the swing's box went active.
        /// </summary>
        private bool CurrentSwingLanded(PlayerController player, Node2D target) {
            bool? landed = PlaytestWorld.SwingLanded(player);
            if (landed.HasValue) return landed.Value;
            return _swingTargetHp > 0 && target != null && GodotObject.IsInstanceValid(target)
                && target.GetInstanceId() == _swingTargetId
                && PlaytestWorld.HealthOf(target) < _swingTargetHp;
        }

        /// <summary>
        /// The <see cref="SpecialPressPolicy.Confirm"/> press (Spam and Play):
        /// once per swing, on the first sample after it has landed and the hero's
        /// impact freeze is over (a press during the freeze is dropped), tap the
        /// first unlocked Special whose cooldown is ready instead of this frame's
        /// basic attack. It deliberately reads neither the lockout nor the Special
        /// gate: it presses on hit-confirm timing, as a person would.
        /// </summary>
        private bool TryConfirmSpecial(PlayerController player, Node2D target, ref BotIntent intent) {
            if (_policy != SpecialPressPolicy.Confirm || _style is not (FightStyle.Spam or FightStyle.Play)) return false;
            if (_confirmPressedThisSwing || player.HitstopFramesRemaining != 0
                || player.CurrentState == CharacterState.UsingSpecial || !CurrentSwingLanded(player, target)) return false;
            if (player.IsAbilityUnlocked(AbilitySlot.Special1) && PlaytestWorld.SpecialReady(player, AbilitySlot.Special1)) {
                intent.Tap |= GameplayButtons.Special1;
                CurrentAction = "confirm_special1";
            } else if (player.IsAbilityUnlocked(AbilitySlot.Special2) && PlaytestWorld.SpecialReady(player, AbilitySlot.Special2)) {
                intent.Tap |= GameplayButtons.Special2;
                CurrentAction = "confirm_special2";
            } else {
                return false;
            }
            _confirmPressedThisSwing = true;
            return true;
        }

        private bool Ignored(Node2D body) =>
            _ignoredUntil.TryGetValue(body.GetInstanceId(), out int until) && until > Frame;

        private Node2D NearestFoe(PlayerController player, float radius, bool ahead = false) {
            Node2D best = null;
            float bestDistance = radius;
            Vector2 at = player.GlobalPosition + new Vector2(0f, -32f);
            foreach (Node2D foe in World.Foes()) {
                // A dormant lock member refuses every hit: never a blocker to strike.
                if (PlaytestWorld.IsDormantMember(foe)) continue;
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

        /// <summary>
        /// The foe to fight now, or null. Bosses are never given up on, and since
        /// the 2026-10-12 encounter locks neither are the woken members of the
        /// lock the hero is engaged in: they are targeted at any distance inside
        /// the lock and through its walls (the approach navigates), and one the
        /// bot has not hurt for a while is only deprioritized. A dormant member
        /// (an Armed lock's, which refuses every hit) is never a target: the bot
        /// walks into its area instead (<see cref="Travel"/>).
        /// </summary>
        private Node2D SelectTarget(PlayerController player, float radiusOverride, out bool attacking) {
            attacking = false;
            Node2D best = null;
            float bestScore = float.MaxValue;
            Vector2 chest = player.GlobalPosition + new Vector2(0f, -36f);
            EncounterLock engaged = World.EngagedLock();
            foreach (Node2D foe in World.Foes()) {
                bool bossLike = PlaytestWorld.IsBossLike(foe);
                if (foe is BossController boss && (boss.IsMechanicInvulnerable || boss.IsHistoricalRecoverySuspended)) {
                    // Guarded: fight the guardians (they are foes too), not the boss.
                    if (boss.IsGuarded) continue;
                }
                if (PlaytestWorld.IsDormantMember(foe)) continue;
                bool lockTarget = IsLockTarget(foe, engaged);
                if (!bossLike && !lockTarget && Ignored(foe)) continue;
                // Bypass fights only what the run requires: bosses and the lock it stands in.
                if (_style == FightStyle.Bypass && !bossLike && !lockTarget) continue;
                float radius = radiusOverride > 0f ? radiusOverride
                    : bossLike ? BossEngageRadius
                    : lockTarget ? float.MaxValue
                    : EngageRadius;
                Vector2 center = foe.GlobalPosition + new Vector2(0f, -30f);
                float distance = chest.DistanceTo(center);
                if (distance > radius) continue;
                if (!lockTarget && distance > 110f && !World.LineOfSight(chest, center)) continue;
                // Far above with nothing to stand on under it: not reachable now
                // (a lock member there is hunted from below, HuntLockMembers).
                float dy = player.GlobalPosition.Y - foe.GlobalPosition.Y;
                if (dy > 330f && World.Nav.SegmentUnder(foe.GlobalPosition, 360f) == null) continue;
                float score = distance + (PlaytestWorld.IsAttacking(foe) ? -80f : 0f) + Mathf.Max(0f, dy - 120f);
                if (foe is ArenaGuardian) score -= 200f;
                if (lockTarget && _deprioritizedUntil.TryGetValue(foe.GetInstanceId(), out int until) && until > Frame) {
                    score += LockTargetDeprioritizedPenalty;
                }
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
            } else if (IsLockTarget(target, World.EngagedLock())) {
                // The run cannot go on without it: never ignored, only fought
                // after the members the bot can hurt (the stuck-lock watchdog
                // reports one nobody can reach).
                if (Frame - _targetProgressFrame > LockTargetDeprioritizeFrames) {
                    _deprioritizedUntil[_targetId] = Frame + LockTargetDeprioritizeFrames;
                    _targetProgressFrame = Frame;
                }
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
                // record it once and fight on instead of pressing forever. Frames
                // spent waiting out the ability lockout (at most 30 after the last
                // swing or special) are expected refusals and do not count.
                if (player.AbilityLockoutFramesRemaining <= 0 && ++_ultimateTapFrames > 40) {
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

            // A lock member standing on a surface the hero can reach but is not on
            // (a ledge, a yard, a gallery) is fought up there, never from below
            // (FIX-BOT, 2026-10-13).
            bool lockTarget = IsLockTarget(target, World.EngagedLock());
            Vector2? perch = lockTarget ? LockTargetPerch(player, target) : null;

            // Above us: jump and up-attack (drones, platform shooters).
            float above = -delta.Y; // positive: target above the feet
            if (perch == null && above > 95f && above < 330f && distance < 150f) {
                AirStrike(player, target, ref intent);
                return;
            }

            float strikeRange = StrikeRangeFor(player, target);
            if (_phase == Phase.Idle) {
                // A lock member that stands on a surface is struck from the ground:
                // a string thrown mid-jump on the way up to its ledge dropped the
                // hero back under it (FIX-BOT), so the flight finishes first.
                bool inRange = distance <= strikeRange && Mathf.Abs(delta.Y) < 80f
                    && (!lockTarget || player.IsOnFloor() || StandingSurface(target) == null);
                if (!inRange) {
                    CurrentAction = "approach";
                    float side = feet.X <= target.GlobalPosition.X ? -1f : 1f;
                    Vector2 standAt = new(target.GlobalPosition.X + side * 60f, target.GlobalPosition.Y);
                    bool close = Mathf.Abs(delta.Y) < 60f && distance < 300f;
                    if (perch.HasValue) {
                        if (!Go(player, perch.Value, ref intent, 30f, PerchMaxBelow)) FaceTarget(player, target, ref intent);
                    } else if (close && player.IsOnFloor() && (!lockTarget || LockWalkInClear(player, target))) {
                        // Same level and close: just walk in.
                        intent.MoveX = Mathf.Sign(delta.X);
                        if (lockTarget) WatchLockWalkIn(player);
                    } else if (lockTarget) {
                        // Close but something solid stands between (a breakable cell
                        // lock, a boulder, a wall of cover, a step), or not close: the
                        // agent's Break, jump or hop edges to the member's own
                        // surface. Never to a stand point 60 px off it, which can lie
                        // over another surface (Level 4's cell A, whose far wall
                        // stands 60 px from the slashers behind it).
                        if (close) CurrentAction = "approach_around";
                        Vector2 goal = LockTargetGoal(target, out float maxBelow);
                        if (!Go(player, goal, ref intent, 20f, maxBelow)) FaceTarget(player, target, ref intent);
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
                // C3: the confirm policy's hit-confirm Special replaces this frame's basic attack.
                if (TryConfirmSpecial(player, target, ref intent)) return;
                if (phaseFrame % 6 == 0) intent.Tap |= GameplayButtons.BasicAttack;
                bool hitboxActive = player.GetNodeOrNull<FTT.Combat.Hitbox>("MeleeHitbox")?.IsActive ?? false;
                bool stringDone = (_swingsSeen >= 3 && !hitboxActive) || phaseFrame > 130;
                if (stringDone) {
                    _phase = _style is FightStyle.Basics or FightStyle.Bypass ? Phase.Idle : Phase.Specials;
                    _phaseStart = Frame;
                }
                // Target slid out of range mid-string: chase.
                if (distance > strikeRange + StringChaseSlack) _phase = Phase.Idle;
                return;
            }

            // Phase.Specials: each Special as soon as it is ready (a pip in the Momentum gauge and its 2 s reuse delay clear) and the
            // hero is free to cast. Since 2026-10-08 a swing start or a cast
            // locks specials for 30 frames (BasicComboRules.AbilityLockoutFrames),
            // so the bot waits the hero's own Special gate out rather than
            // spending a refused tap. Before group A's D1 that gate is the
            // ability lockout; after it, the hit-confirm gate, so a landed swing
            // opens it at once (spec C §1.4(f)). The Ultimate keeps reading the
            // ability lockout, as the game does.
            CurrentAction = "specials";
            bool free = player.CurrentState != CharacterState.UsingSpecial
                && PlaytestWorld.SpecialGateFrames(player) <= 0;
            // The campaign kit (Legacy Unlock Schedule) may not have restored a slot yet.
            if (!player.IsAbilityUnlocked(AbilitySlot.Special1)) _special1Done = true;
            if (!player.IsAbilityUnlocked(AbilitySlot.Special2)) _special2Done = true;
            if (_special1Done && _special2Done) {
                _phase = Phase.Idle;
                return;
            }
            if (!_special1Done && free && PlaytestWorld.SpecialReady(player, AbilitySlot.Special1)) {
                intent.Tap |= GameplayButtons.Special1;
                _special1Done = true;
                CurrentAction = "special1";
            } else if (_special1Done && !_special2Done && free && phaseFrame > 4 && PlaytestWorld.SpecialReady(player, AbilitySlot.Special2)) {
                intent.Tap |= GameplayButtons.Special2;
                _special2Done = true;
                CurrentAction = "special2";
            } else if ((_special1Done || !PlaytestWorld.SpecialReady(player, AbilitySlot.Special1))
                && (_special2Done || !PlaytestWorld.SpecialReady(player, AbilitySlot.Special2)) && free) {
                _phase = Phase.Idle;
            } else if (phaseFrame > 120) {
                _phase = Phase.Idle;
            }
            // A Special on its reuse delay was cast already (or is not worth waiting for): move on.
            if (PlaytestWorld.SpecialReuseRemaining(player, AbilitySlot.Special1) > 0f && !_special1Done) _special1Done = true;
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
                    _airStrikeHopStage = 0;
                    intent.Held |= GameplayButtons.Jump;
                }
                return;
            }
            intent.MoveX = Mathf.Abs(dx) > 20f ? Mathf.Sign(dx) * 0.6f : 0f;
            // Velocity-driven like StrikeRect: hold to the apex, release one frame,
            // and press the second jump while the target is still well above.
            switch (_airStrikeHopStage) {
                case 0:
                    intent.Held |= GameplayButtons.Jump;
                    if (player.Velocity.Y >= 0f) {
                        _airStrikeHopStage = feet.Y - target.GlobalPosition.Y > 160f && player.RemainingJumps > 0 ? 1 : 3;
                    }
                    break;
                case 1:
                    _airStrikeHopStage = 2; // one frame released, so the next press is an edge
                    break;
                case 2:
                    intent.Held |= GameplayButtons.Jump;
                    if (player.Velocity.Y >= 0f) _airStrikeHopStage = 3;
                    break;
            }
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
                case RotatingGear gear:
                    // FIX-BOT: one press at a time, each re-planned from the
                    // gears' new turns (a press also turns the gears it drives).
                    return GoInteract(player, gear, ref intent, "gear", 26f, 30);
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
            // FIX-E (2026-10-13): a fracture an uncleared lock holds only posts a
            // notice when struck; drop it until the lock clears.
            CheckpointTrigger c => c.IsActivated || PlaytestWorld.IsHeldByLock(c),
            SequenceGlyph g => g.IsLit || g.Lock?.IsCompleted == true || g.Lock?.ExpectedGlyph != g,
            RescuableNPC n => n.IsRescued,
            DamageableEnvironmentObject d => d.IsDestroyed,
            ConductiveCoil coil => _coilPlan == null || !_coilPlan.TryGetValue(coil, out int turns) || coil.QuarterTurns == turns,
            RotatingGear gear => !World.GearPlan().TryGetValue(gear, out int presses) || presses == 0,
            PressurePlate plate => plate.ResolvePuzzleManager()?.IsCompleted != false
                || plate.CurrentWeight + plate.PlayerWeight + 0.001f < plate.RequiredWeight,
            _ => false
        };

        private static int TaskProgress(Node2D task) => task switch {
            DamageableEnvironmentObject d => d.CurrentHP * 10 + d.HitCount,
            ConductiveCoil c => c.QuarterTurns,
            RotatingGear g => g.QuarterTurns,
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

            // reachedAt: a stand point was already proved reachable (a plate's PlatePoint).
            void Consider(Node2D node, string kind, float bias, float maxDistance, bool reachedAt = false) {
                if (node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree() || TaskBlocked(node)) return;
                Vector2 delta = node.GlobalPosition - feet;
                float score = Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) * 1.5f + bias;
                if (Mathf.Abs(delta.X) > maxDistance || score >= bestScore) return;
                bool reachable;
                if (reachedAt) {
                    reachable = true;
                } else if (node is DamageableEnvironmentObject && PlaytestWorld.ShapeRect(node) is Rect2 r) {
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
                if (PlaytestWorld.IsHeldByLock(checkpoint)) continue;
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
                // FIX-BOT: a plate the engaged lock needs counts as reachable when
                // any of its stand points is (Level 6's east pan, whose centre the
                // dropped pumice boulder covers, was never a task).
                Consider(plate, "plate", -100f, 2000f, reachedAt: EngagedLockRequires(manager.PuzzleID));
            }
            // FIX-BOT: a gear train the engaged lock needs solved (Florence's print
            // shop): the gears the plan still has to press, nearest first.
            if (World.Gears.Count > 0) {
                foreach (KeyValuePair<RotatingGear, int> pair in World.GearPlan()) {
                    if (pair.Value <= 0 || !EngagedLockRequires(PlaytestWorld.PuzzleOf(pair.Key)?.PuzzleID)) continue;
                    Consider(pair.Key, "gear", 0f, 2400f);
                }
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
                    // A short hop swings on the way up instead (FIX-BOT, below).
                    if (_airStrikeShortHop ? t < 3 : (player.Velocity.Y < -40f || t < 3)) intent.Held |= GameplayButtons.Jump;
                    if (!_airStrikeSwung && t > 2 && (_airStrikeShortHop || player.Velocity.Y > -40f)) {
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
                    && Mathf.Abs(feet.X - Mathf.Clamp(feet.X, area.Position.X + 8f, area.End.X - 8f)) < 30f
                    && !PrefersBeside(player, area)) {
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
                        // FIX-BOT (inside an engaged lock): from a full jump's apex
                        // the down-air's box can end short of a target at the hero's
                        // own floor level — Level 6's wedge, its top 8 px under the
                        // gallery, which Joan's apex down-air never touched. Hop
                        // short and swing on the way up when the apex cannot reach.
                        float apexFeet = feet.Y - World.Nav.Hero.SingleRise(World.Nav.GravityScaleAt(feet.X, feet.Y));
                        _airStrikeShortHop = World.EngagedLock() != null
                            && apexFeet + DownAirReachBelowPixels < area.Position.Y + 8f;
                        intent.Held |= GameplayButtons.Jump;
                    }
                    return;
                }
            }

            // Walk to a stand point.
            (Vector2 point, float maxBelow) = StrikePoint(player, area);
            GoRaw(player, point, ref intent, 8f, maxBelow);
        }

        /// <summary>
        /// FIX-BOT (2026-10-13), inside an engaged lock: a target the hero can stand
        /// beside at its base, where the string lands every swing, is struck from
        /// there rather than by jumping up-attacks from under it — Level 11's
        /// shield array Alpha, on the gallery deck, took a minute of up-attacks
        /// through the deck from the ground route below.
        /// </summary>
        private bool PrefersBeside(PlayerController player, Rect2 area) =>
            World.EngagedLock() != null
            && (Reachable(player, new Vector2(area.Position.X - 20f, area.End.Y - 2f), 46f)
                || Reachable(player, new Vector2(area.End.X + 20f, area.End.Y - 2f), 46f));

        private Rect2 _airStrikeArea;
        private bool _airStrikeDown;
        /// <summary>The down-air strike in progress is a short hop that swings on the way up.</summary>
        private bool _airStrikeShortHop;
        /// <summary>
        /// How far below the feet the Story down-air box reaches (the bot's reading
        /// of <c>PlayerController</c>'s 125 px box, centred 62.5 px under the feet;
        /// the same reading the band checks above make).
        /// </summary>
        private const float DownAirReachBelowPixels = 125f;
        private int _airStrikeStage;
        /// <summary>AirStrike's jump stage: 0 first rise, 1 released, 2 second rise, 3 done.</summary>
        private int _airStrikeHopStage;

        /// <summary>
        /// How far above the feet a jumping up-attack reaches: the jump budget's
        /// rise, 10% short because the swing has to land near the apex, + the
        /// 150 px box (the same reach the old closed-form 1.9 x single rise gave).
        /// </summary>
        private float JumpStrikeReach => World.Nav.Hero.BestRise(1f) * 0.9f + 140f;
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
            var beside = candidates.GetRange(0, 2);
            candidates.Sort((a, b) => feet.DistanceTo(a.Point).CompareTo(feet.DistanceTo(b.Point)));
            if (PrefersBeside(player, area)) {
                beside.Sort((a, b) => feet.DistanceTo(a.Point).CompareTo(feet.DistanceTo(b.Point)));
                candidates.RemoveAll(c => beside.Contains(c));
                candidates.InsertRange(0, beside);
            }
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
            // Encounter locks: never push at an engaged lock's closed barrier, and
            // walk into the next Armed lock on the way (its exit is shut from
            // level build, so the way on runs through its fight).
            if (HoldInEngagedLock(player, ref intent)) return;
            CurrentAction = "travel";
            Vector2 goal = TravelGoal(player);
            Func<NavSegment, bool> isGoal = null;
            EncounterLock ahead = World.ArmedLockOnTheWay(player.GlobalPosition.X, goal.X);
            if (ahead != null) {
                // Any surface inside the area past its engage line will do (the
                // area may sit up a climb or down a drop from here); the bot then
                // walks to the entry point on it.
                CurrentAction = "lock_enter";
                goal = new Vector2(PlaytestWorld.EntryPointX(ahead, player.GlobalPosition.X), player.GlobalPosition.Y);
                Rect2 area = ahead.Area;
                float inset = HeroPhysics.BodyHalfWidth + EncounterLockRules.EngageInsetPixels;
                float lo = area.Position.X + inset, hi = area.End.X - inset;
                isGoal = segment => !segment.IsRope && segment.Right > lo && segment.Left < hi;
            }
            _travelDir = Mathf.Sign(goal.X - player.GlobalPosition.X) == 0 ? _travelDir : Mathf.Sign(goal.X - player.GlobalPosition.X);
            if (!Go(player, goal, ref intent, 30f, 300f, isGoal)) {
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

        /// <summary>
        /// Navigate toward a point; a Break step attacks the obstacle. Returns false
        /// on arrival. <paramref name="isGoal"/>, when given, names the goal
        /// segments instead of the point's own surface.
        /// </summary>
        private bool Go(PlayerController player, Vector2 goal, ref BotIntent intent, float arriveRadius, float maxBelow,
            Func<NavSegment, bool> isGoal = null) {
            bool moving = Agent.Drive(player, goal, ref intent, arriveRadius, maxBelow, isGoal);
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
        public static PlaytestBot Create(string name, PlaytestWorld world, CampaignLevel level, string enemyID = "chrono_slasher",
            SpecialPressPolicy policy = SpecialPressPolicy.Gated) => name switch {
            "bypass" => new StoryBot(world, FightStyle.Bypass, level, policy),
            "spam" => new StoryBot(world, FightStyle.Spam, level, policy),
            "basics" => new StoryBot(world, FightStyle.Basics, level, policy),
            "play" => new StoryBot(world, FightStyle.Play, level, policy),
            "feel" => new FeelProbeBot(world, enemyID),
            _ => throw new ArgumentException($"Unknown playtest bot '{name}' (bypass | spam | basics | play | feel).")
        };
    }
}
