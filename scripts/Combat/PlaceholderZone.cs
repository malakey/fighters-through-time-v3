using FTT.Core;
using Godot;

namespace FTT.Combat {

    public partial class PlaceholderZone : PooledNode, IPoolable {
        private float _lifetime;
        private float _tickTimer;
        private float _tickInterval;
        private float _damage;
        private int _ownerIndex;
        private StatusType _appliedStatus = StatusType.None;
        private float _statusDuration;
        private float _statusIntensity = 1f;
        private float _ownerSpeedMultiplier = 1f;
        private FTT.Characters.PlayerController _ownerPlayer;
        private Area2D _area;
        private ColorRect _visual;
        private ColorRect _border;
        private AnimatedSprite2D _authoredVisual;
        private CollisionShape2D _shape;

        // Refresh window slightly longer than one frame so the buff persists while
        // the owner remains inside and lapses right after leaving.
        private const float OwnerBuffRefreshSeconds = 0.05f;

        public override void _Ready() => AddToGroup("story_zone");

        public void Setup(float damage, float lifetime, float tickInterval, int ownerIndex,
                          Color color, float radius = 60f,
                          StatusType appliedStatus = StatusType.None,
                          float statusDuration = 0f,
                          float statusIntensity = 1f,
                          FTT.Characters.PlayerController ownerPlayer = null,
                          float ownerSpeedMultiplier = 1f,
                          AbilityData data = null) {
            _lifetime = lifetime;
            _tickInterval = tickInterval;
            _damage = damage;
            _ownerIndex = ownerIndex;
            _appliedStatus = appliedStatus;
            _statusDuration = statusDuration;
            _statusIntensity = statusIntensity <= 0f ? 1f : statusIntensity;
            _ownerPlayer = ownerPlayer;
            _ownerSpeedMultiplier = ownerSpeedMultiplier;

            EnsureNodes();
            bool usesAuthoredVisual = ApplyAuthoredVisual(data, radius);
            _visual.Size = new Vector2(radius * 2, radius * 2);
            _visual.Position = new Vector2(-radius, -radius);
            _visual.Color = new Color(color.R, color.G, color.B, 0.3f);
            _visual.Visible = !usesAuthoredVisual;
            _border.Size = new Vector2(radius * 2, 4);
            _border.Position = new Vector2(-radius, -radius);
            _border.Color = color;
            _border.Visible = !usesAuthoredVisual;
            ((CircleShape2D)_shape.Shape).Radius = radius;
            // The zone applies effects directly to overlapping combatant bodies,
            // so it monitors body layers rather than hitbox/hurtbox area layers.
            _area.CollisionLayer = 0;
            _area.CollisionMask = CollisionLayers.Player | CollisionLayers.Enemy;
            _area.Monitoring = true;
            _area.Monitorable = false;
        }

        private void EnsureNodes() {
            if (_area != null) return;
            _visual = new ColorRect { Name = "Visual", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_visual);
            _border = new ColorRect { Name = "Border", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_border);
            _authoredVisual = new AnimatedSprite2D {
                Name = "AuthoredVisual",
                Visible = false,
                ZIndex = 2
            };
            AddChild(_authoredVisual);
            _area = new Area2D { Name = "Area" };
            _shape = new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new CircleShape2D()
            };
            _area.AddChild(_shape);
            AddChild(_area);
        }

        private bool ApplyAuthoredVisual(AbilityData data, float radius) {
            if (_authoredVisual == null) return false;
            _authoredVisual.Visible = false;
            _authoredVisual.Stop();
            float scale = data?.AbilityID == "einstein_cosmological_constant"
                ? 2.15f
                : Mathf.Max(0.35f, radius / 96f);
            return AbilityVisualLibrary.Apply(_authoredVisual, data?.AbilityID, scale);
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            RefreshOwnerBuff();

            _tickTimer -= dt;
            if (_tickTimer <= 0) {
                _tickTimer = _tickInterval;
                Godot.Collections.Array<Node2D> bodies = _area.GetOverlappingBodies();
                using var bodiesLifetime = bodies.AsDisposable();
                foreach (Node2D body in bodies) {
                    ApplyTickTo(body);
                }
            }

            Modulate = new Color(1, 1, 1, 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 200f));
        }

        /// <summary>
        /// One damage/status pulse against an overlapping combatant body (audit
        /// Low "Kits/talents", Einstein rift incoherence). All zones share the
        /// same rules: damage is scaled by the owner's Story special-damage
        /// multiplier, rounded half-up to match the Fighter loadout convention
        /// (authored 1.5 ticks 2 in both modes, not a truncated 1), and dealt
        /// damage credits the owner's Ultimate Meter — the same contract the
        /// bespoke tick paths (e.g. Cleopatra's vortex) already honored. Public
        /// so tests can pulse a constructed target without a physics frame.
        /// </summary>
        public void ApplyTickTo(Node2D body) {
            int tickDamage = _damage > 0f
                ? ComputeTickDamage(_damage, _ownerPlayer?.StorySpecialDamageMultiplier ?? 1f)
                : 0;
            if (body is FTT.Characters.PlayerController pc) {
                if (pc.PlayerIndex == _ownerIndex) return;
                if (tickDamage > 0) CreditOwner(pc.ApplyDamage(tickDamage));
                if (_appliedStatus != StatusType.None && _statusDuration > 0f) {
                    pc.GetNodeOrNull<StatusController>("StatusController")
                        ?.ApplyStatus(_appliedStatus, _statusDuration, _statusIntensity);
                }
            } else if (body is FTT.Enemies.EnemyController enemy) {
                if (tickDamage > 0) CreditOwner(enemy.TakeDamage(tickDamage));
                if (_appliedStatus != StatusType.None && _statusDuration > 0f) {
                    enemy.ApplyStatusEffect(_appliedStatus, _statusDuration, _statusIntensity);
                }
            } else if (body is FTT.Characters.TrainingDummy) {
                Hurtbox hurtbox = body.GetNodeOrNull<Hurtbox>("Hurtbox");
                float dealt = hurtbox?.TakeHit(new HitPayload {
                    AttackerIndex = _ownerIndex,
                    AttackID = "placeholder_zone",
                    HitboxID = "tick",
                    AttackClass = AttackClass.Special,
                    Damage = tickDamage,
                    Knockback = new Vector2(1, -1),
                    HitstunDuration = 0.1f,
                    HitOrigin = GlobalPosition,
                    AttackerFacingRight = true,
                    AppliedStatus = _appliedStatus,
                    StatusDuration = _statusDuration,
                    StatusIntensity = _statusIntensity,
                    ScreenShakeIntensity = 0.1f,
                    ScreenShakeDuration = 0.08f
                }) ?? 0f;
                CreditOwner(dealt);
            }
        }

        /// <summary>
        /// The shared zone-tick rounding rule: half-up, exactly like
        /// <c>FighterLoadoutFactory.RoundDamage</c>, so an authored 1.5-damage
        /// tick means 2 in Story just as it does in Fighter.
        /// </summary>
        public static int ComputeTickDamage(float damage, float specialDamageMultiplier) =>
            System.Math.Max(0, (int)System.MathF.Round(
                damage * specialDamageMultiplier,
                System.MidpointRounding.AwayFromZero));

        private void CreditOwner(float damageApplied) {
            if (damageApplied <= 0f) return;
            if (_ownerPlayer == null || !IsInstanceValid(_ownerPlayer)) return;
            _ownerPlayer.AddInfluenceFromDamageDealt(damageApplied);
        }

        private void RefreshOwnerBuff() {
            if (_ownerSpeedMultiplier <= 1f || _ownerPlayer == null || !IsInstanceValid(_ownerPlayer)) return;
            Godot.Collections.Array<Node2D> bodies = _area.GetOverlappingBodies();
            using var bodiesLifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body == _ownerPlayer) {
                    _ownerPlayer.ApplyStorySpeedBuff(_ownerSpeedMultiplier, OwnerBuffRefreshSeconds);
                    return;
                }
            }
        }

        public void OnSpawn() {
            _lifetime = 0f;
            _tickTimer = 0f;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            if (_area != null) {
                _area.Monitoring = false;
                _area.Monitorable = false;
            }
            _lifetime = 0f;
            _tickTimer = 0f;
            _damage = 0f;
            _ownerIndex = -1;
            _appliedStatus = StatusType.None;
            _statusDuration = 0f;
            _statusIntensity = 1f;
            _ownerSpeedMultiplier = 1f;
            _ownerPlayer = null;
            if (_authoredVisual != null) {
                _authoredVisual.Stop();
                _authoredVisual.Visible = false;
            }
            if (_visual != null) _visual.Visible = true;
            if (_border != null) _border.Visible = true;
            Modulate = Colors.White;
        }
    }
}
