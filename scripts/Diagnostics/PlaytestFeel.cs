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
    /// jump arc, a whiffed 3-hit string, a mashed string, and a string plus both
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

        public bool Done { get; private set; }
        public bool CaptureActive { get; private set; }
        public string Segment { get; private set; } = "settle";
        public EnemyController Target => _target;

        // Segment start frames (relative to the first controllable frame). The
        // jump runs first, at the spawn point: the Level 2 start has open sky
        // there, while the run carries the hero under a low platform (370..630
        // at y 720) that clipped every hero but Joan on the first probes.
        private const int JumpStart = 30, RunStart = 130, RunRelease = 190, WhiffStart = 260,
            MashStart = 360, SpawnAt = 460, HitStringStart = 500, Special1At = 548, Special2At = 600,
            UltimateAt = 680, End = 780;
        /// <summary>Jump held long enough to reach the full-hold apex (no short-hop cut).</summary>
        private const int JumpHoldFrames = 40;

        protected override void Decide(PlayerController player, ref BotIntent intent) {
            _t++;
            if (_t == 0) {
                _startX = player.GlobalPosition.X;
                ClearLevelEnemies(player);
            }
            CaptureActive = _t >= WhiffStart - 10 && _t < End;
            if (_t < JumpStart) {
                Segment = "settle";
            } else if (_t < RunStart) {
                Segment = "jump";
                if (_t < JumpStart + JumpHoldFrames) intent.Held |= GameplayButtons.Jump;
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
                if (_t == Special1At) intent.Tap |= GameplayButtons.Special1;
                if (_t == Special2At) intent.Tap |= GameplayButtons.Special2;
                if (_target != null && GodotObject.IsInstanceValid(_target) && _target.IsAlive) {
                    FaceTarget(player, _target, ref intent);
                }
            } else if (_t < End) {
                Segment = "ultimate";
                if (_t == UltimateAt) intent.Tap |= GameplayButtons.Ultimate;
            } else {
                Segment = "done";
                Done = true;
            }
            CurrentAction = Segment;
            CurrentTarget = _target;
        }

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
