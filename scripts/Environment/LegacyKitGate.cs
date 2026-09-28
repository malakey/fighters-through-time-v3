using System;
using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// How a <see cref="LegacyKitGate"/> recognises its authored ability.
    ///
    /// <para>V01c (campaign validation) is explicit that a Level 4A gate accepts
    /// <i>its</i> authored ability or event and nothing else — a combat construct,
    /// a decoy, an enemy corpse or an incidental physics contact can never stand in
    /// for it. These modes are the five shapes the nine roster kits actually take;
    /// a variant that needs a sixth records it in the plan's §9 rather than
    /// loosening an existing one. Append-only: never renumber a member.</para>
    /// </summary>
    public enum LegacyGateMode {
        /// <summary>A projectile or melee special: the gate's own hurtbox matches <c>HitPayload.AttackID</c>.</summary>
        Strike = 0,
        /// <summary>A placed zone special: a live <c>story_zone</c> with the authored ability ID overlaps the gate.</summary>
        Zone = 1,
        /// <summary>The movement ability: the player reaches the gate during (or just after) a movement-ability use.</summary>
        Traversal = 2,
        /// <summary>The Ultimate set-piece: resolved only by <see cref="NexusResonanceSource"/> (F04).</summary>
        Nexus = 3,
        /// <summary>
        /// Cast-watch (Package 12 W8): the hero is mid-execution of the authored
        /// ability — read from the player's own <see cref="FTT.Combat.BaseSpecial"/>
        /// children, by ability ID — within <see cref="LegacyKitGate.CastWatchRadius"/>
        /// of the gate. For specials that raise nothing a surface or a zone poll can
        /// observe (Shakespeare's The Tempest) and for deployed constructs whose cast
        /// is the act the mechanism answers (Tesla Coil, Serpent Nest). Replaces the
        /// retired Package 11 <c>LegacyCastGateWatcher</c> stand-in.
        /// </summary>
        CastWatch = 4
    }

    /// <summary>
    /// Level 4A kit gate (V7.6 / Package 11 A12) — one authored mechanism that only
    /// the hero's own ability can operate. The Legacy Level is the campaign's single
    /// exception to "every required puzzle must work with every eligible kit": it is
    /// authored per character and must require the whole kit once each — one
    /// traversal gate for the Movement Ability, one puzzle per Special, and a
    /// set-piece the Ultimate resolves through the Nexus Resonance Source.
    ///
    /// <para>A gate is a latch, not a trigger: once resolved it stays resolved for
    /// the attempt (<see cref="ForceResolve"/> replays that fact on a checkpoint
    /// reconstruction without re-awarding anything). Resolution is idempotent, so a
    /// second qualifying hit on an open gate is silently ignored rather than firing
    /// the objective chain twice.</para>
    ///
    /// <para>Deliberately not an <see cref="IInteractable"/>: Interact is the Font's
    /// and the Nexus source's verb, and a gate that could be opened with Interact
    /// would stop requiring the ability it exists to require.</para>
    /// </summary>
    public partial class LegacyKitGate : Node2D {

        /// <summary>Frames after a movement-ability use during which a Traversal gate still counts the arrival.</summary>
        public const int TraversalGraceFrames = 30;

        /// <summary>
        /// The generous strike surface for a gate whose ability arrives as an
        /// enemy-hurtbox sweep (Divine Piercing's thrusts, Clockwork Turret bolts):
        /// the retired <c>LegacyResonantEffigy</c>'s receiving surface, kept at its
        /// size so those gates are exactly as reachable as they were.
        /// </summary>
        public static readonly Vector2 SweepStrikeSurfaceSize = new(120f, 190f);

        [Export] public string GateID = "";

        /// <summary>The authored ability ID (<c>einstein_relativity_rift</c>) — never a slot or a display name.</summary>
        [Export] public string RequiredAbilityID = "";

        [Export] public LegacyGateMode Mode = LegacyGateMode.Strike;

        /// <summary>Zone mode: how close a matching zone's centre must come to the gate.</summary>
        [Export] public float ResolveRadius = 150f;

        /// <summary>Traversal mode: the landing box the movement ability has to reach.</summary>
        [Export] public Vector2 TraversalBoxSize = new(160f, 220f);

        /// <summary>Strike mode: the strikeable surface size.</summary>
        [Export] public Vector2 StrikeSurfaceSize = new(80f, 140f);

        /// <summary>
        /// The strike surface also sits on the <c>EnemyHurtbox</c> layer, so a
        /// delivery that only queries that layer — the shape-query specials (Divine
        /// Piercing, E=mc², …) and the deployed constructs (Clockwork Turret, Tesla
        /// Coil, Serpent Nest) — can strike it. On by default: every shipped Strike
        /// gate has relied on it since the Package 11 integration.
        ///
        /// <para>It cannot be mistaken for an enemy. The surface is unowned
        /// (<c>OwnerPlayerIndex = -1</c>), is neither an <c>EnemyController</c> nor a
        /// <c>BossController</c> descendant (the kits' owner walks find nothing), and
        /// <see cref="OnStruck"/> returns zero damage, so every meter, Rally and
        /// reward path — all gated on damage dealt &gt; 0 — credits nothing. Enemy
        /// attacks carry a negative attacker index and are ignored. The layer is
        /// dropped when the gate latches, so a solved gate stops drawing construct
        /// fire (the retired <c>LegacyResonantEffigy</c>'s "goes inert" rule).</para>
        ///
        /// <para>The sealed <c>BuildLevel</c> builds the gate, so a 4A variant sets
        /// this through <see cref="ConfigureStrikeSurface"/>.</para>
        /// </summary>
        [Export] public bool StrikeSurfaceOnEnemyHurtbox = true;

        /// <summary>CastWatch mode: how close the caster must be to the gate (the retired watcher's radius).</summary>
        [Export] public float CastWatchRadius = 220f;

        /// <summary>
        /// Zone-mode option (Package 12 W8): the zone poll also accepts a live
        /// persistent construct the hero deployed <b>with the authored ability</b> —
        /// identified by that ability's own <c>PersistentObjectScene</c>, read from
        /// the player's <see cref="FTT.Combat.BaseSpecial"/> — within
        /// <see cref="ResolveRadius"/> of the gate. For a placed special that deploys
        /// a construct instead of a <c>story_zone</c> (Pocahontas's Vine Snare).
        /// Replaces the retired <c>VineSnareGateResolver</c> stand-in. Read every
        /// poll, so a variant may set it after the gate is in the tree.
        /// </summary>
        [Export] public bool ZoneAcceptsOwnedConstruct;

        public bool IsResolved { get; private set; }

        /// <summary>Raised once, on the transition to resolved.</summary>
        public event Action<LegacyKitGate> Resolved;

        private int _traversalGraceFrames;
        private Area2D _traversalArea;
        private EnvironmentHurtboxAdapter _strikeSurface;
        private CollisionShape2D _strikeShape;
        private ColorRect _visual;
        private Label _prompt;

        public override void _Ready() {
            BuildPresentation();
            switch (Mode) {
                case LegacyGateMode.Strike: BuildStrikeSurface(); break;
                case LegacyGateMode.Traversal: BuildTraversalArea(); break;
            }
            UpdatePresentation();
        }

        /// <summary>
        /// Zone and Traversal gates poll: a zone applies its effects to bodies, not
        /// to hurtbox areas, and a teleporting movement ability may never overlap the
        /// gate at all (it lands past it). Strike and Nexus gates do no work here.
        /// </summary>
        public override void _PhysicsProcess(double delta) {
            if (IsResolved) return;
            switch (Mode) {
                case LegacyGateMode.Zone: PollZones(); break;
                case LegacyGateMode.Traversal: PollTraversal(); break;
                case LegacyGateMode.CastWatch: PollCastWatch(); break;
            }
        }

        /// <summary>
        /// Resolves the gate when <paramref name="abilityID"/> is the authored one.
        /// The single entry point: every mode funnels through it, so the accepted
        /// ability is stated once.
        /// </summary>
        public bool TryResolve(string abilityID) {
            if (IsResolved) return false;
            if (string.IsNullOrWhiteSpace(RequiredAbilityID)) return false;
            if (!string.Equals(abilityID, RequiredAbilityID, StringComparison.Ordinal)) return false;
            ForceResolve();
            return true;
        }

        /// <summary>
        /// Opens the gate without an ability — the checkpoint-reconstruction path.
        /// Rebuilding the scene at PreBoss must find every mandatory gate already
        /// open; it awards nothing, so replaying it is safe.
        /// </summary>
        public void ForceResolve() {
            if (IsResolved) return;
            IsResolved = true;
            ApplyStrikeSurfaceLayer();
            UpdatePresentation();
            Resolved?.Invoke(this);
        }

        // === Strike ===

        private void BuildStrikeSurface() {
            if (_strikeSurface != null && IsInstanceValid(_strikeSurface)) return;
            _strikeSurface = new EnvironmentHurtboxAdapter {
                Name = "GateSurface",
                OwnerPlayerIndex = -1,
                CollisionLayer = StrikeSurfaceLayer(),
                CollisionMask = CollisionLayers.PlayerHitbox,
                Monitoring = true,
                Monitorable = true
            };
            _strikeShape = new CollisionShape2D {
                Shape = new RectangleShape2D { Size = StrikeSurfaceSize },
                Position = new Vector2(0, -StrikeSurfaceSize.Y / 2f)
            };
            _strikeSurface.AddChild(_strikeShape);
            _strikeSurface.OnHit += OnStruck;
            AddChild(_strikeSurface);
        }

        /// <summary>
        /// PersistentObject always; EnemyHurtbox while <see cref="StrikeSurfaceOnEnemyHurtbox"/>
        /// holds and the gate is still unsolved.
        /// </summary>
        private uint StrikeSurfaceLayer() =>
            CollisionLayers.PersistentObject
            | (StrikeSurfaceOnEnemyHurtbox && !IsResolved ? CollisionLayers.EnemyHurtbox : 0u);

        /// <summary>
        /// Re-applies the surface layer. Deferred inside a physics callback, because a
        /// latch usually arrives from a hit, mid-flush.
        /// </summary>
        private void ApplyStrikeSurfaceLayer() {
            if (_strikeSurface == null || !IsInstanceValid(_strikeSurface)) return;
            uint layer = StrikeSurfaceLayer();
            if (PhysicsCallbackGuard.IsInPhysicsCallback) {
                _strikeSurface.SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, layer);
            } else {
                _strikeSurface.CollisionLayer = layer;
            }
        }

        /// <summary>
        /// Configures the strike surface once the gate is in the tree — the only seam
        /// a 4A variant has, because the sealed <c>BuildLevel</c> builds the gate.
        /// Builds the surface when the mode did not (a CastWatch gate whose cast
        /// deploys a construct keeps a surface that construct can strike), resizes it,
        /// and sets the <see cref="StrikeSurfaceOnEnemyHurtbox"/> option. Call from a
        /// level hook, never from inside a physics callback.
        /// </summary>
        public void ConfigureStrikeSurface(Vector2? size = null, bool? onEnemyHurtbox = null) {
            if (size is Vector2 newSize) StrikeSurfaceSize = newSize;
            if (onEnemyHurtbox is bool enabled) StrikeSurfaceOnEnemyHurtbox = enabled;
            if (_strikeSurface == null || !IsInstanceValid(_strikeSurface)) {
                BuildStrikeSurface();
                return;
            }
            if (_strikeShape?.Shape is RectangleShape2D rect) rect.Size = StrikeSurfaceSize;
            if (_strikeShape != null) _strikeShape.Position = new Vector2(0, -StrikeSurfaceSize.Y / 2f);
            ApplyStrikeSurfaceLayer();
        }

        /// <summary>The gate's strike surface, or null when it has none. Test seam.</summary>
        internal EnvironmentHurtboxAdapter StrikeSurface =>
            _strikeSurface != null && IsInstanceValid(_strikeSurface) ? _strikeSurface : null;

        /// <summary>
        /// Hurtbox receiver. Enemy hitboxes share the -1 owner index and are skipped
        /// by the Hitbox pipeline, and this additionally requires a player attacker,
        /// so nothing but the hero's own ability can open the gate. Returns 0 damage:
        /// a gate is a mechanism, not a combat target, and must not credit meter.
        /// </summary>
        internal float OnStruck(FTT.Combat.HitPayload payload) {
            if (payload.AttackerIndex >= 0) TryResolve(payload.AttackID);
            return 0f;
        }

        // === Zone ===

        private void PollZones() {
            Godot.Collections.Array<Node> zones = GetTree()?.GetNodesInGroup("story_zone");
            if (zones != null) {
                using var lifetime = zones.AsDisposable();
                foreach (Node node in zones) {
                    if (node is not FTT.Combat.PlaceholderZone zone || !IsInstanceValid(zone)) continue;
                    if (!string.Equals(zone.AbilityID, RequiredAbilityID, StringComparison.Ordinal)) continue;
                    if (zone.GlobalPosition.DistanceTo(GlobalPosition) > ResolveRadius) continue;
                    TryResolve(zone.AbilityID);
                    return;
                }
            }
            if (ZoneAcceptsOwnedConstruct) PollOwnedConstructs();
        }

        // === Zone option: an owned construct ===

        /// <summary>
        /// Walks the hero's own <c>ActivePersistentObjects</c> (no engine collection)
        /// for a live construct deployed by the authored ability. A construct from
        /// another ability, a decoy or an enemy can never satisfy it (V01c).
        /// </summary>
        private void PollOwnedConstructs() {
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not PlayerController player) return;
            if (!IsInstanceValid(player)) return;
            PackedScene requiredScene = FindRequiredSpecial(player)?.Data?.PersistentObjectScene;
            if (requiredScene == null) return;
            bool suspended = IsWorldSuspended(player, GetTree());
            foreach (Node2D node in player.ActivePersistentObjects) {
                if (TryRecognizeConstruct(node, requiredScene, suspended)) return;
            }
        }

        /// <summary>
        /// Resolves the gate from <paramref name="construct"/> when it is a live
        /// construct spawned from <paramref name="requiredScene"/> within
        /// <see cref="ResolveRadius"/> and the world is not suspended. The single
        /// entry point for the construct option — the poll and the tests share it.
        /// </summary>
        internal bool TryRecognizeConstruct(Node2D construct, PackedScene requiredScene, bool worldSuspended) {
            if (IsResolved || !ZoneAcceptsOwnedConstruct || worldSuspended) return false;
            if (construct == null || !IsInstanceValid(construct) || requiredScene == null) return false;
            if (construct is not PooledNode pooled || !IsSameScene(pooled.SceneOrigin, requiredScene)) return false;
            if (!IsConstructLive(construct)) return false;
            if (construct.GlobalPosition.DistanceTo(GlobalPosition) > ResolveRadius) return false;
            return TryResolve(RequiredAbilityID);
        }

        private static bool IsSameScene(PackedScene origin, PackedScene required) {
            if (origin == null || required == null) return false;
            if (ReferenceEquals(origin, required)) return true;
            return !string.IsNullOrEmpty(required.ResourcePath)
                && string.Equals(origin.ResourcePath, required.ResourcePath, StringComparison.Ordinal);
        }

        /// <summary>A destroyed construct can linger in its owner's list until a deferred release lands.</summary>
        private static bool IsConstructLive(Node2D construct) => construct switch {
            FTT.Characters.Abilities.VineSnareNode snare => !snare.IsSnareDestroyed,
            FTT.Characters.Abilities.TeslaCoilNode coil => !coil.IsCoilDestroyed,
            FTT.Characters.Abilities.SerpentNestNode nest => !nest.IsNestDestroyed,
            FTT.Characters.Abilities.LeonardoTurretNode turret => !turret.IsTurretDestroyed,
            _ => true
        };

        // === CastWatch ===

        /// <summary>
        /// Latches while the hero is mid-execution of the authored ability inside
        /// <see cref="CastWatchRadius"/>. Refused while the world is suspended.
        /// </summary>
        private void PollCastWatch() {
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not PlayerController player) return;
            if (!IsInstanceValid(player)) return;
            FTT.Combat.BaseSpecial special = FindRequiredSpecial(player);
            if (special == null || !special.IsExecuting) return;
            TryRecognizeCast(special.Data.AbilityID, player.GlobalPosition, IsWorldSuspended(player, GetTree()));
        }

        /// <summary>
        /// Resolves a CastWatch gate when <paramref name="castAbilityID"/> is the
        /// authored one, cast within <see cref="CastWatchRadius"/>, and the world is
        /// not suspended. The single entry point — the poll and the tests share it.
        /// </summary>
        internal bool TryRecognizeCast(string castAbilityID, Vector2 castPosition, bool worldSuspended) {
            if (Mode != LegacyGateMode.CastWatch || worldSuspended) return false;
            if (castPosition.DistanceTo(GlobalPosition) > CastWatchRadius) return false;
            return TryResolve(castAbilityID);
        }

        /// <summary>The player's own ability node carrying the authored ability ID — never a slot.</summary>
        private FTT.Combat.BaseSpecial FindRequiredSpecial(PlayerController player) {
            if (string.IsNullOrWhiteSpace(RequiredAbilityID)) return null;
            Godot.Collections.Array<Node> children = player.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is not FTT.Combat.BaseSpecial special || !IsInstanceValid(special)) continue;
                if (string.Equals(special.Data?.AbilityID, RequiredAbilityID, StringComparison.Ordinal)) return special;
            }
            return null;
        }

        /// <summary>
        /// Package 12 W1: Time Freeze, the Post-Landing Hold (the hero's
        /// <c>IsRecoveryWorldHeld</c>) and a boss's T01b suspension
        /// (<see cref="ChronalRewindManager.IsWorldHeld"/>) — the same facts
        /// <c>InteractionArea.TryInteract</c> refuses on and the Hitbox/Hurtbox gates
        /// discard struck deliveries on, so the polled recognisers cannot latch
        /// through a suspension the struck modes already ignore.
        /// </summary>
        internal static bool IsWorldSuspended(PlayerController player, SceneTree tree) =>
            (player != null && IsInstanceValid(player) && (player.TimeFrozen || player.IsRecoveryWorldHeld))
            || ChronalRewindManager.IsWorldHeld(tree);

        // === Traversal ===

        private void BuildTraversalArea() {
            _traversalArea = new Area2D {
                Name = "LandingZone",
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player,
                Monitoring = true
            };
            _traversalArea.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = TraversalBoxSize },
                Position = new Vector2(0, -TraversalBoxSize.Y / 2f)
            });
            AddChild(_traversalArea);
        }

        /// <summary>
        /// A movement ability that teleports (Einstein's Warp) never overlaps the
        /// gate mid-cast, so the gate watches the player for a movement-ability use
        /// and then accepts an arrival inside the landing box within the grace
        /// window. Ordinary walking or jumping into the box resolves nothing.
        /// </summary>
        private void PollTraversal() {
            if (_traversalArea == null) return;
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not PlayerController player) return;

            if (player.CurrentState == CharacterState.UsingMovementAbility) {
                _traversalGraceFrames = TraversalGraceFrames;
            } else if (_traversalGraceFrames > 0) {
                _traversalGraceFrames--;
            }
            if (_traversalGraceFrames <= 0) return;

            Godot.Collections.Array<Node2D> bodies = _traversalArea.GetOverlappingBodies();
            using var lifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body != player) continue;
                TryResolve(RequiredAbilityID);
                return;
            }
        }

        /// <summary>Test seam: the traversal latch without driving physics frames.</summary>
        internal void NotifyMovementAbilityUsed() => _traversalGraceFrames = TraversalGraceFrames;

        /// <summary>Test seam: whether the traversal grace window is still open.</summary>
        internal bool TraversalWindowOpen => _traversalGraceFrames > 0;

        // === Presentation (graybox; Package 10 replaces it) ===

        private void BuildPresentation() {
            _visual = new ColorRect {
                Name = "GateVisual",
                Size = new Vector2(40, 160),
                Position = new Vector2(-20, -160),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_visual);

            _prompt = new Label {
                Name = "GatePrompt",
                Text = Tr(PromptKeyFor(Mode)),
                Position = new Vector2(-110, -200),
                CustomMinimumSize = new Vector2(220, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _prompt.AddThemeFontSizeOverride("font_size", 10);
            _prompt.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 1f));
            AddChild(_prompt);
        }

        /// <summary>The gate's authored hint key, by mode. Localized; never English in code.</summary>
        public static string PromptKeyFor(LegacyGateMode mode) => mode switch {
            LegacyGateMode.Strike => "legacy_gate_strike_prompt",
            LegacyGateMode.Zone => "legacy_gate_zone_prompt",
            LegacyGateMode.Traversal => "legacy_gate_traversal_prompt",
            LegacyGateMode.CastWatch => "legacy_gate_cast_prompt",
            _ => "legacy_gate_nexus_prompt"
        };

        private void UpdatePresentation() {
            if (_visual != null) {
                _visual.Color = IsResolved
                    ? new Color(0.3f, 0.9f, 0.75f, 0.35f)
                    : new Color(0.85f, 0.45f, 0.2f, 0.75f);
            }
            if (_prompt != null) _prompt.Visible = !IsResolved;
        }
    }
}
