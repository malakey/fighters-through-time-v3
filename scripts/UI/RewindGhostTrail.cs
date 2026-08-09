using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Draws the Chronal Rewind ghost trail (Package 8 B6): fading copies of each
    /// player at the positions they actually occupied, sampled from the per-player
    /// <see cref="FTT.Environment.TemporalPositionHistory"/> that
    /// <c>CharacterFactory</c> attaches.
    ///
    /// <para>Lives in world space as a child of the running scene, not on the
    /// overlay's <c>CanvasLayer</c> — the trail has to sit with the level geometry,
    /// under the tint and scanlines rather than over them.</para>
    ///
    /// <para>Reads the history node from the outside and never writes to it, so
    /// nothing here touches <c>CharacterFactory</c> or the rewind simulation.</para>
    /// </summary>
    public partial class RewindGhostTrail : Node2D {
        public const string NodeName = "RewindGhostTrail";

        /// <summary>Ghosts drawn per player.</summary>
        public const int GhostCount = 6;

        /// <summary>Frames of history between consecutive ghosts.</summary>
        public const int GhostSpacingFrames = 7;

        /// <summary>Opacity of the freshest ghost; later ones scale down linearly.</summary>
        public const float LeadGhostAlpha = 0.42f;

        private readonly List<Sprite2D> _ghosts = new();
        private bool _active;

        public override void _Ready() {
            ZIndex = 4;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
        }

        /// <summary>Opacity of the ghost <paramref name="index"/> steps back in time.</summary>
        public static float GhostAlpha(int index) =>
            Mathf.Max(0f, LeadGhostAlpha * (1f - (float)index / GhostCount));

        /// <summary>How far back in time ghost <paramref name="index"/> samples.</summary>
        public static int GhostFramesAgo(int index) => (index + 1) * GhostSpacingFrames;

        /// <summary>Turns the trail on or off. Off releases every ghost sprite.</summary>
        public void SetActive(bool active) {
            _active = active;
            Visible = active;
            if (active) return;
            foreach (Sprite2D ghost in _ghosts) {
                if (GodotObject.IsInstanceValid(ghost)) ghost.Visible = false;
            }
        }

        public override void _Process(double delta) {
            if (!_active || !IsInsideTree()) return;
            Refresh();
        }

        /// <summary>
        /// Repositions the ghost sprites against the current history. Public so a
        /// test can drive one frame without waiting on the engine's process loop.
        /// </summary>
        public void Refresh() {
            int used = 0;
            Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();
            foreach (Node node in players) {
                if (node is not Node2D body || !GodotObject.IsInstanceValid(body)) continue;
                var history = body.GetNodeOrNull<FTT.Environment.TemporalPositionHistory>(
                    nameof(FTT.Environment.TemporalPositionHistory));
                if (history == null) continue;

                (Texture2D texture, Vector2 offset, Vector2 scale) = FindSpriteAppearance(body);
                for (int index = 0; index < GhostCount; index++) {
                    if (!history.TryGetFramesAgo(GhostFramesAgo(index), out Vector2 position)) break;
                    Sprite2D ghost = GhostAt(used++);
                    ghost.Texture = texture;
                    // The history samples the body root; the sprite hangs above it.
                    ghost.GlobalPosition = position + offset;
                    ghost.Scale = scale;
                    ghost.Modulate = new Color(0.55f, 0.95f, 1f, GhostAlpha(index));
                    ghost.Visible = texture != null;
                }
            }
            for (int index = used; index < _ghosts.Count; index++) {
                if (GodotObject.IsInstanceValid(_ghosts[index])) _ghosts[index].Visible = false;
            }
        }

        /// <summary>Ghost sprites currently allocated. Grows to a small fixed ceiling.</summary>
        public int AllocatedGhosts => _ghosts.Count;

        private Sprite2D GhostAt(int index) {
            while (_ghosts.Count <= index) {
                var ghost = new Sprite2D {
                    Name = $"Ghost_{_ghosts.Count}",
                    Visible = false,
                    TopLevel = true
                };
                AddChild(ghost);
                _ghosts.Add(ghost);
            }
            return _ghosts[index];
        }

        /// <summary>
        /// The player's current sprite frame plus the local transform it is drawn
        /// with, so a ghost looks like the character rather than like a generic blob
        /// sitting at its feet. Falls back to any child <c>Sprite2D</c>.
        /// </summary>
        private static (Texture2D Texture, Vector2 Offset, Vector2 Scale) FindSpriteAppearance(Node2D body) {
            if (body.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") is AnimatedSprite2D animated &&
                animated.SpriteFrames != null &&
                animated.SpriteFrames.HasAnimation(animated.Animation)) {
                int count = animated.SpriteFrames.GetFrameCount(animated.Animation);
                if (count > 0) {
                    Texture2D frame = animated.SpriteFrames.GetFrameTexture(
                        animated.Animation, Mathf.Clamp(animated.Frame, 0, count - 1));
                    return (frame, animated.Position, animated.Scale);
                }
            }
            Godot.Collections.Array<Node> children = body.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is Sprite2D sprite && sprite.Texture != null) {
                    return (sprite.Texture, sprite.Position, sprite.Scale);
                }
            }
            return (null, Vector2.Zero, Vector2.One);
        }

        /// <summary>
        /// Ensures the running scene carries exactly one trail node.
        /// Returns null outside the tree.
        /// </summary>
        public static RewindGhostTrail EnsureInstalled(Node anyNode) {
            if (anyNode == null || !anyNode.IsInsideTree()) return null;
            // Same host resolution as A3's VfxPresentationBinder, for the same
            // reason: the running scene when there is one, the caller's parent
            // otherwise, so a detached fixture still works.
            Node host = anyNode.GetTree()?.CurrentScene ?? anyNode.GetParent();
            if (host == null || !GodotObject.IsInstanceValid(host)) return null;
            var existing = host.GetNodeOrNull<RewindGhostTrail>(NodeName);
            if (existing != null) return existing;
            var trail = new RewindGhostTrail { Name = NodeName };
            host.AddChild(trail);
            return trail;
        }
    }
}
