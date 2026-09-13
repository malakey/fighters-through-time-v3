using Godot;
using FTT.Combat;
using FTT.Core;

namespace FTT.Environment {

    public partial class DamageableEnvironmentObject : StaticBody2D, IStoryRewindable {
        [Signal] public delegate void DamagedEventHandler(float currentHP, float maxHP);
        [Signal] public delegate void DestroyedEventHandler();

        [Export] public string ObjectID = "";
        [Export(PropertyHint.Range, "1,10000,1")] public int MaxHP = 30;
        [Export(PropertyHint.Range, "0,3,1")] public int HitsToBreak;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        protected EnvironmentHurtboxAdapter Hurtbox;
        private int _checkpointHP;
        private int _initialHP;
        public int CurrentHP { get; private set; }
        public int HitCount { get; private set; }
        public bool IsDestroyed { get; private set; }

        public override void _Ready() {
            AddToGroup("damageable_environment");
            _initialHP = Mathf.Max(1, MaxHP);
            CurrentHP = _initialHP;
            _checkpointHP = CurrentHP;
            Hurtbox = GetNodeOrNull<EnvironmentHurtboxAdapter>("Hurtbox");
            if (Hurtbox != null) Hurtbox.OnHit += OnHurtboxHit;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            ApplyStatePresentation();
        }

        public override void _ExitTree() {
            if (Hurtbox != null) Hurtbox.OnHit -= OnHurtboxHit;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public int TakeEnvironmentDamage(float requestedDamage) {
            if (IsDestroyed) return 0;
            int applied;
            if (HitsToBreak > 0) {
                HitCount++;
                int targetHP = Mathf.CeilToInt(_initialHP * (1f - HitCount / (float)HitsToBreak));
                applied = CurrentHP - Mathf.Max(0, targetHP);
            } else {
                applied = Mathf.Clamp(Mathf.RoundToInt(requestedDamage), 0, CurrentHP);
            }
            CurrentHP = Mathf.Max(0, CurrentHP - applied);
            OnDamaged(applied);
            EmitSignal(SignalName.Damaged, CurrentHP, MaxHP);
            if (CurrentHP <= 0) DestroyObject();
            else ApplyStatePresentation();
            return applied;
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointHP = CurrentHP;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            int restoredHP = RewindPolicy == StoryRewindPolicy.ResetToInitialState ? _initialHP : _checkpointHP;
            RestoreState(restoredHP);
        }

        protected virtual void OnDamaged(int appliedDamage) { }
        protected virtual void OnDestroyed() { }

        protected void RestoreState(int hp) {
            CurrentHP = Mathf.Clamp(hp, 0, _initialHP);
            IsDestroyed = CurrentHP <= 0;
            HitCount = HitsToBreak > 0
                ? Mathf.Clamp(Mathf.RoundToInt((1f - CurrentHP / (float)_initialHP) * HitsToBreak), 0, HitsToBreak)
                : 0;
            ApplyStatePresentation();
        }

        protected virtual void ApplyStatePresentation() {
            if (GetNodeOrNull<CanvasItem>("Visual") is CanvasItem visual) visual.Visible = !IsDestroyed;
            // Safe setters: a destroying hit arrives inside the hit signal's
            // physics flush, where the direct writes are engine-blocked.
            if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D bodyShape) bodyShape.SetShapeDisabledSafe(IsDestroyed);
            if (Hurtbox != null) {
                Hurtbox.SetMonitorableSafe(!IsDestroyed);
                Hurtbox.SetMonitoringSafe(!IsDestroyed);
            }
        }

        private float OnHurtboxHit(HitPayload payload) {
            TakeEnvironmentDamage(payload.Damage * ResolveIncomingDamageMultiplier(payload));
            return 0f;
        }

        /// <summary>
        /// Package 11 A4: the single hook a subclass overrides to scale an
        /// incoming player hit (Chronal Extractors apply the V7.6 Resonance
        /// Extractor riders here). Neutral 1.0 by default.
        /// </summary>
        protected virtual float ResolveIncomingDamageMultiplier(in HitPayload payload) => 1f;

        /// <summary>
        /// Resolves the attacking <c>PlayerController</c> for a hit payload via
        /// the "Players" group - the same lookup the Extractor's siphon-engage
        /// sweep uses, so it stays headless-safe and needs no back-reference on
        /// the payload. Returns null for enemy, hazard and construct sources.
        /// </summary>
        protected FTT.Characters.PlayerController FindAttackingPlayer(int attackerIndex) {
            if (attackerIndex < 0 || !IsInsideTree()) return null;
            Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
            using var lifetime = players.AsDisposable();
            foreach (Node node in players) {
                if (node is FTT.Characters.PlayerController player
                    && player.PlayerIndex == attackerIndex) return player;
            }
            return null;
        }

        private void DestroyObject() {
            if (IsDestroyed) return;
            IsDestroyed = true;
            ApplyStatePresentation();
            OnDestroyed();
            EmitSignal(SignalName.Destroyed);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
    }
}
