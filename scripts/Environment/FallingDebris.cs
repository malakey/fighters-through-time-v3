using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>Telegraph -> fall -> impact -> spent.</summary>
    public enum FallingDebrisPhase { Telegraph, Falling, Impact, Spent }

    /// <summary>
    /// Package 11 A3 (V7.6): one piece of Collapse Tremor debris.
    ///
    /// Era-flavored rubble that spawns above the camera near the player. It
    /// telegraphs for <see cref="TelegraphSeconds"/> (0.75 s — a glowing crack
    /// overhead plus a marker on the landing surface) before it falls, so the
    /// hit is always answerable.
    ///
    /// The hit is a <b>Basic-class</b> environmental hit: it costs one shield
    /// charge when blocked and deals no HP, it deals
    /// <see cref="CollapseTremorRules.DamageFractionOfMaxHP"/> of max HP
    /// otherwise, it applies a small knockback, and it <b>never triggers
    /// hitstop</b> — a tremor is pressure, not a stinger. Damage routes through
    /// <see cref="PlayerController.ApplyEnvironmentalDamage"/>, the V7.3
    /// chokepoint, so Defy-flag consumption, Rally accounting and the victim
    /// meter all behave exactly as they do for every other environmental
    /// source.
    ///
    /// <b>Enemies are unaffected.</b> The Tremor is the timeline coming apart
    /// around the hero, not a second combat participant.
    ///
    /// Pooled: every mutable field is reset in <see cref="OnSpawn"/> and
    /// <see cref="OnDespawn"/>.
    /// </summary>
    public partial class FallingDebris : Node2D, IPoolable {

        /// <summary>Pool ID and scene-tree group.</summary>
        public const string PoolID = "falling_debris";

        [Export] public float TelegraphSeconds = CollapseTremorRules.DebrisTelegraphSeconds;
        [Export] public float FallSpeed = 900f;
        [Export] public float ImpactSeconds = 0.35f;
        [Export] public Vector2 Knockback = new(140f, -180f);

        /// <summary>Where the piece is headed. Set by the spawner before the fall.</summary>
        public float TargetY { get; private set; }

        public FallingDebrisPhase Phase { get; private set; } = FallingDebrisPhase.Telegraph;

        /// <summary>True once this piece has resolved its single hit (or missed).</summary>
        public bool HasResolved { get; private set; }

        /// <summary>True while the piece occupies one of the two airborne slots.</summary>
        public bool IsAirborne => Phase == FallingDebrisPhase.Telegraph || Phase == FallingDebrisPhase.Falling;

        private float _timer;
        private Node2D _telegraphVisual;
        private Node2D _bodyVisual;
        private bool _rewindFrozen;

        public override void _Ready() {
            AddToGroup(PoolID);
            _telegraphVisual = GetNodeOrNull<Node2D>("Telegraph");
            _bodyVisual = GetNodeOrNull<Node2D>("Body");
            ApplyPhaseVisuals();
        }

        /// <summary>
        /// Arms one piece: it telegraphs above <paramref name="impactPosition"/>
        /// and then falls to it.
        /// </summary>
        public void Launch(Vector2 impactPosition, float spawnHeight = 520f) {
            GlobalPosition = new Vector2(impactPosition.X, impactPosition.Y - Mathf.Max(120f, spawnHeight));
            TargetY = impactPosition.Y;
            Phase = FallingDebrisPhase.Telegraph;
            _timer = Mathf.Max(0.05f, TelegraphSeconds);
            HasResolved = false;
            ApplyPhaseVisuals();
        }

        /// <summary>The death-rewind / collapse-beat world freeze also holds debris.</summary>
        public void SetFrozen(bool frozen) => _rewindFrozen = frozen;

        public override void _PhysicsProcess(double delta) {
            if (_rewindFrozen || Phase == FallingDebrisPhase.Spent) return;
            Step((float)delta);
        }

        /// <summary>Pure-ish step, directly callable by the pin tests.</summary>
        internal void Step(float delta) {
            switch (Phase) {
                case FallingDebrisPhase.Telegraph:
                    _timer -= delta;
                    if (_timer > 0f) return;
                    Phase = FallingDebrisPhase.Falling;
                    ApplyPhaseVisuals();
                    return;
                case FallingDebrisPhase.Falling:
                    Position = new Vector2(Position.X, Position.Y + FallSpeed * delta);
                    if (GlobalPosition.Y < TargetY) return;
                    GlobalPosition = new Vector2(GlobalPosition.X, TargetY);
                    Phase = FallingDebrisPhase.Impact;
                    _timer = ImpactSeconds;
                    ApplyPhaseVisuals();
                    ResolveImpact();
                    return;
                case FallingDebrisPhase.Impact:
                    _timer -= delta;
                    if (_timer > 0f) return;
                    Phase = FallingDebrisPhase.Spent;
                    ApplyPhaseVisuals();
                    if (PoolManager.Instance != null) PoolManager.Instance.Release(this);
                    else QueueFree();
                    return;
            }
        }

        private void ResolveImpact() {
            if (HasResolved) return;
            HasResolved = true;
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not PlayerController player) return;
            if (!GodotObject.IsInstanceValid(player)) return;
            if (player.GlobalPosition.DistanceTo(GlobalPosition) > CollapseTremorRules.DebrisHitRadiusPixels) return;
            ApplyTo(player);
        }

        /// <summary>
        /// The hit itself. Internal so the pin tests exercise the exact path
        /// the impact takes — blockable as a Basic-class hit, a 5%-max-HP
        /// fraction otherwise, small knockback, never any hitstop.
        /// </summary>
        internal int ApplyTo(PlayerController player) {
            if (player == null || !GodotObject.IsInstanceValid(player)) return 0;
            int damage = CollapseTremorRules.DebrisDamage(player.MaximumHP);
            // Blockable as a Basic-class hit: one shield charge, no HP. The
            // block system is reached by the same node path PlayerController
            // itself uses, so no new PlayerController surface is introduced
            // (A7a owns the shared non-damaging block entry point in Wave 2 —
            // this call site moves onto it then).
            if (player.GetNodeOrNull<FTT.Combat.BlockSystem>("BlockSystem") is FTT.Combat.BlockSystem block
                && block.ResolveHit(new FTT.Combat.HitPayload {
                    AttackerIndex = -1,
                    TargetIndex = player.PlayerIndex,
                    AttackID = "collapse_tremor_debris",
                    AttackClass = FTT.Combat.AttackClass.Basic,
                    Damage = damage,
                    HitOrigin = GlobalPosition,
                    ExemptFromHitstop = true
                }) != FTT.Combat.BlockResult.NotBlocked) {
                return 0;
            }
            int applied = player.ApplyEnvironmentalDamage(damage, appliesHitstop: false);
            if (applied > 0) {
                float direction = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
                player.Velocity += new Vector2(Knockback.X * direction, Knockback.Y);
            }
            return applied;
        }

        private void ApplyPhaseVisuals() {
            if (_telegraphVisual != null) _telegraphVisual.Visible = Phase == FallingDebrisPhase.Telegraph;
            if (_bodyVisual != null) {
                _bodyVisual.Visible = Phase == FallingDebrisPhase.Falling || Phase == FallingDebrisPhase.Impact;
            }
        }

        public void OnSpawn() {
            Phase = FallingDebrisPhase.Telegraph;
            _timer = Mathf.Max(0.05f, TelegraphSeconds);
            TargetY = 0f;
            HasResolved = false;
            _rewindFrozen = false;
            Visible = true;
            SetPhysicsProcess(true);
            ApplyPhaseVisuals();
        }

        public void OnDespawn() {
            Phase = FallingDebrisPhase.Spent;
            _timer = 0f;
            TargetY = 0f;
            HasResolved = false;
            _rewindFrozen = false;
            Visible = false;
            SetPhysicsProcess(false);
            ApplyPhaseVisuals();
        }
    }
}
