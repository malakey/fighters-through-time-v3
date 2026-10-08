using Godot;
using FTT.Combat;
using FTT.Characters;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Movement — Relativity Warp, a <b>spacetime fold</b> (E04, Package 13 W7a).
    /// On the press a visible destination ghost appears up to 4 units away in the
    /// held direction (horizontal, vertical or diagonal); after a 10-frame
    /// startup Einstein relocates there <b>instantly</b>, with no travel frames,
    /// so gaps, platforms, hazards and attacks in between are simply crossed.
    /// The destination needs full-body clearance: it is shortened to the farthest
    /// point his body can occupy along the fold (a shape cast against solid
    /// terrain). He is hittable through the startup — a hit cancels the fold with
    /// the cooldown spent — and gains no invulnerability. The fold ends in the
    /// float window (Up/Down steered; fast-fall cancels it). Distance, startup
    /// and cooldown come from the authored MovementAbilityData;
    /// <c>KitMotionRules</c> pins the startup the sim shares.
    /// </summary>
    public partial class EinsteinRelativityWarp : BaseSpecial {

        private const float FloatDuration = 1.0f;

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2): the warp's
        /// reduced-gravity float window lasts 20 frames longer.
        /// </summary>
        public const string ExtendedFloatPerkKey = "extended_float";
        /// <summary>V7.6 Extended Float: additional float frames at 60 Hz.</summary>
        public const int ExtendedFloatBonusFrames = 20;

        private Vector2 _warpDirection;
        private Vector2 _startPosition;
        private Vector2 _destination;
        private float _warpDistance = KitMotionRules.RelativityWarpDistanceUnits * KitMotionRules.StoryPixelsPerUnit;
        private ColorRect _ghost;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        /// <summary>The fold's resolved (clearance-shortened) destination. Test seam.</summary>
        public Vector2 FoldDestination => _destination;

        /// <summary>True while the destination ghost is shown (the startup). Test seam.</summary>
        public bool GhostVisible => _ghost != null && IsInstanceValid(_ghost) && _ghost.Visible;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            EmitCharacterVfx(
                "res://scenes/vfx/einstein/EinsteinRelativityWarpVfx.tscn",
                Owner.GlobalPosition + new Vector2(0f, -28f));
            _warpDistance = MovementData?.DistanceMoved > 0f
                ? MovementData.DistanceMoved
                : KitMotionRules.RelativityWarpDistanceUnits * KitMotionRules.StoryPixelsPerUnit;

            float hInput = Owner.CurrentInputFrame.Horizontal;
            // Godot 2D Y is down and the vertical axis is Down minus Up, so a
            // negative value warps upward. Since §2.7 that comes from the real
            // Up input (W / stick up / dpad-up), not from a held Jump.
            float vInput = Owner.CurrentInputFrame.Vertical;
            _warpDirection = new Vector2(hInput, vInput);
            if (_warpDirection == Vector2.Zero) {
                _warpDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            }
            _warpDirection = _warpDirection.Normalized();
            _startPosition = Owner.GlobalPosition;
            _destination = ResolveDestination(_startPosition);
            Owner.Velocity = Vector2.Zero;
            ShowGhost(_destination);
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            HideGhost();
            // Instant relocation — the startup already resolved clearance, but
            // the world may have moved (a platform, a construct); re-resolve from
            // where the fold began so the landing is still clear.
            _destination = ResolveDestination(_startPosition);
            Owner.GlobalPosition = _destination;
            Owner.Velocity = Vector2.Zero;
            // The float window follows every fold that ends in the air.
            // IsOnFloor still reports the pre-fold contact, so an upward fold
            // from the ground counts as airborne by its own displacement.
            if (!Owner.IsOnFloor() || _destination.Y < _startPosition.Y - 1f) {
                Owner.StoryFloatTimer = FloatDuration
                    + (Owner.HasStoryPerk(ExtendedFloatPerkKey)
                        ? ExtendedFloatBonusFrames / 60f
                        : 0f);
            }
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Relativity Warp",
                StartPosition = _startPosition,
                EndPosition = _destination
            });
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        /// <summary>E04: a hit during the startup cancels the fold — the ghost goes with it.</summary>
        protected override void OnInterrupted() => HideGhost();

        public override void _PhysicsProcess(double delta) {
            // F5: the fold is caster-bound — it holds with its frozen caster.
            if (CastClockSuspended) return;
            // The startup holds him in place (hittable, no invulnerability).
            if (CurrentPhase == AbilityPhase.Startup && Owner != null) Owner.Velocity = Vector2.Zero;
            base._PhysicsProcess(delta);
        }

        public override void _ExitTree() {
            if (_ghost != null && IsInstanceValid(_ghost)) _ghost.QueueFree();
            _ghost = null;
            base._ExitTree();
        }

        /// <summary>
        /// E04 clearance: the farthest point along the fold at which Einstein's
        /// whole collision body is clear of solid terrain — a shape cast of his
        /// own body shape. With no physics space (a headless unit harness) the
        /// full distance stands.
        /// </summary>
        private Vector2 ResolveDestination(Vector2 start) {
            Vector2 motion = _warpDirection * _warpDistance;
            var space = Owner?.GetWorld2D()?.DirectSpaceState;
            Shape2D bodyShape = FindBodyShape(out Transform2D shapeTransform);
            if (space == null || bodyShape == null) return start + motion;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = bodyShape,
                Transform = new Transform2D(0f, start + shapeTransform.Origin),
                Motion = motion,
                CollideWithAreas = false,
                CollideWithBodies = true,
                CollisionMask = FTT.Core.CollisionLayers.Environment
            };
            float[] fractions = space.CastMotion(query);
            float safe = fractions != null && fractions.Length > 0 ? Mathf.Clamp(fractions[0], 0f, 1f) : 1f;
            return start + motion * safe;
        }

        private Shape2D FindBodyShape(out Transform2D transform) {
            transform = Transform2D.Identity;
            if (Owner == null) return null;
            Godot.Collections.Array<Node> children = Owner.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is CollisionShape2D shape && shape.Shape != null && !shape.Disabled) {
                    transform = shape.Transform;
                    return shape.Shape;
                }
            }
            return null;
        }

        private void ShowGhost(Vector2 at) {
            Node parent = Owner?.GetParent();
            if (parent == null) return;
            if (_ghost == null || !IsInstanceValid(_ghost)) {
                // Placeholder presentation: a translucent silhouette at the
                // destination (production art is Package 10).
                _ghost = new ColorRect {
                    Name = "RelativityWarpGhost",
                    Size = new Vector2(36f, 72f),
                    Color = new Color(0.55f, 0.75f, 1f, 0.35f),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
            }
            if (_ghost.GetParent() != parent) {
                _ghost.GetParent()?.RemoveChild(_ghost);
                parent.AddChild(_ghost);
            }
            _ghost.GlobalPosition = at + new Vector2(-18f, -72f);
            _ghost.Visible = true;
        }

        private void HideGhost() {
            if (_ghost == null || !IsInstanceValid(_ghost)) return;
            _ghost.Visible = false;
        }
    }
}
