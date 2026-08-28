using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7.2 Stasis Echo — the manual rewind's constructive half (Story only):
    /// a frozen, translucent copy of the player left at the rewind origin.
    /// Lives 10 seconds from the moment playback ends and survives room
    /// transitions. It never moves. It weighs down pressure plates and holds
    /// switches, absorbs one enemy projectile (then shatters), and counts as
    /// a one-way platform the player can stand on. It has no hitbox, enemies
    /// ignore it, at most one exists at a time (a new manual rewind replaces
    /// it), and death-rewinds leave no Echo. Strictly Story-side.
    /// </summary>
    public partial class StasisEcho : AnimatableBody2D {
        /// <summary>
        /// V7.3 body layer: Environment (the player stands on their past self)
        /// PLUS PersistentObject, so pressure plates physically receive the
        /// Echo through their authored mask (Player | PersistentObject) and
        /// the searchlight occlusion ray can find it. Hitboxes stay
        /// indifferent: the Hitbox pipeline reacts only to Hurtbox AREAS, and
        /// the Echo's body is not one.
        /// </summary>
        public const uint BodyLayer = CollisionLayers.Environment | CollisionLayers.PersistentObject;

        /// <summary>The single live Echo, if any.</summary>
        public static StasisEcho Current { get; private set; }

        private float _lifeSeconds;
        private bool _projectileAbsorbed;
        private Area2D _absorbArea;

        /// <summary>Seconds of life left. Test seam.</summary>
        public float LifeSecondsRemaining => _lifeSeconds;

        /// <summary>True after the Echo has eaten its one projectile. Test seam.</summary>
        public bool HasAbsorbedProjectile => _projectileAbsorbed;

        /// <summary>
        /// Spawns the Echo at the rewind origin, replacing any earlier one.
        /// Parented to the scene root so a room transition inside the level
        /// cannot free it with a room container.
        /// </summary>
        public static StasisEcho Spawn(SceneTree tree, Vector2 origin, PlayerController player, float lifeSeconds) {
            if (tree?.CurrentScene == null) return null;
            if (Current != null && GodotObject.IsInstanceValid(Current)) Current.QueueFree();
            var echo = new StasisEcho {
                Name = "StasisEcho",
                GlobalPosition = origin,
                _lifeSeconds = Mathf.Max(0.1f, lifeSeconds)
            };
            tree.CurrentScene.CallDeferred(Node.MethodName.AddChild, echo);
            Current = echo;
            _ = player; // The copy borrows the player's silhouette when art lands.
            return echo;
        }

        public override void _Ready() {
            // One-way platform body: the player can stand on their past self;
            // nothing collides against them sideways. V7.3: the layer also
            // carries PersistentObject so plates and the searchlight ray
            // physically receive the Echo (the old Environment-only layer
            // made the plate's authored mask unreachable).
            CollisionLayer = BodyLayer;
            CollisionMask = 0;
            SyncToPhysics = false;
            var shape = new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(40f, 64f) },
                OneWayCollision = true
            };
            AddChild(shape);

            // The absorb surface: one enemy projectile shatters against the
            // Echo. Projectiles overlap the Player layer, so the Echo exposes a
            // player-layer area without being a combatant (no hurtbox, no HP).
            _absorbArea = new Area2D {
                Name = "AbsorbArea",
                CollisionLayer = CollisionLayers.Player,
                CollisionMask = CollisionLayers.Projectile,
                Monitoring = true,
                Monitorable = true
            };
            _absorbArea.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(40f, 64f) }
            });
            _absorbArea.AreaEntered += OnAreaEntered;
            AddChild(_absorbArea);

            // Translucent past-self placeholder until character art is wired.
            AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(40f, 64f),
                Position = new Vector2(-20f, -32f),
                Color = new Color(0.4f, 0.9f, 1f, 0.45f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }

        public override void _ExitTree() {
            if (Current == this) Current = null;
        }

        /// <summary>Physics-path tests need a live Echo without the full
        /// manual-rewind commit that normally sets its lifetime.</summary>
        internal void SetLifeSecondsForTesting(float seconds) => _lifeSeconds = seconds;

        public override void _PhysicsProcess(double delta) {
            _lifeSeconds -= (float)delta;
            if (_lifeSeconds <= 0f) QueueFree();
        }

        private void OnAreaEntered(Area2D area) {
            if (_projectileAbsorbed) return;
            // Only enemy projectiles shatter the Echo; friendly fire and
            // overlapping trigger volumes pass through.
            if (area is not FTT.Combat.Hitbox hitbox || hitbox.OwnerPlayerIndex >= 0) return;
            _projectileAbsorbed = true;
            AbsorbProjectile(area);
        }

        /// <summary>Absorbs one projectile and shatters. Test seam.</summary>
        public void AbsorbProjectile(Node2D projectileHitbox) {
            Node projectileRoot = projectileHitbox?.GetParent();
            if (projectileRoot is Enemies.EnemyProjectile projectile && IsInstanceValid(projectile)) {
                if (PoolManager.Instance != null) PoolManager.Instance.Release(projectile);
                else projectile.QueueFree();
            }
            QueueFree();
        }
    }
}
