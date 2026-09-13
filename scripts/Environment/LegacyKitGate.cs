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
    /// for it. These modes are the four shapes the nine roster kits actually take;
    /// a variant that needs a fifth records it in the plan's §9 rather than
    /// loosening an existing one.</para>
    /// </summary>
    public enum LegacyGateMode {
        /// <summary>A projectile or melee special: the gate's own hurtbox matches <c>HitPayload.AttackID</c>.</summary>
        Strike = 0,
        /// <summary>A placed zone special: a live <c>story_zone</c> with the authored ability ID overlaps the gate.</summary>
        Zone = 1,
        /// <summary>The movement ability: the player reaches the gate during (or just after) a movement-ability use.</summary>
        Traversal = 2,
        /// <summary>The Ultimate set-piece: resolved only by <see cref="NexusResonanceSource"/> (F04).</summary>
        Nexus = 3
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

        public bool IsResolved { get; private set; }

        /// <summary>Raised once, on the transition to resolved.</summary>
        public event Action<LegacyKitGate> Resolved;

        private int _traversalGraceFrames;
        private Area2D _traversalArea;
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
            UpdatePresentation();
            Resolved?.Invoke(this);
        }

        // === Strike ===

        private void BuildStrikeSurface() {
            var surface = new EnvironmentHurtboxAdapter {
                Name = "GateSurface",
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.PersistentObject,
                CollisionMask = CollisionLayers.PlayerHitbox,
                Monitoring = true,
                Monitorable = true
            };
            surface.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = StrikeSurfaceSize },
                Position = new Vector2(0, -StrikeSurfaceSize.Y / 2f)
            });
            surface.OnHit += OnStruck;
            AddChild(surface);
        }

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
            if (zones == null) return;
            using var lifetime = zones.AsDisposable();
            foreach (Node node in zones) {
                if (node is not FTT.Combat.PlaceholderZone zone || !IsInstanceValid(zone)) continue;
                if (!string.Equals(zone.AbilityID, RequiredAbilityID, StringComparison.Ordinal)) continue;
                if (zone.GlobalPosition.DistanceTo(GlobalPosition) > ResolveRadius) continue;
                TryResolve(zone.AbilityID);
                return;
            }
        }

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
