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
            // Package 8 B5. The discharge already sounds through the hazard event the
            // director subscribes to; the break itself is a separate, lower beat.
            EnvironmentAudioCues.PlayDestruction();
            Publish();
        }

        protected override void ApplyStatePresentation() {
            base.ApplyStatePresentation();
            VisualState = IsDestroyed
                ? ChronalExtractorVisualState.Destroyed
                : CurrentHP <= MaxHP / 2 ? ChronalExtractorVisualState.Damaged : ChronalExtractorVisualState.Idle;
            if (GetNodeOrNull<CanvasItem>("DamagedVisual") is CanvasItem damaged) {
                damaged.Visible = VisualState == ChronalExtractorVisualState.Damaged;
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
