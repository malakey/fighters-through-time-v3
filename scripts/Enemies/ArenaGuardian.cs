using System;
using Godot;
using FTT.Core;

namespace FTT.Enemies {

    /// <summary>
    /// Package 12 W9 (GAP-07) — a destroyable arena object that shields a boss
    /// while it stands. The Chronal Inventor's Phase 2 siphon coils are the first
    /// user (design §6: "deploys two siphon coils at the arena corners that shield
    /// him until destroyed — the level's mirror-coil routing lesson, weaponized").
    ///
    /// <para>
    /// The level places guardians (corners are arena geometry) and registers each
    /// with <see cref="BossController.RegisterGuardian"/>; the boss refuses damage
    /// while any registered guardian is alive. A guardian is struck through an
    /// ordinary <c>EnemyHurtbox</c>, so basics, specials, projectiles and the
    /// shape-query kit deliveries all reach it, and the shared Time Freeze /
    /// world-held gates in <c>Hitbox</c> and <c>Hurtbox</c> refuse strikes while
    /// the world is held. It takes no stun, knockback or status: it is a machine.
    /// Damage dealt to it earns no meter or Rally (it returns 0 to the hit
    /// pipeline), so a coil can never be farmed for Influence.
    /// </para>
    /// </summary>
    public partial class ArenaGuardian : Node2D, FTT.Combat.IDamageable {
        public const string GroupName = "boss_guardian";
        public static readonly Color CoilColor = new(0.55f, 0.85f, 1f);

        [Export] public string GuardianID = "";
        [Export] public int MaxHP = 60;

        public int CurrentHP { get; private set; }

        /// <summary>Raised once, the frame the guardian is destroyed.</summary>
        public event Action<ArenaGuardian> Destroyed;

        public bool IsAlive => CurrentHP > 0 && !IsQueuedForDeletion();

        private FTT.Combat.Hurtbox _hurtbox;
        private ColorRect _visual;
        private ProgressBar _hpBar;
        private bool _built;

        public override void _Ready() {
            Build();
            AddToGroup(GroupName);
        }

        public override void _ExitTree() {
            if (_hurtbox != null) _hurtbox.OnHit -= OnHurtboxHit;
        }

        private void Build() {
            if (_built) return;
            _built = true;
            CurrentHP = Math.Max(1, MaxHP);
            _visual = new ColorRect {
                Name = "Visual",
                Size = new Vector2(36f, 120f),
                Position = new Vector2(-18f, -120f),
                Color = CoilColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_visual);
            _hpBar = new ProgressBar {
                Name = "HPBar",
                MinValue = 0,
                MaxValue = CurrentHP,
                Value = CurrentHP,
                ShowPercentage = false,
                Size = new Vector2(48f, 6f),
                Position = new Vector2(-24f, -134f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_hpBar);
            _hurtbox = new FTT.Combat.Hurtbox {
                Name = "Hurtbox",
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.EnemyHurtbox,
                CollisionMask = 0,
                Monitoring = false,
                Monitorable = true,
                Position = new Vector2(0f, -60f)
            };
            _hurtbox.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(40f, 120f) }
            });
            _hurtbox.OnHit += OnHurtboxHit;
            AddChild(_hurtbox);
        }

        public float TakeDamage(in FTT.Combat.HitPayload hit) => OnHurtboxHit(hit);

        /// <summary>Direct damage entry for scripted callers and tests.</summary>
        public void ApplyDamage(int amount) {
            Build();
            if (!IsAlive || amount <= 0) return;
            CurrentHP = Math.Max(0, CurrentHP - amount);
            if (_hpBar != null) _hpBar.Value = CurrentHP;
            if (CurrentHP <= 0) Destroy();
        }

        private float OnHurtboxHit(FTT.Combat.HitPayload hit) {
            // Only player attacks damage a guardian; enemy hitboxes share index -1.
            if (hit.AttackerIndex < 0) return 0f;
            ApplyDamage(Mathf.Max(0, Mathf.RoundToInt(hit.Damage)));
            return 0f;
        }

        private void Destroy() {
            if (_hurtbox != null) _hurtbox.SetMonitorableSafe(false);
            if (_visual != null) _visual.Color = new Color(0.25f, 0.25f, 0.28f, 0.6f);
            if (_hpBar != null) _hpBar.Visible = false;
            EventBus.Instance?.RaiseEnemyPresentation(new EnemyPresentationPayload {
                SourceID = GuardianID,
                AbilityID = "",
                PresentationEventID = $"{GuardianID}.death",
                Phase = EnemyPresentationPhase.Death,
                Position = GlobalPosition
            });
            Destroyed?.Invoke(this);
        }
    }
}
