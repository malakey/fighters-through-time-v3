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

        // --- F24 ownership edge (Package 11 A8) -----------------------------
        public const string OwnerOutlineColorUniform = "owner_outline_color";
        public const string OwnerOutlineThicknessUniform = "owner_outline_thickness";
        public const string OwnerOutlineEnabledUniform = "owner_outline_enabled";

        /// <summary>HUD_CONTRACT's one-pixel reference thickness for the ownership edge.</summary>
        public const float OwnerOutlineThickness = 1f;

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
        // Package 12 W10: one remembered type per status layer (the Venom lerp
        // reads the damage layer's occupant).
        private FTT.Core.StatusType _controlStatus = FTT.Core.StatusType.None;
        private FTT.Core.StatusType _damageStatus = FTT.Core.StatusType.None;
        // V7.6 Suppression (Package 11 A1): the aura-smother channel — a FOURTH
        // independent channel, deliberately not part of the glow stack, so it
        // cannot be cleared by a status ending or a telegraph starting.
        private bool _auraSmothered;

        // --- V7.6 hero resonance aura channel (Package 11 A6b) ---------------
        // design §2's two-colour grammar: the hero carries a warm-gold aura for
        // the ENTIRE game. It is a FIFTH independent channel — not a GlowState
        // layer, not a tint override — precisely because every other channel is
        // transient. Pushed onto the arbitrated stack, the first status would
        // outrank it; expressed as a tint override, the first FlashHit would
        // clear it. The aura is identity, so it sits below the F24 slot
        // indicator (match identity, which outranks everything) and above
        // nothing: it never competes with an effect, it underlies them.
        //
        // It composes with the two tint channels rather than replacing them —
        // base tint or override first, then the warm bias, then A1's Suppression
        // smother — so Suppression correctly drains the gold grey and restores
        // it intact when the status lifts, which is exactly what the design's
        // "what their machines touch drains grey" asks for.
        private bool _heroAura;
        private Color _heroAuraColor = FTT.UI.UIPalette.ResonanceAura;
        private PointLight2D _auraLight;

        // --- F24 ownership channel ------------------------------------------
        // Deliberately NOT part of _stack. It is set once from match slot
        // identity, re-applied on spawn and rollback, and never popped; no effect
        // source can read or write it.
        private bool _ownerOutlineEnabled;
        private Color _ownerOutlineColor = new(0f, 0f, 0f, 0f);
        private int _ownerSlot = -1;

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
                // A6b: the hero aura warms the tint under whatever the transient
                // channels are doing, so a status tint or a hit flash rides on
                // top of the gold instead of erasing it.
                if (_heroAura) tint = WarmTowardAura(tint);
                return _auraSmothered ? Desaturate(tint) : tint;
            }
        }

        /// <summary>
        /// True while this actor carries the persistent V7.6 hero resonance aura.
        /// Story players only: a Fighter proxy's identity is the F24 ownership
        /// edge, and giving both fighters a gold aura would say "both of you are
        /// the hero", which the grammar must never say.
        /// </summary>
        public bool HasHeroAura => _heroAura;

        /// <summary>The aura's authored tint; meaningless while it is off.</summary>
        public Color HeroAuraColor => _heroAuraColor;

        /// <summary>
        /// The aura light node, or null before one is built. Exposed for the
        /// presentation tests; nothing in gameplay reads it.
        /// </summary>
        public PointLight2D AuraLight => _auraLight;

        /// <summary>True while the V7.6 Suppression aura-smother channel is up.</summary>
        public bool IsAuraSmothered => _auraSmothered;
        public bool IsGlowing => _resolvedVisible;
        public GlowState ResolvedState => _resolved;

        /// <summary>
        /// F24: true while this actor carries a player-slot ownership edge. Set
        /// from match identity; no status, armor, spawn-protection or expiry path
        /// can clear it.
        /// </summary>
        public bool HasOwnershipOutline => _ownerOutlineEnabled;

        /// <summary>The ownership edge's colour; transparent when it is not set.</summary>
        public Color OwnershipOutlineColor => _ownerOutlineColor;

        /// <summary>The slot this actor's ownership edge represents, or -1.</summary>
        public int OwnershipSlot => _ownerSlot;

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

        // Package 12 W10: both status events are per slot. An application paints
        // only its own layer and a clear empties only its own layer, so the other
        // slot's glow is never touched and nothing has to be re-announced.
        private void OnStatusEffectApplied(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != OwnerPlayerIndex) return;
            if (payload.Type == FTT.Core.StatusType.None) return;
            SetStatusSlot(payload.ResolveSlot(), payload.Type);
        }

        private void OnStatusEffectCleared(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != OwnerPlayerIndex) return;
            if (payload.ClearsAllSlots) {
                ClearState(GlowLayer.Status);
                return;
            }
            SetStatusSlot(payload.ResolveSlot(), FTT.Core.StatusType.None);
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
            if (state.Layer == GlowLayer.DamageStatus) _venomPhase = 0f;
            ApplyResolvedState();
        }

        public void ClearState(GlowLayer layer) {
            _stack.Clear(layer);
            if (layer == GlowLayer.Status || layer == GlowLayer.ControlStatus) {
                _controlStatus = FTT.Core.StatusType.None;
            }
            if (layer == GlowLayer.Status || layer == GlowLayer.DamageStatus) {
                _damageStatus = FTT.Core.StatusType.None;
            }
            ApplyResolvedState();
        }

        public void ClearAllStates() {
            _stack.ClearAll();
            _controlStatus = FTT.Core.StatusType.None;
            _damageStatus = FTT.Core.StatusType.None;
            ApplyResolvedState();
        }

        /// <summary>
        /// Single-glow form, kept for callers that resolve one presented status
        /// themselves (enemies, bosses). Clears both status layers, then paints
        /// <paramref name="type"/> on its own layer; <c>None</c> clears both.
        /// Two-slot owners use <see cref="SetStatusSlot"/> / <see cref="SetStatusSlots"/>.
        /// </summary>
        public void SetStatus(FTT.Core.StatusType type) {
            if (type == FTT.Core.StatusType.None) {
                ClearState(GlowLayer.Status);
                return;
            }
            _stack.Clear(GlowLayer.Status);
            _controlStatus = FTT.Core.StatusType.None;
            _damageStatus = FTT.Core.StatusType.None;
            SetStatusSlot(StatusRouting.SlotOf(type), type);
        }

        /// <summary>
        /// Package 12 W10: paints (or with <c>None</c> clears) exactly one status
        /// layer. The other slot's layer is untouched.
        /// </summary>
        public void SetStatusSlot(StatusSlot slot, FTT.Core.StatusType type) {
            GlowLayer layer = GlowLayers.ForSlot(slot);
            if (type == FTT.Core.StatusType.None) {
                ClearState(layer);
                return;
            }
            if (slot == StatusSlot.Damage) _damageStatus = type;
            else _controlStatus = type;
            GlowState state = GlowPalette.Status(type);
            // A mis-slotted type still lands on the slot it was published for.
            PushState(new GlowState(layer, state.OutlineColor, state.Thickness, state.Intensity, state.PulseSpeed));
        }

        /// <summary>Both status layers from authoritative two-slot state (the Fighter driver).</summary>
        public void SetStatusSlots(FTT.Core.StatusType control, FTT.Core.StatusType damage) {
            SetStatusSlot(StatusSlot.Control, control);
            SetStatusSlot(StatusSlot.Damage, damage);
        }

        /// <summary>The status painting a layer, or <c>None</c>. Test seam.</summary>
        public FTT.Core.StatusType StatusOn(StatusSlot slot) =>
            slot == StatusSlot.Damage ? _damageStatus : _controlStatus;

        public void SetHyperArmor(bool active) {
            if (active) PushState(GlowPalette.HyperArmor());
            else ClearState(GlowLayer.HyperArmor);
        }

        public void SetSpawnInvulnerability(bool active) {
            if (active) PushState(GlowPalette.SpawnInvulnerability());
            else ClearState(GlowLayer.SpawnInvulnerability);
        }

        /// <summary>
        /// F24 Option A (Package 11 A8): binds this actor's <b>ownership edge</b>.
        ///
        /// <para>Until Package 11 this pushed the slot colour onto the arbitrated
        /// effect stack as its lowest layer, so the first status, armor shell or
        /// spawn aura outranked it and the player simply lost track of which
        /// fighter was theirs. HUD_CONTRACT makes that a bug: the edge "is a
        /// separate persistent layer… damage, statuses, armor, invulnerability,
        /// ability decoys and an effect expiring cannot recolor, pulse, disable or
        /// replace it." So it now writes its own shader channel, composited after
        /// the effect edge, and nothing but a re-bind or
        /// <see cref="ClearSlotIndicator"/> touches it.</para>
        ///
        /// <para>Idempotent, which is what makes it safe to re-assert on spawn and
        /// after a rollback re-sync: those paths restore ownership from match
        /// identity, independently of whatever the effect stack is doing.</para>
        /// </summary>
        public void SetSlotIndicator(int playerIndex) {
            _ownerSlot = playerIndex;
            // Package 12 W5 (G12): the local player-slot palette recolors the
            // edge on this machine only; Default is exactly GlowPalette.SlotColor.
            _ownerOutlineColor = PlayerSlotPalettes.ActiveSlotColor(playerIndex);
            _ownerOutlineEnabled = playerIndex >= 0;
            PushOwnershipToMaterial();
        }

        /// <summary>
        /// Drops the ownership edge. Story entities never have one, and a Fighter
        /// proxy released back to a pool must not keep another slot's colour.
        /// Reachable only from ownership code — never from an effect expiry.
        /// </summary>
        public void ClearSlotIndicator() {
            _ownerSlot = -1;
            _ownerOutlineColor = new Color(0f, 0f, 0f, 0f);
            _ownerOutlineEnabled = false;
            PushOwnershipToMaterial();
        }

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
            // A6b: the smother must reach the hero aura's own light too, or a
            // Suppressed hero keeps radiating gold while their sprite drains.
            ApplyHeroAuraLight();
        }

        /// <summary>
        /// V7.6 persistent hero resonance aura (Package 11 A6b; design §2's
        /// two-colour grammar). Warm gold, carried by the campaign hero for the
        /// entire game — the visible statement that history's resonance lives in
        /// a person, set against every cold Unbound light in the world.
        ///
        /// <para>Its independence is the whole point. A status starting or
        /// ending, a hit flash decaying, hyper-armor, spawn invulnerability, a
        /// telegraph and the F24 ownership edge all leave it exactly where it
        /// was; only this setter and A1's Suppression smother can change what it
        /// looks like, and the smother only <i>drains</i> it — the aura is still
        /// on underneath and comes back at full strength when Suppression
        /// lifts.</para>
        ///
        /// <para>Idempotent, so a respawn, a rewind restore or a pooled rebind
        /// can re-assert it without accumulating state.</para>
        /// </summary>
        public void SetHeroAura(bool active) => SetHeroAura(active, FTT.UI.UIPalette.ResonanceAura);

        /// <summary>
        /// Aura with an explicit tint — the seam A3b's Act III Tremor variant
        /// needs to gutter it at partial strength without owning this channel.
        /// </summary>
        public void SetHeroAura(bool active, Color auraColor) {
            if (_heroAura == active && (!active || _heroAuraColor == auraColor)) return;
            _heroAura = active;
            if (active) _heroAuraColor = auraColor;
            ApplyTint();
            ApplyHeroAuraLight();
        }

        /// <summary>
        /// How far the aura warms the sprite tint. Small on purpose: the aura
        /// must read as a glow the hero carries, not as a gold repaint that
        /// swallows the character's own identity colour.
        /// </summary>
        public const float HeroAuraTintWeight = 0.22f;

        /// <summary>Resting energy of the aura's own light.</summary>
        public const float HeroAuraLightEnergy = 0.45f;

        /// <summary>Aura light scale, relative to the effect light.</summary>
        public const float HeroAuraLightScale = 1.35f;

        private Color WarmTowardAura(Color source) =>
            new(
                Mathf.Lerp(source.R, _heroAuraColor.R, HeroAuraTintWeight),
                Mathf.Lerp(source.G, _heroAuraColor.G, HeroAuraTintWeight),
                Mathf.Lerp(source.B, _heroAuraColor.B, HeroAuraTintWeight),
                source.A);

        /// <summary>
        /// The aura owns its own <see cref="PointLight2D"/> rather than sharing
        /// the effect light, because the effect light is driven by the arbitrated
        /// stack and switches off the moment no effect is resolved — which is
        /// most of the game, and exactly when the aura must still be visible.
        /// </summary>
        private void ApplyHeroAuraLight() {
            if (!_heroAura) {
                if (_auraLight != null) {
                    _auraLight.Enabled = false;
                    _auraLight.Energy = 0f;
                }
                return;
            }
            if (_auraLight == null) {
                var texture = ResourceLoader.Load<Texture2D>(LightTexturePath);
                if (texture == null) return;
                _auraLight = new PointLight2D {
                    Name = "HeroAuraLight",
                    Texture = texture,
                    TextureScale = LightScale * HeroAuraLightScale,
                    BlendMode = Light2D.BlendModeEnum.Add
                };
                AddChild(_auraLight);
            }
            // Suppression drains the aura's light the same way it drains its
            // tint, so a smothered hero is visibly unlit rather than merely
            // recoloured.
            _auraLight.Color = _auraSmothered ? Desaturate(_heroAuraColor) : _heroAuraColor;
            _auraLight.Energy = _auraSmothered
                ? HeroAuraLightEnergy * (1f - AuraSmotherWeight)
                : HeroAuraLightEnergy;
            _auraLight.Enabled = true;
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
            // A material built after SetSlotIndicator ran (pooled proxies rebind
            // their sprite) would otherwise come up with no ownership edge.
            PushOwnershipToMaterial();
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

        /// <summary>
        /// Writes the ownership channel. Separate from
        /// <see cref="PushToMaterial"/> on purpose: the effect path must never be
        /// able to reach these three uniforms, and this path must never be able to
        /// reach the effect's four.
        /// </summary>
        private void PushOwnershipToMaterial() {
            if (_material == null) return;
            _material.SetShaderParameter(OwnerOutlineColorUniform, _ownerOutlineColor);
            _material.SetShaderParameter(OwnerOutlineThicknessUniform, OwnerOutlineThickness);
            _material.SetShaderParameter(OwnerOutlineEnabledUniform, _ownerOutlineEnabled);
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
                _material.SetShaderParameter(PulseSpeedUniform, FTT.Core.ComfortSettings.ResolvePulseSpeed(pulse));
            }
            if (_light == null) return;
            _light.Enabled = _resolvedVisible;
            // The design keeps the light in sync with the same _GlowIntensity value.
            _light.Energy = _resolvedVisible ? intensity * 0.5f : 0f;
            _light.Color = outlineColor.A > 0f ? new Color(outlineColor.R, outlineColor.G, outlineColor.B) : Colors.White;
        }

        private bool _appliedReducedEffects;

        public override void _Process(double delta) {
            float dt = (float)delta;
            if (_flashSecondsRemaining > 0f) {
                _flashSecondsRemaining -= dt;
                if (_flashSecondsRemaining <= 0f) ClearTintOverride();
            }
            // C01a applies live. Polled rather than event-driven for the same
            // reason HudOpacity is: the preset can move from a pause menu opened
            // over this very scene, and a stale pulse would keep throbbing until
            // the next glow change.
            if (_appliedReducedEffects != FTT.Core.ComfortSettings.ReducedTemporalEffects) {
                _appliedReducedEffects = FTT.Core.ComfortSettings.ReducedTemporalEffects;
                ApplyResolvedState();
            }
            // Venom is authored as a gradient: lerp between its two colours. That
            // lerp is decorative pulsing, so C01a freezes it at the authored base
            // colour rather than animating.
            if (_resolvedVisible && _resolved.Layer == GlowLayer.DamageStatus
                && _damageStatus == FTT.Core.StatusType.Venom && _material != null
                && FTT.Core.ComfortSettings.ScreenTintPulseAllowed) {
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
