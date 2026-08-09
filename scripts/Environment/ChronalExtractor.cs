using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public enum ChronalExtractorVisualState { Idle, Damaged, Destroyed }

    public partial class ChronalExtractor : DamageableEnvironmentObject {
        [Export(PropertyHint.Range, "0,100,1")] public int HazardDamage = 20;
        [Export] public Vector2 HazardKnockback = new(420f, -260f);
        [Export(PropertyHint.Range, "0,100,1")] public float UltimateDrain = 20f;
        [Export(PropertyHint.Range, "1,100,1")] public int DustReward = 15;

        private Area2D _hazardArea;
        public ChronalExtractorVisualState VisualState { get; private set; }
        public int DischargeCount { get; private set; }

        public override void _Ready() {
            MaxHP = 100;
            HitsToBreak = 0;
            base._Ready();
            AddToGroup("chronal_extractor");
            _hazardArea = GetNodeOrNull<Area2D>("HazardArea");
            Publish();
        }

        public void Discharge() {
            if (IsDestroyed) return;
            DischargeCount++;
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = ObjectID,
                Phase = HazardPhase.Active,
                Duration = 0.25f
            });
            EmitDischargeVfx();
            if (_hazardArea == null) return;
            Godot.Collections.Array<Node2D> bodies = _hazardArea.GetOverlappingBodies();
            using var bodiesLifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body is PlayerController player) ApplyDischargeToPlayer(player);
            }
        }

        public void ApplyDischargeToPlayer(PlayerController player) {
            if (player == null || IsDestroyed) return;
            player.ApplyDamage(HazardDamage);
            float direction = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
            player.Velocity += new Vector2(HazardKnockback.X * direction, HazardKnockback.Y);
            player.DrainUltimateMeter(UltimateDrain);
        }

        protected override void OnDamaged(int appliedDamage) {
            VisualState = CurrentHP <= MaxHP / 2
                ? ChronalExtractorVisualState.Damaged
                : ChronalExtractorVisualState.Idle;
            Discharge();
            Publish();
        }

        protected override void OnDestroyed() {
            VisualState = ChronalExtractorVisualState.Destroyed;
            EventBus.Instance?.RaiseChronalDustCollected(DustReward);
            Publish();
        }

        /// <summary>
        /// Package 8 B6: a shockwave at the discharge ring, so the 190-unit hazard
        /// radius that already damages the player is visible before it lands. Pooled
        /// and null-safe — a scene with no VFX pool simply gets no effect.
        /// </summary>
        private void EmitDischargeVfx() {
            if (!IsInsideTree()) return;
            PackedScene scene = FTT.Combat.VfxLibrary.Load(FTT.Combat.VfxEffectFamily.Shockwave);
            if (scene == null) {
                FTT.Combat.VfxEmitter.EmitEnvironment(GlobalPosition, GetParent(), DischargeColor);
                return;
            }
            FTT.Combat.VfxEmitter.EmitScene(scene, GlobalPosition, GetParent(), DischargeColor);
        }

        /// <summary>Apex Archive cyan, matching the extractor's core glow.</summary>
        public static readonly Color DischargeColor = new(0.2f, 0.95f, 1f, 0.85f);

        protected override void ApplyStatePresentation() {
            base.ApplyStatePresentation();
            VisualState = IsDestroyed
                ? ChronalExtractorVisualState.Destroyed
                : CurrentHP <= MaxHP / 2 ? ChronalExtractorVisualState.Damaged : ChronalExtractorVisualState.Idle;
            if (GetNodeOrNull<CanvasItem>("DamagedVisual") is CanvasItem damaged) {
                damaged.Visible = VisualState == ChronalExtractorVisualState.Damaged;
            }
            // Package 8 B6 dressing: the destroyed husk, the sparks that only run
            // while the machine is wounded, and the core glow that dies with it.
            if (GetNodeOrNull<CanvasItem>("DestroyedVisual") is CanvasItem destroyed) {
                destroyed.Visible = VisualState == ChronalExtractorVisualState.Destroyed;
            }
            if (GetNodeOrNull<CpuParticles2D>("DamageSparks") is CpuParticles2D sparks) {
                sparks.Emitting = VisualState == ChronalExtractorVisualState.Damaged;
            }
            if (GetNodeOrNull<CanvasItem>("CoreGlow") is CanvasItem core) {
                core.Visible = !IsDestroyed;
                core.Modulate = VisualState == ChronalExtractorVisualState.Damaged
                    ? new Color(1f, 0.45f, 0.2f, 1f)
                    : Colors.White;
            }
        }

        private void Publish() => EventBus.Instance?.RaiseExtractorStateChanged(new ExtractorStatePayload {
            ExtractorID = ObjectID,
            CurrentHP = CurrentHP,
            MaxHP = MaxHP,
            IsDestroyed = IsDestroyed
        });
    }
}
