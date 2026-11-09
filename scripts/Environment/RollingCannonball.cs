using System;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// A loose cannonball rolling along a deck (GR-LV3, ruling 14, the Dread Admiral's Phase 2).
    /// It is a <see cref="StoryCyclicHazard"/>, so the warning, one hit per cycle, the block rule,
    /// the freeze and the rewind policy are the cyclic hazard's own: warning at its rail, active
    /// while it rolls, cooldown out of sight. No physics: it follows the deck's top, read from
    /// <see cref="DeckTopAt"/>, and its hurt trigger is its own Area2D, so it blocks nobody.
    /// </summary>
    public partial class RollingCannonball : StoryCyclicHazard {
        /// <summary>Where it waits through its warning and starts to roll.</summary>
        public float StartX;
        /// <summary>Where the roll ends.</summary>
        public float EndX;
        /// <summary>Pixels a second along the x axis.</summary>
        public float RollSpeed = 300f;
        /// <summary>The ball's radius; its centre rides this far over the deck.</summary>
        public float Radius = 22f;
        /// <summary>The top of the deck at an x.</summary>
        public Func<float, float> DeckTopAt;

        private float _rollElapsed;

        /// <summary>Builds a ball, dormant until a level sets <see cref="StoryCyclicHazard.Enabled"/>.</summary>
        public static RollingCannonball Create(string hazardID, float startX, float endX, float rollSpeed,
            float warningSeconds, float cooldownSeconds, int damage, Func<float, float> deckTopAt) {
            var ball = new RollingCannonball {
                Name = $"Cannonball_{hazardID.Replace('.', '_')}",
                HazardID = hazardID,
                StartX = startX,
                EndX = endX,
                RollSpeed = rollSpeed,
                WarningDuration = warningSeconds,
                ActiveDuration = Mathf.Abs(endX - startX) / rollSpeed,
                CooldownDuration = cooldownSeconds,
                Damage = damage,
                Enabled = false,
                RewindPolicy = StoryRewindPolicy.PreserveCurrentState,
                DeckTopAt = deckTopAt,
                CollisionLayer = CollisionLayers.Trigger
            };
            ball.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = ball.Radius } });
            ball.AddChild(MakeDisc("WarningVisual", ball.Radius, new Color(1f, 0.62f, 0.2f, 0.75f)));
            ball.AddChild(MakeDisc("ActiveVisual", ball.Radius, new Color(0.12f, 0.12f, 0.14f)));
            ball.Position = new Vector2(startX, deckTopAt(startX) - ball.Radius);
            return ball;
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            if (IsTimeFrozen || !Enabled) return;
            _rollElapsed = Phase == HazardPhase.Active ? _rollElapsed + (float)delta : 0f;
            float x = Mathf.MoveToward(StartX, EndX, RollSpeed * _rollElapsed);
            Position = new Vector2(x, DeckTopAt(x) - Radius);
        }

        private static Polygon2D MakeDisc(string name, float radius, Color color) {
            const int Sides = 16;
            var points = new Vector2[Sides];
            for (int index = 0; index < Sides; index++) {
                points[index] = Vector2.FromAngle(Mathf.Tau * index / Sides) * radius;
            }
            return new Polygon2D { Name = name, Polygon = points, Color = color, Visible = false };
        }
    }
}
