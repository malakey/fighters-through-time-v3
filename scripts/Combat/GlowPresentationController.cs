using Godot;

namespace FTT.Combat {

    /// <summary>
    /// The single owner of an entity's sprite tint and outline/glow material.
    /// Attached by <c>CharacterFactory</c> for players and by the enemy/boss
    /// controllers for the roster (Package 8 plan section 2.3).
    ///
    /// Three independent channels, deliberately separated so they cannot clobber
    /// one another:
    /// <list type="bullet">
    /// <item>base tint — the entity's resting <c>Modulate</c> (enemy
    /// <c>PlaceholderTint</c>, player white).</item>
    /// <item>tint override — a transient full-sprite recolour that replaces the base
    /// tint while set (ability telegraph, death dim, hit flash).</item>
    /// <item>glow stack — the priority-arbitrated outline shader state
    /// (<see cref="GlowStateStack"/>) plus a matching child <c>PointLight2D</c>.</item>
    /// </list>
    /// A telegraph therefore survives a status ending, and a status ending cannot
    /// erase a telegraph tint: they are different channels.
    ///
    /// gl_compatibility has no canvas <c>instance uniform</c>s, so the arbiter
    /// duplicates the shared base <c>ShaderMaterial</c> once per entity at attach
    /// time rather than sharing one material across entities.
    /// </summary>
    public partial class GlowPresentationController : Node2D {
        public const string ShaderPath = "res://assets/shaders/outline_glow.gdshader";
        public const string LightTexturePath = "res://assets/placeholders/glow_light_gradient.tres";
        public const string NodeName = "GlowPresentation";

        public const string OutlineColorUniform = "outline_color";
        public const string OutlineThicknessUniform = "outline_thickness";
        public const string GlowIntensityUniform = "glow_intensity";
        public const string PulseSpeedUniform = "pulse_speed";

        /// <summary>Player slot this arbiter listens for on the EventBus; -1 disables filtering.</summary>
        [Export] public int OwnerPlayerIndex = -1;
        /// <summary>Story players subscribe to the bus; Fighter presentation bodies are driven by the driver.</summary>
        [Export] public bool SubscribeToStoryEvents = true;
        /// <summary>Light radius scale relative to the sprite; 0 keeps the light off.</summary>
        [Export] public float LightScale = 1.6f;

        private CanvasItem _target;
        private readonly GlowStateStack _stack = new();
        private ShaderMaterial _material;
        private PointLight2D _light;
        private Color _baseTint = Colors.White;
        private Color _tintOverride = Colors.White;
        private bool _tintOverrideActive;
        private float _flashSecondsRemaining;
        private float _venomPhase;
        private bool _eventsBound;
        private bool _resolvedVisible;
        private GlowState _resolved;
        private FTT.Core.StatusType _activeStatus = FTT.Core.StatusType.None;
        // V7.6 Suppression (Package 11 A1): the aura-smother channel — a FOURTH
        // independent channel, deliberately not part of the glow stack, so it
        // cannot be cleared by a status ending or a telegraph starting.
        private bool _auraSmothered;

        /// <summary>The sprite (or other CanvasItem) this arbiter owns.</summary>
        public CanvasItem Target => _target;
        public bool HasMaterial => _material != null;
        /// <summary>The per-entity duplicate of the shared outline material.</summary>
        public ShaderMaterial GlowMaterial => _material;
        public PointLight2D Light => _light;
        public Color BaseTint => _baseTint;
        public bool HasTintOverride => _tintOverrideActive;
        public Color EffectiveTint {
            get {
                Color tint = _tintOverrideActive ? _tintOverride : _baseTint;
                return _auraSmothered ? Desaturate(tint) : tint;
            }
        }

        /// <summary>True while the V7.6 Suppression aura-smother channel is up.</summary>
        public bool IsAuraSmothered => _auraSmothered;
        public bool IsGlowing => _resolvedVisible;
        public GlowState ResolvedState => _resolved;

        public bool IsLayerActive(GlowLayer layer) => _stack.IsActive(layer);

        /// <summary>
        /// Binds the arbiter to a sprite and installs a per-entity duplicate of the
        /// shared outline material. Safe to call again with the same target.
        /// </summary>
        public void Attach(CanvasItem target) {
            _target = target;
            if (_target == null) return;
            EnsureMaterial();
            EnsureLight();
            ApplyTint();
            ApplyResolvedState();
        }

        // Pooled owners re-enter the tree repeatedly but _Ready runs once, so the
        // bus subscription lives on the enter/exit pair.
        public override void _EnterTree() => BindEvents();

        public override void _Ready() {
            if (_target == null) {
                // Default binding: the owner's AnimatedSprite2D, either directly or
                // under a "Presentation" child, matching both scene conventions.
                Node owner = GetParent();
                CanvasItem found = owner?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")
                    ?? (CanvasItem)owner?.GetNodeOrNull<AnimatedSprite2D>("Presentation/AnimatedSprite2D");
                if (found != null) Attach(found);
            }
            BindEvents();
        }

        public override void _ExitTree() => UnbindEvents();

        private void BindEvents() {
            if (_eventsBound || !SubscribeToStoryEvents) return;
            FTT.Core.EventBus bus = FTT.Core.EventBus.Instance;
            if (bus == null) return;
            _eventsBound = true;
            bus.OnStatusEffectApplied += OnStatusEffectApplied;
            bus.OnStatusEffectCleared += OnStatusEffectCleared;
            bus.OnHyperArmorChanged += OnHyperArmorChanged;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            FTT.Core.EventBus bus = FTT.Core.EventBus.Instance;
            if (bus == null) return;
            bus.OnStatusEffectApplied -= OnStatusEffectApplied;
            bus.OnStatusEffectCleared -= OnStatusEffectCleared;
            bus.OnHyperArmorChanged -= OnHyperArmorChanged;
        }

        private void OnStatusEffectApplied(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != OwnerPlayerIndex) return;
            SetStatus(payload.Type);
        }

        private void OnStatusEffectCleared(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != OwnerPlayerIndex) return;
            ClearState(GlowLayer.Status);
        }

        private void OnHyperArmorChanged(FTT.Core.HyperArmorPayload payload) {
            if (payload.PlayerIndex != OwnerPlayerIndex) return;
            SetHyperArmor(payload.IsActive);
        }

        // === Public API (B1/B2/B6 consume this surface) ===

        /// <summary>Sets the resting sprite tint. Ignored while a tint override is set.</summary>
        public void SetBaseTint(Color tint) {
            _baseTint = tint;
            ApplyTint();
        }

        /// <summary>Replaces the sprite tint until <see cref="ClearTintOverride"/>.</summary>
        public void SetTintOverride(Color tint) {
            _tintOverride = tint;
            _tintOverrideActive = true;
            ApplyTint();
        }

        public void ClearTintOverride() {
            if (!_tintOverrideActive) return;
            _tintOverrideActive = false;
            _flashSecondsRemaining = 0f;
            ApplyTint();
        }

        /// <summary>
        /// Brief full-sprite flash on taking damage. Implemented on the tint-override
        /// channel with a timer, so it cannot leak: it always restores the base tint.
        /// </summary>
        public void FlashHit(Color flashColor, float seconds = 0.08f) {
            if (seconds <= 0f) return;
            _tintOverride = flashColor;
            _tintOverrideActive = true;
            _flashSecondsRemaining = seconds;
            ApplyTint();
        }

        public void FlashHit() => FlashHit(new Color(1f, 0.45f, 0.45f), 0.08f);

        public void PushState(GlowState state) {
            _stack.Push(state);
            if (state.Layer == GlowLayer.Status) _venomPhase = 0f;
            ApplyResolvedState();
        }

        public void ClearState(GlowLayer layer) {
            _stack.Clear(layer);
            if (layer == GlowLayer.Status) _activeStatus = FTT.Core.StatusType.None;
            ApplyResolvedState();
        }

        public void ClearAllStates() {
            _stack.ClearAll();
            _activeStatus = FTT.Core.StatusType.None;
            ApplyResolvedState();
        }

        /// <summary>Pushes or clears the status layer from a status type.</summary>
        public void SetStatus(FTT.Core.StatusType type) {
            if (type == FTT.Core.StatusType.None) {
                ClearState(GlowLayer.Status);
                return;
            }
            _activeStatus = type;
            PushState(GlowPalette.Status(type));
        }

        public void SetHyperArmor(bool active) {
            if (active) PushState(GlowPalette.HyperArmor());
            else ClearState(GlowLayer.HyperArmor);
        }

        public void SetSpawnInvulnerability(bool active) {
            if (active) PushState(GlowPalette.SpawnInvulnerability());
            else ClearState(GlowLayer.SpawnInvulnerability);
        }

        public void SetSlotIndicator(int playerIndex) => PushState(GlowPalette.SlotIndicator(playerIndex));

        /// <summary>
        /// V7.6 Suppression aura-smother (Package 11 A1). While set, the
        /// persistent gold resonance aura desaturates to cold grey and the
        /// outline shader drops to base priority — thickness, glow intensity and
        /// pulse are pinned to their resting values without touching the glow
        /// stack, so the arbiter's resolved layer (telegraph, hyper-armor,
        /// slot ownership) is preserved and restored intact when Suppression
        /// ends. A1 owns this channel and the state read; A8 owns the HUD
        /// cross-out, driven by OnAbilitySlotLockChanged.
        /// </summary>
        public void SetAuraSmothered(bool smothered) {
            if (_auraSmothered == smothered) return;
            _auraSmothered = smothered;
            ApplyTint();
            ApplyResolvedState();
        }

        /// <summary>Luminance-preserving desaturation toward the cold Suppression grey.</summary>
        private static Color Desaturate(Color source) {
            float luminance = 0.299f * source.R + 0.587f * source.G + 0.114f * source.B;
            Color cold = GlowPalette.SuppressionColor;
            return new Color(
                Mathf.Lerp(source.R, luminance * cold.R * 2f, AuraSmotherWeight),
                Mathf.Lerp(source.G, luminance * cold.G * 2f, AuraSmotherWeight),
                Mathf.Lerp(source.B, luminance * cold.B * 2f, AuraSmotherWeight),
                source.A);
        }

        /// <summary>How far the smother pulls the aura toward cold grey.</summary>
        public const float AuraSmotherWeight = 0.85f;

        /// <summary>The base outline priority a smothered outline drops to.</summary>
        public const float SmotheredOutlineThickness = 1f;
        public const float SmotheredGlowIntensity = 1f;

        // === Internals ===

        private void EnsureMaterial() {
            if (_material != null) {
                if (_target.Material != _material) _target.Material = _material;
                return;
            }
            var shader = ResourceLoader.Load<Shader>(ShaderPath);
            if (shader == null) return;
            // One duplicate per entity: gl_compatibility canvas shaders have no
            // instance uniforms, so a shared material could not vary per entity.
            _material = new ShaderMaterial { Shader = shader };
            _material.SetShaderParameter(OutlineColorUniform, new Color(0f, 0f, 0f, 0f));
            _material.SetShaderParameter(OutlineThicknessUniform, 0f);
            _material.SetShaderParameter(GlowIntensityUniform, 1f);
            _material.SetShaderParameter(PulseSpeedUniform, 0f);
            _target.Material = _material;
        }

        private void EnsureLight() {
            if (_light != null || LightScale <= 0f) return;
            var texture = ResourceLoader.Load<Texture2D>(LightTexturePath);
            if (texture == null) return;
            _light = new PointLight2D {
                Name = "GlowLight",
                Texture = texture,
                TextureScale = LightScale,
                Energy = 0f,
                Enabled = false,
                BlendMode = Light2D.BlendModeEnum.Add
            };
            AddChild(_light);
        }

        private void ApplyTint() {
            if (_target == null) return;
            _target.Modulate = EffectiveTint;
        }

        private void ApplyResolvedState() {
            _resolvedVisible = _stack.TryResolve(out GlowState resolved) && resolved.IsVisible;
            _resolved = _resolvedVisible ? resolved : default;
            PushToMaterial(_resolvedVisible ? _resolved.OutlineColor : new Color(0f, 0f, 0f, 0f));
        }

        private void PushToMaterial(Color outlineColor) {
            // V7.6 Suppression: the outline drops to base priority — resting
            // thickness and intensity, no pulse — without disturbing the stack.
            float thickness = _resolvedVisible
                ? (_auraSmothered ? Mathf.Min(_resolved.Thickness, SmotheredOutlineThickness) : _resolved.Thickness)
                : 0f;
            float intensity = _resolvedVisible
                ? (_auraSmothered ? SmotheredGlowIntensity : _resolved.Intensity)
                : 1f;
            float pulse = _resolvedVisible && !_auraSmothered ? _resolved.PulseSpeed : 0f;
            if (_material != null) {
                _material.SetShaderParameter(OutlineColorUniform, outlineColor);
                _material.SetShaderParameter(OutlineThicknessUniform, thickness);
                _material.SetShaderParameter(GlowIntensityUniform, intensity);
                _material.SetShaderParameter(PulseSpeedUniform, pulse);
            }
            if (_light == null) return;
            _light.Enabled = _resolvedVisible;
            // The design keeps the light in sync with the same _GlowIntensity value.
            _light.Energy = _resolvedVisible ? intensity * 0.5f : 0f;
            _light.Color = outlineColor.A > 0f ? new Color(outlineColor.R, outlineColor.G, outlineColor.B) : Colors.White;
        }

        public override void _Process(double delta) {
            float dt = (float)delta;
            if (_flashSecondsRemaining > 0f) {
                _flashSecondsRemaining -= dt;
                if (_flashSecondsRemaining <= 0f) ClearTintOverride();
            }
            // Venom is authored as a gradient: lerp between its two colours.
            if (_resolvedVisible && _resolved.Layer == GlowLayer.Status
                && _activeStatus == FTT.Core.StatusType.Venom && _material != null) {
                _venomPhase += dt * 0.6f;
                float weight = 0.5f + 0.5f * Mathf.Sin(_venomPhase * Mathf.Tau);
                Color blended = GlowPalette.VenomColor.Lerp(GlowPalette.VenomSecondaryColor, weight);
                blended.A = _resolved.OutlineColor.A;
                _material.SetShaderParameter(OutlineColorUniform, blended);
                if (_light != null) _light.Color = new Color(blended.R, blended.G, blended.B);
            }
        }

        /// <summary>
        /// Convenience attach used by every call site: finds or creates the arbiter
        /// under <paramref name="owner"/> and binds it to <paramref name="sprite"/>.
        /// </summary>
        public static GlowPresentationController AttachTo(Node owner, CanvasItem sprite, int ownerPlayerIndex,
            bool subscribeToStoryEvents) {
            if (owner == null) return null;
            var existing = owner.GetNodeOrNull<GlowPresentationController>(NodeName);
            if (existing == null) {
                existing = new GlowPresentationController {
                    Name = NodeName,
                    OwnerPlayerIndex = ownerPlayerIndex,
                    SubscribeToStoryEvents = subscribeToStoryEvents,
                    // Explicit so the arbiter keeps processing under Fighter-mode
                    // proxies, whose body subtree the driver sets to Disabled —
                    // otherwise FlashHit's tint decay and the Venom lerp freeze
                    // and the first hit's flash sticks forever. Under a Story
                    // body (Pausable via Inherit) this changes nothing.
                    ProcessMode = ProcessModeEnum.Pausable
                };
                owner.AddChild(existing);
            } else {
                existing.OwnerPlayerIndex = ownerPlayerIndex;
                existing.SubscribeToStoryEvents = subscribeToStoryEvents;
            }
            existing.Attach(sprite);
            return existing;
        }
    }
}
