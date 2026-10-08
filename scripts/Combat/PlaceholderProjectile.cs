using Godot;
using FTT.Core;

namespace FTT.Combat {

    public partial class PlaceholderProjectile : PooledNode, IPoolable {
        private float _speed;
        private bool _movingRight;
        private float _lifetime;
        private Hitbox _hitbox;
        private ColorRect _visual;
        private ColorRect _trail;
        private AnimatedSprite2D _authoredVisual;
        private CollisionShape2D _shape;

        /// <summary>
        /// When true, the projectile despawns on its first confirmed contact and
        /// raises <see cref="Impacted"/> with the impact position (heavy detonating
        /// projectiles like Einstein's E=mc²). Cleared on despawn.
        /// </summary>
        public bool DetonateOnImpact { get; set; }

        /// <summary>Raised once at the impact position when a detonating projectile connects.</summary>
        public event System.Action<Vector2> Impacted;

        /// <summary>
        /// Package 13 W7a — the burst-on-terrain primitive (Story half; the sim's
        /// is <c>FighterProjectileBurstRules</c>). Opt-in, from
        /// <c>AbilityData.ProjectileBurstsOnTerrain</c>: a detonating projectile
        /// also raises <see cref="Impacted"/> where it strikes terrain or a wall
        /// (an <c>Environment</c>-layer ray along each step's travel) or reaches
        /// its maximum range (its lifetime), instead of vanishing silently.
        /// Cleared on despawn.
        /// </summary>
        public bool BurstsOnTerrain { get; set; }

        /// <summary>
        /// Package 13 W7a (L03): the shot stops (vanishes) where it strikes
        /// terrain — line of sight for the turret's straight bolts. Implied by
        /// <see cref="BurstsOnTerrain"/>. Cleared on despawn.
        /// </summary>
        public bool StopsOnTerrain { get; set; }

        /// <summary>
        /// Package 13 W7a (L03): aims the shot straight along an arbitrary
        /// direction at its configured speed (the turret's bolts fly at the
        /// target, not only horizontally). Call after <see cref="Setup"/>.
        /// </summary>
        public void ConfigureDirection(Vector2 direction) {
            if (direction == Vector2.Zero) return;
            Vector2 unit = direction.Normalized();
            float speed = _speed;
            _movingRight = unit.X >= 0f;
            _speed = speed * Mathf.Abs(unit.X);
            _verticalVelocity = speed * unit.Y;
            _arcGravity = 0f;
        }

        /// <summary>
        /// R12 (2026-10-04 fix pass): the execution of the cast that fired this
        /// shot (<see cref="Hitbox.SourceExecutionSerial"/>), stamped by
        /// <c>BaseSpecial.SpawnPlaceholderProjectile</c> after <see cref="Setup"/>;
        /// 0 for a shot no cast owns. Reset on every Setup and despawn (pooled).
        /// </summary>
        public int SourceExecutionSerial {
            get => _hitbox?.SourceExecutionSerial ?? 0;
            set { if (_hitbox != null) _hitbox.SourceExecutionSerial = value; }
        }

        /// <summary>
        /// Package 13 W7a (L03): a construct's bolt — Basic-class, impulse-free,
        /// non-launching, construct delivery (no Rally reclaim), never a
        /// Shield-Breaker. Call after <see cref="Setup"/>.
        /// </summary>
        public void ConfigureConstructHit() {
            if (_hitbox == null) return;
            _hitbox.AttackClass = AttackClass.Basic;
            _hitbox.BlockChargeCost = 0;
            _hitbox.Unblockable = false;
            _hitbox.ShieldBreaker = false;
            _hitbox.Launches = false;
            _hitbox.Delivery = HitDelivery.Construct;
            _hitbox.KnockbackForce = Vector2.Zero;
            _hitbox.HitstunDuration = 0.15f;
        }

        /// <summary>
        /// Package 11 A4 (Resonance V7.6). Authoring flag for Lincoln's Rail
        /// Breaker traversal node: only a projectile marked breakable can be
        /// destroyed by a Rail Charge. Beams, persistent zones and
        /// environmental hazards carry no such flag and are never destroyed;
        /// an unbreakable projectile resolves normally against his existing
        /// armor. Default TRUE for ordinary enemy shots, which is the whole
        /// class the design calls breakable; author false on a shot that must
        /// survive a charge.
        /// </summary>
        public bool IsBreakable { get; set; } = true;

        private float _verticalVelocity;
        private float _arcGravity;

        /// <summary>
        /// Turns the flat shot into a lobbed arc: an initial vertical velocity in
        /// px/s (negative = up) pulled down by the given gravity in px/s². Used by
        /// slow space-holding lobs (Mozart's Fortissimo Wave); cleared on despawn.
        /// </summary>
        public void ConfigureArc(float initialVerticalVelocity, float gravity) {
            _verticalVelocity = initialVerticalVelocity;
            _arcGravity = gravity;
        }

        /// <summary>
        /// Strips the authored on-contact status so a two-stage projectile
        /// (e.g. Yorick's skull + wave) applies its status once, from the stage
        /// that owns it, instead of on both contact and detonation.
        /// </summary>
        public void ClearContactStatus() {
            if (_hitbox == null) return;
            _hitbox.AppliedStatus = FTT.Core.StatusType.None;
            _hitbox.StatusDuration = 0f;
        }

        /// <summary>
        /// Package 13 W7a: the contact stage of a two-stage (bursting) shot is
        /// the minor hit — its own damage, no status, no knockback, no launch;
        /// the burst carries all three. Mirrors the sim's contact stage.
        /// </summary>
        public void MakeMinorContactStage() {
            ClearContactStatus();
            if (_hitbox == null) return;
            _hitbox.KnockbackForce = Vector2.Zero;
            _hitbox.Launches = false;
        }

        /// <summary>
        /// Package 13 W7b: replaces the contact knockback after <see cref="Setup"/>
        /// (Mozart's Requiem Chord contact holds without an impulse so its burst
        /// lands). Reset by the next Setup.
        /// </summary>
        public void OverrideContactKnockback(Vector2 knockback) {
            if (_hitbox == null) return;
            _hitbox.KnockbackForce = knockback;
            _hitbox.Launches = false;
        }

        /// <summary>
        /// Package 13 W7b: the HP the detonating contact actually dealt (0 when
        /// blocked or absorbed), readable inside <see cref="Impacted"/>.
        /// </summary>
        public float LastImpactDamage { get; private set; }

        /// <summary>Owning local player slot (mirrors the hitbox); -1 marks an enemy shot.</summary>
        public int OwnerPlayerIndex => _hitbox?.OwnerPlayerIndex ?? -1;

        /// <summary>
        /// 2026-10-04 fix pass: true from <see cref="Setup"/> until the shot is
        /// despawned — the shot is in flight. A released shot is PARKED under the
        /// pool manager, not removed: it stays inside the tree, stays in the
        /// <c>story_projectile</c> group (the template scene and <see cref="_Ready"/>
        /// both put it there) and keeps its last owner slot on the hitbox, so a
        /// scan of that group must skip anything not live. The Level 13 Mirror's
        /// nearest-hostile-projectile read (<c>MirrorParadoxDecisionAdapter</c>)
        /// used to take every parked player shot for incoming fire at the spot it
        /// despawned. Never-Setup warm-up instances read false.
        /// </summary>
        public bool IsLive { get; private set; }

        /// <summary>Current horizontal travel velocity in pixels per second (+X right).</summary>
        public float HorizontalVelocity => _movingRight ? _speed : -_speed;

        public override void _Ready() {
            AddToGroup("story_projectile");
        }

        public void Setup(float damage, Vector2 knockback, float speed, bool movingRight,
                          int ownerIndex, Color color, Vector2 size = default, float lifetime = 3f,
                          FTT.Characters.PlayerController sourcePlayer = null, AbilityData data = null) {
            if (size == default) size = new Vector2(24, 12);
            _speed = speed;
            _movingRight = movingRight;
            _lifetime = lifetime;

            EnsureNodes();
            bool usesAuthoredVisual = ApplyAuthoredVisual(data, movingRight);
            _visual.Size = size;
            _visual.Position = -size / 2;
            _visual.Color = color;
            _visual.Visible = !usesAuthoredVisual;
            _trail.Size = new Vector2(size.X * 0.6f, size.Y * 0.5f);
            _trail.Position = new Vector2(movingRight ? -size.X * 0.6f : size.X * 0.5f, -size.Y * 0.25f);
            _trail.Color = new Color(color.R, color.G, color.B, 0.4f);
            _trail.Visible = !usesAuthoredVisual;
            ((RectangleShape2D)_shape.Shape).Size = size;

            _hitbox.AttackID = data?.AbilityID ?? "placeholder_projectile";
            _hitbox.HitboxID = "projectile";
            // M08 (Package 12 W3): the ability's AUTHORED hit contract (class,
            // hitstun, launch flag, delivery, origin) replaces the old
            // slot-derived attack class. A data-less shot keeps the historical
            // Special / 0.2 s / direct defaults, reset every spawn (pooled).
            _hitbox.AttackClass = AttackClass.Special;
            _hitbox.BlockChargeCost = 0;
            _hitbox.Unblockable = false;
            _hitbox.HitstunDuration = 0.2f;
            _hitbox.Launches = knockback != Vector2.Zero;
            _hitbox.Delivery = HitDelivery.DirectHit;
            _hitbox.Origin = HitOrigin.Special;
            _hitbox.ApplyAbilityHitContract(data);
            _hitbox.Damage = damage;
            _hitbox.KnockbackForce = knockback;
            _hitbox.AppliedStatus = data?.AppliedStatus ?? FTT.Core.StatusType.None;
            _hitbox.StatusDuration = data?.StatusDuration ?? 0f;
            _hitbox.StatusIntensity = data?.StatusIntensity ?? 1f;
            _hitbox.ScreenShakeIntensity = data?.ScreenShakeIntensity ?? 0.2f;
            _hitbox.ScreenShakeDuration = data?.ScreenShakeDuration ?? 0.1f;
            _hitbox.OwnerPlayerIndex = ownerIndex;
            _hitbox.SourcePlayer = sourcePlayer;
            // R12: pooled reset; the firing cast stamps it after Setup.
            _hitbox.SourceExecutionSerial = 0;
            _hitbox.CollisionLayer = CollisionLayers.Projectile;
            _hitbox.CollisionMask = CollisionLayers.ProjectileMask;
            _hitbox.Monitorable = true;
            // Hostile-to-player shots clear with enemy projectiles on a Chronal
            // Rewind. Owner index alone is not enough: the Level 13 Mirror clone
            // fires with a non-negative player index, so the firing controller's
            // hostility flag is consulted at grouping time (audit H-8).
            bool hostileToPlayer = ownerIndex < 0 || (sourcePlayer?.IsStoryHostile ?? false);
            if (hostileToPlayer) AddToGroup("enemy_projectile");
            else RemoveFromGroup("enemy_projectile");
            _hitbox.Activate();
            IsLive = true;
        }

        private void EnsureNodes() {
            if (_hitbox != null) return;
            _visual = new ColorRect { Name = "Visual", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_visual);
            _trail = new ColorRect { Name = "Trail", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_trail);
            _authoredVisual = new AnimatedSprite2D {
                Name = "AuthoredVisual",
                Visible = false,
                ZIndex = 2
            };
            AddChild(_authoredVisual);
            _hitbox = new Hitbox { Name = "Hitbox" };
            _shape = new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D()
            };
            _hitbox.AddChild(_shape);
            AddChild(_hitbox);
            _hitbox.HitConfirmed += OnHitConfirmed;
        }

        private bool ApplyAuthoredVisual(AbilityData data, bool movingRight) {
            if (_authoredVisual == null) return false;
            _authoredVisual.Visible = false;
            _authoredVisual.Stop();
            return AbilityVisualLibrary.Apply(_authoredVisual, data?.AbilityID, 0.38f,
                flipH: !movingRight);
        }

        private void OnHitConfirmed(HitPayload payload, float damageApplied) {
            if (!DetonateOnImpact) return;
            // One detonation per flight: the release below is deferred past the
            // physics flush this handler runs in, so go logically inert now or a
            // second hurtbox in the same flush would detonate again.
            DetonateOnImpact = false;
            _hitbox?.Deactivate();
            LastImpactDamage = damageApplied;
            Vector2 impactPosition = GlobalPosition;
            Impacted?.Invoke(impactPosition);
            ReturnToPool();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) {
                // W7a: a bursting shot bursts at its maximum range.
                if (BurstsOnTerrain && DetonateOnImpact) BurstAt(GlobalPosition);
                else ReturnToPool();
                return;
            }

            var pos = GlobalPosition;
            Vector2 from = pos;
            pos.X += (_movingRight ? _speed : -_speed) * dt;
            if (_arcGravity != 0f || _verticalVelocity != 0f) {
                pos.Y += _verticalVelocity * dt;
                _verticalVelocity += _arcGravity * dt;
            }
            if ((BurstsOnTerrain || StopsOnTerrain) && TryStrikeTerrain(from, pos, out Vector2 terrainPoint)) {
                GlobalPosition = terrainPoint;
                if (BurstsOnTerrain && DetonateOnImpact) BurstAt(terrainPoint);
                else ReturnToPool();
                return;
            }
            GlobalPosition = pos;

            Modulate = new Color(1, 1, 1, Mathf.Min(1f, _lifetime * 2f));
        }

        /// <summary>
        /// W7a: the burst stage outside a hurtbox contact — at a terrain strike or
        /// the maximum range. One burst per flight, like the contact path.
        /// </summary>
        private void BurstAt(Vector2 point) {
            DetonateOnImpact = false;
            _hitbox?.Deactivate();
            Impacted?.Invoke(point);
            ReturnToPool();
        }

        /// <summary>
        /// W7a: true when this step's travel crosses solid terrain (the
        /// <c>Environment</c> layer — walls, floors, solid props; one-way
        /// platforms are not terrain). <paramref name="point"/> is the contact.
        /// Pure query, legal outside a physics signal flush.
        /// </summary>
        private bool TryStrikeTerrain(Vector2 from, Vector2 to, out Vector2 point) {
            point = to;
            if (!IsInsideTree() || from == to) return false;
            PhysicsDirectSpaceState2D space = GetWorld2D()?.DirectSpaceState;
            if (space == null) return false;
            var query = PhysicsRayQueryParameters2D.Create(from, to, CollisionLayers.Environment);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            using Godot.Collections.Dictionary hit = space.IntersectRay(query);
            if (hit == null || hit.Count == 0) return false;
            point = hit["position"].AsVector2();
            return true;
        }

        public void OnSpawn() {
            _lifetime = 0f;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            IsLive = false;
            _hitbox?.Deactivate();
            if (_hitbox != null) _hitbox.SourceExecutionSerial = 0;
            RemoveFromGroup("enemy_projectile");
            _speed = 0f;
            _lifetime = 0f;
            _movingRight = true;
            _verticalVelocity = 0f;
            _arcGravity = 0f;
            DetonateOnImpact = false;
            BurstsOnTerrain = false;
            StopsOnTerrain = false;
            Impacted = null;
            LastImpactDamage = 0f;
            if (_authoredVisual != null) {
                _authoredVisual.Stop();
                _authoredVisual.Visible = false;
                _authoredVisual.FlipH = false;
            }
            if (_visual != null) _visual.Visible = true;
            if (_trail != null) _trail.Visible = true;
            Modulate = Colors.White;
        }
    }

}
