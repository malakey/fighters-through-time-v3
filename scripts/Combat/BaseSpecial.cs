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

        public bool TryExecute() => TryExecute(armCooldown: true);

        /// <summary>
        /// Package 12 W4. <paramref name="armCooldown"/> false starts the cast
        /// WITHOUT touching the slot's cooldown timer: a Second Glide re-entry
        /// or a banked Sonata Drift charge "ignores and does not restart" the
        /// running recharge. Every ordinary cast arms it.
        /// </summary>
        public bool TryExecute(bool armCooldown) {
            if (IsExecuting) return false;
            if (!Validate()) return false;

            CurrentPhase = AbilityPhase.Startup;
            if (armCooldown) StartCooldown();
            OnStartup();
            EmitCastVfx();
            return true;
        }

        /// <summary>
        /// Package 12 W4: a movement ability may authorize a cast while its
        /// cooldown is still running (Pocahontas's Second Glide, Mozart's
        /// banked Extra Note charge). Returns true and consumes that allowance
        /// atomically; such a cast never re-arms the cooldown. Default: none.
        /// </summary>
        public virtual bool TryConsumeCooldownBypass() => false;

        /// <summary>
        /// Package 12 W4: true while this cast lets projectiles pass through its
        /// owner (Tesla's Lightning Blink translation only). Default: never.
        /// </summary>
        public virtual bool PassesThroughProjectilesNow => false;

        /// <summary>
        /// Design 776: the cooldown starts on the first frame of the cast action,
        /// matching the Fighter sim's press-time cooldown start. Centralized here
        /// so no kit re-authors the timing; the authored
        /// <c>AbilityData.CooldownDuration</c> stays the canonical number, and an
        /// interrupted cast (H-4) still burns its cooldown. Ultimates are
        /// meter-gated and carry no cooldown timer.
        /// </summary>
        /// <summary>
        /// Package 11 A4 (Resonance V7.6). A per-CAST cooldown multiplier an
        /// ability may override — Cleopatra's Vortex Step halves exactly the
        /// cast that consumed its allowance, AFTER every other modifier, and
        /// never rewrites a cooldown that has already started. Neutral 1.0
        /// everywhere else.
        /// </summary>
        public virtual float CooldownScaleForThisCast => 1f;

        /// <summary>
        /// Package 12 W4: re-arms this slot's full cooldown outside a cast —
        /// the recharge of a banked charge (Mozart's Extra Note) starts the next
        /// charge's timer through the one canonical cooldown computation.
        /// </summary>
        protected void RearmCooldown() => StartCooldown();

        private void StartCooldown() {
            if (Owner == null || Data == null) return;
            // Package 11 A4 (Resonance V7.6): CooldownReduction is now
            // ability-SCOPED (Joan's Divine Piercing, Mozart's Fortissimo Wave,
            // Lincoln's Splitting Strike). One lookup here covers all three -
            // the character-wide StoryCooldownMultiplier still ticks the timers
            // down in PlayerController, and an unscoped grid reads neutral 1.0.
            float cooldown = Data.CooldownDuration
                * Owner.StoryScoped("CooldownReduction", Data.AbilityID ?? "")
                * CooldownScaleForThisCast;
            switch (Data.Slot) {
                case FTT.Core.AbilitySlot.Special1:
                    Owner.SpecialOneCooldownTimer = cooldown;
                    // A fresh cooldown cycle re-arms its Resonance Momentum refunds.
                    Owner.OnSpecialCooldownArmed(FTT.Core.AbilitySlot.Special1);
                    break;
                case FTT.Core.AbilitySlot.Special2:
                    Owner.SpecialTwoCooldownTimer = cooldown;
                    Owner.OnSpecialCooldownArmed(FTT.Core.AbilitySlot.Special2);
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
            VfxEmitter.EmitAbilityScene(Data.CastVFXScene, Data, Owner.GlobalPosition,
                Owner.GetParent(), VfxAccentPalette.ForAbility(Data));
        }

        /// <summary>
        /// Spawns the authored impact effect at a confirmed hit. Ability subclasses
        /// and pooled projectiles call this from their hit-confirm paths.
        /// </summary>
        public void EmitImpactVfx(Vector2 position) {
            if (Data?.ImpactVFXScene == null) return;
            Node parent = Owner?.GetParent() ?? GetParent();
            VfxEmitter.EmitAbilityScene(Data.ImpactVFXScene, Data, position, parent,
                VfxAccentPalette.ForAbility(Data, VfxAccentPalette.ImpactAlpha), impact: true);
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

        // ---- Package 12 W4: the kit-payload hit contract ----------------------

        /// <summary>
        /// Stamps a kit-built payload with the authored M08 hit contract of
        /// <paramref name="data"/> — Origin, Delivery, Launches — plus a fresh
        /// per-contact identity, the owning actor and the GAP-14 Nexus fence.
        /// The ONE place a kit script copies these values; it never authors a
        /// second copy of them. <paramref name="deliveryOverride"/> is for the
        /// few kit hits whose delivery is structurally different from the
        /// ability's own (a Vortex tick, a coil chain arc).
        /// </summary>
        public static HitPayload WithAbilityContract(
            HitPayload payload, AbilityData data, PlayerController owner,
            HitDelivery? deliveryOverride = null) {
            if (data != null) {
                payload.Origin = data.Origin;
                payload.Delivery = data.Delivery;
                payload.Launches = data.Launches;
            }
            if (deliveryOverride.HasValue) payload.Delivery = deliveryOverride.Value;
            if (payload.ContactId == 0) payload.ContactId = HitClassification.NextContactId();
            if (owner != null && GodotObject.IsInstanceValid(owner)) {
                payload.SourceActorId = owner.GetInstanceId();
                // GAP-14: every hit of a Nexus-authorized Ultimate is puzzle-only.
                if (owner.IsNexusCastInFlight && payload.Origin == HitOrigin.Ultimate) {
                    payload.PuzzleOnly = true;
                }
            }
            return payload;
        }

        /// <summary>Instance form of <see cref="WithAbilityContract"/> for this cast's data and owner.</summary>
        protected HitPayload Stamp(HitPayload payload, HitDelivery? deliveryOverride = null) =>
            WithAbilityContract(payload, Data, Owner, deliveryOverride);

        /// <summary>
        /// Credits the owner for damage a stamped payload actually dealt, reading
        /// the meter and Rally rules off the payload (D03g/D03h) rather than
        /// literal flags.
        /// </summary>
        public static void CreditDealt(PlayerController owner, in HitPayload payload, float dealt) {
            if (owner == null || dealt <= 0f) return;
            owner.AddInfluenceFromDamageDealt(
                dealt,
                collectsEcho: HitClassification.CollectsEcho(payload.Delivery),
                ultimateOrigin: HitClassification.IsUltimateOrigin(payload.Origin));
        }

        /// <summary>Instance form of <see cref="CreditDealt(PlayerController, in HitPayload, float)"/>.</summary>
        protected void Credit(in HitPayload payload, float dealt) => CreditDealt(Owner, in payload, dealt);

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
            // M08 (Package 12 W3): the authored hit contract replaces the old
            // slot-derived attack class; a data-less hitbox keeps the
            // historical Special / 0.2 s defaults.
            hb.AttackClass = AttackClass.Special;
            hb.HitstunDuration = 0.2f;
            hb.Origin = HitOrigin.Special;
            hb.ApplyAbilityHitContract(Data);
            hb.Damage = Data?.BaseDamage ?? 10f;
            hb.KnockbackForce = Data?.KnockbackForce ?? new Vector2(3, -2);
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
