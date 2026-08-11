using Godot;
using FTT.Characters;

namespace FTT.Combat {

    public enum AbilityPhase {
        Inactive,
        Startup,
        Active,
        Recovery,
        Cleanup
    }

    public abstract partial class BaseSpecial : Node {
        private const string PlaceholderProjectilePoolID = "story_projectile";
        private const string PlaceholderProjectileScenePath = "res://scenes/templates/StoryProjectileTemplate.tscn";
        private const string PlaceholderZonePoolID = "story_zone";
        private const string PlaceholderZoneScenePath = "res://scenes/templates/StoryZoneTemplate.tscn";
        [Export] public AbilityData Data;

        public AbilityPhase CurrentPhase { get; protected set; } = AbilityPhase.Inactive;
        public bool IsExecuting => CurrentPhase != AbilityPhase.Inactive;

        protected new PlayerController Owner;
        private int _phaseFramesRemaining;
        protected float PhaseTimer {
            get => _phaseFramesRemaining / 60.0f;
            set => _phaseFramesRemaining = Mathf.Max(0, Mathf.RoundToInt(value * 60.0f));
        }

        public override void _Ready() {
            Owner = GetParent<PlayerController>();
        }

        /// <summary>
        /// Sets the phase countdown from the authored frame timings on the
        /// AbilityData resource for the current phase. Canonical kit conversions
        /// should call this in OnStartup/OnActive/OnRecovery instead of hardcoding
        /// per-ability durations; every phase runs at least one frame.
        /// </summary>
        protected void UseAuthoredPhaseFrames() {
            if (Data == null) return;
            _phaseFramesRemaining = CurrentPhase switch {
                AbilityPhase.Startup => Mathf.Max(1, Data.StartupFrames),
                AbilityPhase.Active => Mathf.Max(1, Data.ActiveFrames),
                AbilityPhase.Recovery => Mathf.Max(1, Data.RecoveryFrames),
                _ => _phaseFramesRemaining
            };
        }

        public bool TryExecute() {
            if (IsExecuting) return false;
            if (!Validate()) return false;

            CurrentPhase = AbilityPhase.Startup;
            StartCooldown();
            OnStartup();
            EmitCastVfx();
            return true;
        }

        /// <summary>
        /// Design 776: the cooldown starts on the first frame of the cast action,
        /// matching the Fighter sim's press-time cooldown start. Centralized here
        /// so no kit re-authors the timing; the authored
        /// <c>AbilityData.CooldownDuration</c> stays the canonical number, and an
        /// interrupted cast (H-4) still burns its cooldown. Ultimates are
        /// meter-gated and carry no cooldown timer.
        /// </summary>
        private void StartCooldown() {
            if (Owner == null || Data == null) return;
            float cooldown = Data.CooldownDuration;
            switch (Data.Slot) {
                case FTT.Core.AbilitySlot.Special1:
                    Owner.SpecialOneCooldownTimer = cooldown;
                    break;
                case FTT.Core.AbilitySlot.Special2:
                    Owner.SpecialTwoCooldownTimer = cooldown;
                    break;
                case FTT.Core.AbilitySlot.MovementAbility:
                    Owner.MovementAbilityCooldownTimer = cooldown;
                    break;
                default:
                    return;
            }
            FTT.Core.EventBus.Instance?.RaiseCooldownStarted(new FTT.Core.CooldownPayload {
                PlayerIndex = Owner.PlayerIndex,
                Slot = Data.Slot,
                Duration = cooldown
            });
        }

        /// <summary>
        /// H-4: stops an executing cast cold — no further phase advancement, no
        /// remaining multi-hit ticks, no steering — WITHOUT the state-transition
        /// side effect of <see cref="OnCleanup"/>, so an interrupting Stunned/Dead
        /// state is never stomped. Invoked by the owner when a stun lands or the
        /// owner dies; hyper-armor gating happens at the hit-resolution site
        /// (<c>PlayerController.OnHurtboxHit</c>), which never applies the stun
        /// while an armor window is active. Deployed world objects (zones,
        /// constructs, fired projectiles) deliberately survive — interruption
        /// cancels the cast, not what it already put into the world.
        /// </summary>
        public void Interrupt() {
            if (!IsExecuting) return;
            CurrentPhase = AbilityPhase.Inactive;
            _phaseFramesRemaining = 0;
            OnInterrupted();
        }

        /// <summary>
        /// Subclass hook for <see cref="Interrupt"/>: release any steering flags,
        /// live hitboxes, or owner physics overrides the cast was holding. Never
        /// transition the owner's state from here.
        /// </summary>
        protected virtual void OnInterrupted() { }

        /// <summary>
        /// Package 8 A3: spawns the authored cast effect from the pooled VFX
        /// service. Null-safe — <c>CastVFXScene</c> is unassigned until B6 authors
        /// the resources, and nothing gameplay-side depends on the result.
        /// </summary>
        private void EmitCastVfx() {
            if (Data?.CastVFXScene == null || Owner == null) return;
            // Package 8 B6: the 36 abilities share six placeholder scenes, so the
            // per-character accent is what distinguishes them. A3 provided the tint
            // parameter for exactly this and left it unused pending the content.
            VfxEmitter.EmitScene(Data.CastVFXScene, Owner.GlobalPosition, Owner.GetParent(),
                VfxAccentPalette.ForAbility(Data));
        }

        /// <summary>
        /// Spawns the authored impact effect at a confirmed hit. Ability subclasses
        /// and pooled projectiles call this from their hit-confirm paths.
        /// </summary>
        public void EmitImpactVfx(Vector2 position) {
            if (Data?.ImpactVFXScene == null) return;
            Node parent = Owner?.GetParent() ?? GetParent();
            VfxEmitter.EmitScene(Data.ImpactVFXScene, position, parent,
                VfxAccentPalette.ForAbility(Data, VfxAccentPalette.ImpactAlpha));
        }

        /// <summary>
        /// Emits an optional character-specific presentation scene without moving
        /// the AbilityData resource outside the six-family shared VFX taxonomy.
        /// Gameplay remains independent of this best-effort visual layer.
        /// </summary>
        protected void EmitCharacterVfx(string scenePath, Vector2 position) {
            if (Owner == null || string.IsNullOrWhiteSpace(scenePath)) return;
            PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene == null) return;
            VfxEmitter.EmitScene(scene, position, Owner.GetParent(), Colors.White);
        }

        private void OnAbilityHitConfirmed(HitPayload payload, float damageApplied) {
            if (damageApplied <= 0f) return;
            EmitImpactVfx(payload.HitOrigin);
            FTT.Core.EventBus.Instance?.RaiseHitConfirm(new FTT.Core.HitConfirmPayload {
                PlayerIndex = Owner?.PlayerIndex ?? -1,
                AttackID = payload.AttackID ?? "",
                DamageApplied = damageApplied,
                IsHeavy = true,
                Position = payload.HitOrigin
            });
        }

        protected virtual bool Validate() {
            return Owner != null && Data != null;
        }

        protected abstract void OnStartup();
        protected abstract void OnActive();
        protected abstract void OnRecovery();

        protected virtual void OnCleanup() {
            CurrentPhase = AbilityPhase.Inactive;
            // H-4: only hand the FSM back when the owner is still in the state
            // this cast put it in. An expiring phase timer must never stomp an
            // interposed Stunned/Dazed/Dead (or any other) state.
            if (Owner == null || !OwnerIsInAbilityDrivenState()) return;
            if (Owner.IsOnFloor()) {
                Owner.TransitionTo(CharacterState.Idle);
            } else {
                Owner.TransitionTo(CharacterState.Airborne);
            }
        }

        private bool OwnerIsInAbilityDrivenState() => Owner.CurrentState is
            CharacterState.UsingSpecial or
            CharacterState.UsingUltimate or
            CharacterState.UsingMovementAbility;

        public void AdvanceToActive() {
            CurrentPhase = AbilityPhase.Active;
            OnActive();
        }

        public void AdvanceToRecovery() {
            CurrentPhase = AbilityPhase.Recovery;
            OnRecovery();
        }

        public void AdvanceToCleanup() {
            CurrentPhase = AbilityPhase.Cleanup;
            OnCleanup();
        }

        public override void _PhysicsProcess(double delta) {
            if (!IsExecuting) return;
            _phaseFramesRemaining--;
            if (_phaseFramesRemaining <= 0) {
                switch (CurrentPhase) {
                    case AbilityPhase.Startup:
                        AdvanceToActive();
                        break;
                    case AbilityPhase.Active:
                        AdvanceToRecovery();
                        break;
                    case AbilityPhase.Recovery:
                        AdvanceToCleanup();
                        break;
                }
            }
        }

        protected PlaceholderProjectile SpawnPlaceholderProjectile(Vector2 position, float speed,
            bool movingRight, Color color, Vector2 size = default, float lifetime = 3f,
            float damageOverride = -1f) {
            var proj = SpawnPooledPlaceholder<PlaceholderProjectile>(
                PlaceholderProjectilePoolID,
                PlaceholderProjectileScenePath,
                position,
                50);
            if (proj == null) return null;
            float damage = damageOverride >= 0f ? damageOverride : Data?.BaseDamage ?? 10f;
            // Story-only ProjectileSpeed minors accelerate every placeholder
            // projectile the owner fires; neutral 1f outside Story Mode.
            proj.Setup(damage, Data?.KnockbackForce ?? new Vector2(3, -2),
                speed * (Owner?.StoryProjectileSpeedMultiplier ?? 1f),
                movingRight, Owner?.PlayerIndex ?? 0, color, size, lifetime, Owner, Data);
            return proj;
        }

        protected PlaceholderZone SpawnPlaceholderZone(Vector2 position, float damage,
            float lifetime, float tickInterval, Color color, float radius = 60f,
            FTT.Core.StatusType appliedStatus = FTT.Core.StatusType.None,
            float statusDuration = 0f, float statusIntensity = 1f,
            float ownerSpeedMultiplier = 1f) {
            var zone = SpawnPooledPlaceholder<PlaceholderZone>(
                PlaceholderZonePoolID,
                PlaceholderZoneScenePath,
                position,
                24);
            if (zone == null) return null;
            zone.Setup(damage, lifetime, tickInterval, Owner?.PlayerIndex ?? 0, color, radius,
                appliedStatus, statusDuration, statusIntensity, Owner, ownerSpeedMultiplier, Data);
            return zone;
        }

        private T SpawnPooledPlaceholder<T>(string poolID, string scenePath, Vector2 position, int capacity)
            where T : FTT.Core.PooledNode {
            FTT.Core.PoolManager pools = FTT.Core.PoolManager.Instance;
            Node parent = Owner?.GetParent() ?? GetTree().CurrentScene;
            if (pools == null || parent == null) return null;
            if (!pools.IsRegistered(poolID)) {
                PackedScene scene = GD.Load<PackedScene>(scenePath);
                if (scene == null) {
                    GD.PushError($"Missing pooled placeholder scene: {scenePath}");
                    return null;
                }
                pools.RegisterPool(poolID, scene, 1, capacity, FTT.Core.PoolOverflowPolicy.Grow);
            }
            return pools.Spawn(poolID, position, parent) as T;
        }

        protected Hitbox GetOrCreateChildHitbox(string name) {
            var existing = GetNodeOrNull<Hitbox>(name);
            if (existing != null) return existing;
            var hb = new Hitbox();
            hb.Name = name;
            hb.AttackID = Data?.AbilityID ?? "";
            hb.HitboxID = name;
            hb.AttackClass = Data?.Slot == FTT.Core.AbilitySlot.Ultimate
                ? AttackClass.Ultimate
                : AttackClass.Special;
            hb.Damage = Data?.BaseDamage ?? 10f;
            hb.KnockbackForce = Data?.KnockbackForce ?? new Vector2(3, -2);
            hb.HitstunDuration = Data?.HitstunDuration ?? 0.2f;
            hb.AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None;
            hb.StatusDuration = Data?.StatusDuration ?? 0f;
            hb.StatusIntensity = Data?.StatusIntensity ?? 1f;
            hb.ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f;
            hb.ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.1f;
            hb.OwnerPlayerIndex = Owner?.PlayerIndex ?? 0;
            hb.SourcePlayer = Owner;
            hb.CollisionLayer = FTT.Core.CollisionLayers.HitboxLayerForFighterSlot(Owner?.PlayerIndex ?? 0);
            hb.CollisionMask = FTT.Core.CollisionLayers.HitboxMaskForFighterSlot(Owner?.PlayerIndex ?? 0);
            hb.Monitorable = true;
            var shape = new CollisionShape2D();
            var rect = new RectangleShape2D();
            rect.Size = Data?.HitboxSize ?? new Vector2(50, 50);
            shape.Shape = rect;
            hb.AddChild(shape);
            hb.HitConfirmed += OnAbilityHitConfirmed;
            AddChild(hb);
            return hb;
        }
    }
}
