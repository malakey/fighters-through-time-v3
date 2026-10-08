using System.Collections.Generic;
using Godot;
using FTT.Combat;
using FTT.Core;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Package 12 W4 (GAP-10c) — Cleopatra's sand decoy (design §5, Desert
    /// Mirage kit rule): "Desert Mirage leaves a sand decoy at its origin that
    /// enemies and the CPU briefly target (1 s); Royal Aegis's decoy shield uses
    /// this decoy."
    ///
    /// <para><b>Story only.</b> The lure is a Story AI rule: while a decoy
    /// stands, every <c>EnemyController</c> chasing its owner aims its approach
    /// and its attacks at the decoy (<see cref="TryGetLure"/>). It carries a
    /// hurtbox on the owner's hurtbox layer, so a hit aimed at it lands on the
    /// sand, not on Cleopatra: the decoy's own Royal Aegis shield absorbs it
    /// (when she owns the node), and a hit the decoy cannot absorb disperses it.
    /// A dispersed or expired decoy lures nobody.</para>
    ///
    /// <para><b>Royal Aegis — a separate recipient (D02a/D02c).</b> The decoy's
    /// shield is its own: its capacity is the same 10 %-of-max-HP grant, it has
    /// its own grant identity (the accepted Mirage cast — a repeated grant ID
    /// is refused), its own depletion and its own 8-second lifetime, and it
    /// never pools capacity with Cleopatra's shield. In practice the decoy's
    /// one-second life ends the shield first.</para>
    ///
    /// <para><b>Not churned.</b> One decoy node lives under its Desert Mirage
    /// ability for the life of the fighter and is re-armed on every cast, so
    /// there is no instantiate/free per cast to pool. It implements both freeze
    /// latches, so Time Freeze and the death-rewind freeze hold its lifetime.</para>
    ///
    /// <para><b>Fighter Mode: not implemented.</b> A deterministic decoy needs
    /// a construct entity the Fighter CPU would read as a target; the sim
    /// persistent-object path cannot express "a target the opponent's AI
    /// prefers" without new CPU observation fields. Recorded as
    /// <c>DEFER-FIGHTER-SAND-DECOY</c>.</para>
    /// </summary>
    public partial class SandDecoyNode : Node2D,
        FTT.Environment.IStoryRewindSimulation, FTT.Environment.IStoryTimeFreezable {

        /// <summary>The design's lure window: 1 s.</summary>
        public const int LifetimeFrames = 60;

        private static readonly List<SandDecoyNode> Active = new();

        private PlayerController _owner;
        private Hurtbox _hurtbox;
        private ColorRect _visual;
        private int _framesRemaining;
        private bool _frozen;
        private bool _timeFrozen;

        private float _shieldPoints;
        private int _shieldFramesRemaining;
        private int _lastShieldGrantId;

        public bool IsActive => _framesRemaining > 0;
        public int FramesRemaining => _framesRemaining;
        public float ShieldPoints => _shieldPoints;
        public PlayerController DecoyOwner => _owner;

        /// <summary>
        /// The lure an enemy chasing <paramref name="target"/> should aim at
        /// instead: that player's own live decoy. False when none stands.
        /// </summary>
        public static bool TryGetLure(PlayerController target, out Vector2 position) {
            position = default;
            if (target == null) return false;
            for (int index = Active.Count - 1; index >= 0; index--) {
                SandDecoyNode decoy = Active[index];
                if (!IsInstanceValid(decoy) || !decoy.IsActive) {
                    Active.RemoveAt(index);
                    continue;
                }
                if (decoy._owner != target) continue;
                position = decoy.GlobalPosition;
                return true;
            }
            return false;
        }

        /// <summary>
        /// True when a live decoy stands at <paramref name="position"/> (within
        /// <paramref name="tolerancePixels"/>): the point an enemy telegraphed is
        /// the sand, not its owner. Playtest pass 2026-10-04 (P3): a shot that
        /// re-acquires its target at fire time uses this to keep a lured
        /// telegraph on the decoy.
        /// </summary>
        public static bool IsLureAt(Vector2 position, float tolerancePixels = 1f) {
            float toleranceSquared = tolerancePixels * tolerancePixels;
            for (int index = Active.Count - 1; index >= 0; index--) {
                SandDecoyNode decoy = Active[index];
                if (!IsInstanceValid(decoy) || !decoy.IsActive) {
                    Active.RemoveAt(index);
                    continue;
                }
                if (decoy.GlobalPosition.DistanceSquaredTo(position) <= toleranceSquared) return true;
            }
            return false;
        }

        public override void _Ready() => EnsureNodes();

        public override void _ExitTree() {
            Active.Remove(this);
        }

        /// <summary>Stands the decoy at <paramref name="origin"/> for one second.</summary>
        public void Arm(PlayerController owner, Vector2 origin) {
            EnsureNodes();
            _owner = owner;
            GlobalPosition = origin;
            _framesRemaining = LifetimeFrames;
            _shieldPoints = 0f;
            _shieldFramesRemaining = 0;
            _hurtbox.OwnerPlayerIndex = owner?.PlayerIndex ?? 0;
            _hurtbox.CollisionLayer = CollisionLayers.HurtboxLayerForFighterSlot(_hurtbox.OwnerPlayerIndex);
            _hurtbox.CollisionMask = CollisionLayers.HurtboxMaskForFighterSlot(_hurtbox.OwnerPlayerIndex);
            _hurtbox.SetMonitorableSafe(true);
            Visible = true;
            if (!Active.Contains(this)) Active.Add(this);
        }

        /// <summary>
        /// Royal Aegis's decoy grant: refills this decoy's own shield to
        /// <paramref name="capacity"/> and restarts its own lifetime. A repeated
        /// <paramref name="grantId"/> is refused outright (D02a).
        /// </summary>
        public bool GrantShield(float capacity, int lifetimeFrames, int grantId) {
            if (!IsActive || capacity <= 0f) return false;
            if (grantId != 0 && grantId == _lastShieldGrantId) return false;
            _lastShieldGrantId = grantId;
            _shieldPoints = capacity;
            _shieldFramesRemaining = lifetimeFrames;
            return true;
        }

        /// <summary>Ends the lure at once (dispersed, owner death, reset).</summary>
        public void Disperse() {
            _framesRemaining = 0;
            _shieldPoints = 0f;
            _shieldFramesRemaining = 0;
            Visible = false;
            _hurtbox?.SetMonitorableSafe(false);
            Active.Remove(this);
        }

        public void SetStoryRewindFrozen(bool frozen) => _frozen = frozen;
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

        public override void _PhysicsProcess(double delta) => Tick();

        /// <summary>One gameplay tick; public so tests can drive it without a physics frame.</summary>
        public void Tick() {
            if (!IsActive || _frozen || _timeFrozen) return;
            if (_owner == null || !IsInstanceValid(_owner) || _owner.CurrentState == CharacterState.Dead) {
                Disperse();
                return;
            }
            if (_shieldFramesRemaining > 0 && --_shieldFramesRemaining <= 0) _shieldPoints = 0f;
            if (--_framesRemaining <= 0) Disperse();
        }

        /// <summary>
        /// A hit aimed at the decoy: its own shield absorbs it; anything the
        /// shield cannot fully absorb disperses the sand. Never reaches
        /// Cleopatra and never deals her damage, meter or Rally.
        /// </summary>
        private float OnDecoyHit(HitPayload hit) {
            if (!IsActive) return 0f;
            float incoming = Mathf.Max(0f, hit.Damage);
            if (_shieldPoints > 0f && incoming <= _shieldPoints) {
                _shieldPoints -= incoming;
                return 0f;
            }
            Disperse();
            return 0f;
        }

        private void EnsureNodes() {
            if (_hurtbox != null) return;
            AddToGroup("sand_decoy");
            // Both freeze sweeps (Time Freeze and the death rewind) reach it here.
            AddToGroup("persistent_construct");
            _visual = new ColorRect {
                Name = "Visual",
                Size = new Vector2(36f, 72f),
                Position = new Vector2(-18f, -72f),
                Color = new Color(0.86f, 0.74f, 0.45f, 0.55f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_visual);
            _hurtbox = new Hurtbox { Name = "Hurtbox", Monitoring = false, Monitorable = false };
            var shape = new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(36f, 72f) },
                Position = new Vector2(0f, -36f)
            };
            _hurtbox.AddChild(shape);
            _hurtbox.OnHit += OnDecoyHit;
            AddChild(_hurtbox);
            Visible = false;
        }
    }
}
