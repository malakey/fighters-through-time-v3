using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Diagnostics {

    /// <summary>
    /// The step-2 "feel probe": a fixed, scripted input timeline that exercises
    /// the parts of Story control a player feels first: run start and stop, a
    /// jump arc, a fast-fall cut into a rise, a whiffed 3-hit string, a mashed string, and a string plus both
    /// Specials on a real enemy spawned in front of the hero. Every frame is
    /// logged by <see cref="FeelLog"/>, and <see cref="CaptureActive"/> brackets
    /// the frames worth rendering to PNG when the runner is windowed.
    ///
    /// <para>Segments are named so the analysis can measure each one in
    /// isolation. The timeline is frame-exact under <c>--fixed-fps 60</c>.</para>
    /// </summary>
    public sealed class FeelProbeBot : PlaytestBot {
        public FeelProbeBot(PlaytestWorld world, string enemyID) : base(world) => _enemyID = enemyID;

        public override string Name => "feel";

        private readonly string _enemyID;
        private int _t = -1;
        private float _startX;
        private EnemyController _target;
        private bool _ultimateAccepted;
        private bool _fastFallLanded;

        public bool Done { get; private set; }
        public bool CaptureActive { get; private set; }
        public string Segment { get; private set; } = "settle";
        public EnemyController Target => _target;

        // Segment start frames (relative to the first controllable frame). The
        // jump runs first, at the spawn point: the Level 2 start has open sky
        // there, while the run carries the hero under a low platform (370..630
        // at y 720) that clipped every hero but Joan on the first probes. The
        // fast-fall jump (2026-10-08) runs second, at the same spot, so every
        // later mark moved 100 frames on.
        private const int JumpStart = 30, FastFallStart = 130, RunStart = 230, RunRelease = 290, WhiffStart = 360,
            MashStart = 460, SpawnAt = 560, HitStringStart = 600, Special1At = 648, Special2At = 760,
            UltimateAt = 860, End = 960;
        // Since the 2026-10-08 ability lockout a connecting string pushes the
        // first legal Special 1 tick to about 702-715 (each chain hit re-arms 30
        // frames and hitstop pauses the count), so the Special 2, Ultimate and
        // end marks moved 60-80 frames later to keep every cast inside its
        // segment.
        /// <summary>
        /// Jump held long enough to reach the full-hold apex (no short-hop cut).
        /// The roster's longest held rise is Cleopatra's, about 41 frames since
        /// the 2026-10-08 apex hang (StoryAirPhysics.SimulateJump), so 60 keeps a
        /// wide margin. Holding past a landing is harmless: only a fresh press jumps.
        /// </summary>
        private const int JumpHoldFrames = 60;
        /// <summary>
        /// Frames into the fast-fall jump at which Down is held, mid-rise for every
        /// hero: the ramped fast-fall must cut the rise short without snapping.
        /// Jump stays held, so the short hop never mixes into the measurement.
        /// </summary>
        private const int FastFallDownFrame = 12;

        protected override void Decide(PlayerController player, ref BotIntent intent) {
            _t++;
            if (_t == 0) {
                _startX = player.GlobalPosition.X;
                ClearLevelEnemies(player);
            }
            CaptureActive = _t >= WhiffStart - 10 && _t < End;
            if (_t < JumpStart) {
                Segment = "settle";
            } else if (_t < FastFallStart) {
                Segment = "jump";
                if (_t < JumpStart + JumpHoldFrames) intent.Held |= GameplayButtons.Jump;
            } else if (_t < RunStart) {
                Segment = "fastfall";
                int s = _t - FastFallStart;
                if (s == 0) _fastFallLanded = false;
                if (s > 2 && player.IsOnFloor()) _fastFallLanded = true;
                // Jump and then Down held until the landing; both released on the
                // ground so the crouch never leaks into the run segment.
                if (!_fastFallLanded) {
                    intent.Held |= GameplayButtons.Jump;
                    if (s >= FastFallDownFrame) {
                        intent.Held |= GameplayButtons.Down;
                        intent.MoveY = 1f;
                    }
                }
            } else if (_t < RunRelease) {
                Segment = "run";
                intent.MoveX = 1f;
            } else if (_t < WhiffStart) {
                Segment = "stop";
            } else if (_t < MashStart) {
                Segment = "whiff_string";
                // A player who presses on rhythm: one press per 12 frames, all
                // inside the 24-frame chain buffer.
                int s = _t - WhiffStart;
                if (s == 0 || s == 12 || s == 24) intent.Tap |= GameplayButtons.BasicAttack;
            } else if (_t < SpawnAt) {
                Segment = "mash_string";
                // A player who mashes: a press every 6 frames for a second.
                int s = _t - MashStart;
                if (s < 60 && s % 6 == 0) intent.Tap |= GameplayButtons.BasicAttack;
            } else if (_t < HitStringStart) {
                Segment = "spawn";
                if (_t == SpawnAt) SpawnTarget(player);
            } else if (_t < UltimateAt) {
                Segment = _t < Special1At ? "hit_string" : _t < Special2At ? "special1" : "special2";
                int s = _t - HitStringStart;
                if (s == 0 || s == 12 || s == 24) intent.Tap |= GameplayButtons.BasicAttack;
                // The ability lockout (2026-10-08) refuses a special for 30 frames
                // after each swing start or cast, so each special is pressed from
                // its segment start until the cast is accepted: the segment then
                // measures the wait plus the cast. A tap only registers on a tick
                // whose previous frame was released, so the bot holds off until a
                // cast can land (see CastCanLand) and the first legal tick always
                // carries a fresh press.
                if (_t >= Special1At && _t < Special2At && player.SpecialOneCooldownTimer <= 0f && CastCanLand(player)) {
                    intent.Tap |= GameplayButtons.Special1;
                }
                if (_t >= Special2At && player.SpecialTwoCooldownTimer <= 0f && CastCanLand(player)) {
                    intent.Tap |= GameplayButtons.Special2;
                }
                if (_target != null && GodotObject.IsInstanceValid(_target) && _target.IsAlive) {
                    FaceTarget(player, _target, ref intent);
                }
            } else if (_t < End) {
                Segment = "ultimate";
                // Pressed until accepted, for the same reason as the specials.
                if (player.CurrentState == CharacterState.UsingUltimate) _ultimateAccepted = true;
                if (!_ultimateAccepted && CastCanLand(player)) intent.Tap |= GameplayButtons.Ultimate;
            } else {
                Segment = "done";
                Done = true;
            }
            CurrentAction = Segment;
            CurrentTarget = _target;
        }

        /// <summary>
        /// True when a special or Ultimate pressed this tick can be accepted. The
        /// bot is sampled before the hero's own tick counts the lockout down, so
        /// a remaining 1 (with no hitstop to pause it) reads 0 when the press is
        /// polled; a running special or Ultimate refuses the press outright.
        /// </summary>
        private static bool CastCanLand(PlayerController player) =>
            player.AbilityLockoutFramesRemaining <= 1
            && player.HitstopFramesRemaining <= 0
            && player.CurrentState != CharacterState.UsingSpecial
            && player.CurrentState != CharacterState.UsingUltimate;

        /// <summary>
        /// The probe measures the hero, not the level: every authored enemy and
        /// boss is removed at the first controllable frame so nothing shoots the
        /// probe mid-measurement (Level 2's opening archer did, on the first run).
        /// </summary>
        private static void ClearLevelEnemies(PlayerController player) {
            Godot.Collections.Array<Node> nodes = player.GetTree().GetNodesInGroup("Enemies");
            using var lifetime = nodes.AsDisposable();
            foreach (Node node in nodes) {
                if (GodotObject.IsInstanceValid(node) && node is Node2D body && body is not PlayerController) body.QueueFree();
            }
        }

        private void SpawnTarget(PlayerController player) {
            Node parent = player.GetParent();
            Vector2 at = player.GlobalPosition + new Vector2(player.IsFacingRight ? 110f : -110f, -20f);
            _target = EnemyFactory.Spawn(_enemyID, parent, at);
        }
    }

    /// <summary>One CSV row per unpaused physics frame while the feel probe runs.</summary>
    public sealed class FeelLog {
        private readonly StringBuilder _csv = new();
        private int _rows;

        public FeelLog() {
            _csv.AppendLine(string.Join(",",
                "t", "segment", "move_x", "held", "pressed", "state", "x", "y", "vx", "vy", "on_floor",
                "anim", "anim_frame", "anim_fps", "anim_speed_scale", "sprite_scale_y",
                "hitstop", "hitbox_active", "combo", "hp", "meter",
                "enemy_state", "enemy_hp", "enemy_hitstop", "enemy_x", "enemy_vx", "enemy_anim", "enemy_anim_frame", "capture"));
        }

        public void Record(FeelProbeBot bot, PlayerController player) {
            if (player == null) return;
            PlayerInputFrame input = player.CurrentInputFrame;
            AnimatedSprite2D sprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            Hitbox hitbox = player.GetNodeOrNull<Hitbox>("MeleeHitbox");
            string anim = sprite?.Animation.ToString() ?? "";
            double fps = sprite?.SpriteFrames != null && sprite.SpriteFrames.HasAnimation(anim)
                ? sprite.SpriteFrames.GetAnimationSpeed(anim) : 0;
            EnemyController enemy = bot.Target != null && GodotObject.IsInstanceValid(bot.Target) ? bot.Target : null;
            AnimatedSprite2D enemySprite = enemy?.GetNodeOrNull<AnimatedSprite2D>("Presentation/AnimatedSprite2D");
            var inv = CultureInfo.InvariantCulture;
            _csv.AppendLine(string.Join(",",
                _rows.ToString(inv), bot.Segment, input.MoveX.ToString(inv), ((int)input.Held).ToString(inv), ((int)input.Pressed).ToString(inv),
                player.CurrentState.ToString(),
                player.GlobalPosition.X.ToString("F1", inv), player.GlobalPosition.Y.ToString("F1", inv),
                player.Velocity.X.ToString("F1", inv), player.Velocity.Y.ToString("F1", inv),
                player.IsOnFloor() ? "1" : "0",
                anim, (sprite?.Frame ?? -1).ToString(inv), fps.ToString("F1", inv),
                (sprite?.SpeedScale ?? 0f).ToString("F2", inv), (sprite?.Scale.Y ?? 0f).ToString("F3", inv),
                player.HitstopFramesRemaining.ToString(inv), hitbox != null && hitbox.IsActive ? "1" : "0",
                player.GroundComboCounter.ToString(inv), player.CurrentHP.ToString(inv),
                player.CurrentUltimateMeter.ToString("F1", inv),
                enemy?.CurrentState.ToString() ?? "", (enemy?.CurrentHP ?? -1).ToString(inv),
                (enemy?.HitstopFramesRemaining ?? -1).ToString(inv),
                (enemy?.GlobalPosition.X ?? 0f).ToString("F1", inv), (enemy?.Velocity.X ?? 0f).ToString("F1", inv),
                enemySprite?.Animation.ToString() ?? "", (enemySprite?.Frame ?? -1).ToString(inv),
                bot.CaptureActive ? "1" : "0"));
            _rows++;
        }

        public string Csv => _csv.ToString();
    }
}
