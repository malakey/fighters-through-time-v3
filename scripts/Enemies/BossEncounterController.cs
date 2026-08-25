using Godot;
using System;
using FTT.Core;
using FTT.Environment;
using FTT.UI;

namespace FTT.Enemies {

    /// <summary>
    /// Reusable boss-encounter wrapper a level scene drops into its arena. Owns the
    /// authored boss scene spawn, the HUD boss bar wiring through the boss events,
    /// the dust award on defeat, and the rewind freeze/restore policy. Level content
    /// keeps its own dialogue and objective flow by listening to the two events here.
    /// </summary>
    public partial class BossEncounterController : Node2D, IStoryRewindSimulation {
        public const string DefaultBossScenePath = "res://scenes/enemies/Boss.tscn";

        [ExportGroup("Encounter")]
        [Export] public BossData Data;
        [Export] public PackedScene BossScene;
        [Export] public Vector2 SpawnOffset;
        [Export] public bool SpawnOnReady = true;
        /// <summary>Distance at which the HUD bar and intro fire; 0 reveals immediately.</summary>
        [Export] public float RevealDistance = 800f;
        [Export] public bool AwardDustOnDefeat = true;
        [Export] public StoryRewindPolicy BossRewindPolicy { get; set; } = StoryRewindPolicy.PreserveCurrentState;

        public BossController Boss { get; private set; }
        public bool IsRevealed { get; private set; }
        public bool IsDefeated { get; private set; }

        /// <summary>Raised once when the player first comes within reveal range.</summary>
        public event Action BossRevealed;

        /// <summary>Raised once on defeat, after the dust award.</summary>
        public event Action<BossDefeatedPayload> BossDefeated;

        /// <summary>Assigned by the level after StorySceneBootstrapper builds the HUD.</summary>
        public StoryHUD HUD { get; set; }

        private FTT.Characters.PlayerController _player;
        private bool _eventsBound;

        public override void _Ready() {
            BindEvents();
            if (SpawnOnReady) SpawnBoss();
        }

        public override void _ExitTree() => UnbindEvents();

        private void BindEvents() {
            if (_eventsBound || EventBus.Instance == null) return;
            _eventsBound = true;
            EventBus.Instance.OnBossHPChanged += OnBossHPChanged;
            EventBus.Instance.OnBossDefeated += OnBossDefeated;
        }

        private void UnbindEvents() {
            if (!_eventsBound || EventBus.Instance == null) {
                _eventsBound = false;
                return;
            }
            _eventsBound = false;
            EventBus.Instance.OnBossHPChanged -= OnBossHPChanged;
            EventBus.Instance.OnBossDefeated -= OnBossDefeated;
        }

        /// <summary>Instantiates the authored boss scene at the encounter anchor.</summary>
        public BossController SpawnBoss() {
            if (Boss != null && IsInstanceValid(Boss)) return Boss;
            PackedScene scene = BossScene ?? GD.Load<PackedScene>(DefaultBossScenePath);
            if (scene == null) {
                GD.PushWarning($"Boss encounter '{Name}' has no boss scene.");
                return null;
            }
            Boss = scene.Instantiate<BossController>();
            Boss.Name = string.IsNullOrWhiteSpace(Data?.BossID) ? "Boss" : Data.BossID.ToPascalCase();
            Boss.Data = Data;
            Boss.RewindPolicy = BossRewindPolicy;
            Boss.Position = SpawnOffset;
            AddChild(Boss);
            return Boss;
        }

        /// <summary>The intro's establishing beat, per the V7 boss directive.</summary>
        public const float IntroBeatSeconds = 1.5f;

        private float _introBeatSecondsRemaining = -1f;

        /// <summary>True while the intro's establishing beat holds the boss. Test seam.</summary>
        public bool IsIntroBeatRunning => _introBeatSecondsRemaining > 0f;

        public override void _Process(double delta) {
            AdvanceFreeTelegraph();
            if (AdvanceIntroBeat((float)delta)) return;
            if (IsRevealed || IsDefeated || Boss == null || !IsInstanceValid(Boss)) return;
            if (RevealDistance <= 0f) {
                Reveal();
                return;
            }
            if (_player == null || !IsInstanceValid(_player)) {
                _player = GetTree()?.GetFirstNodeInGroup("StoryPlayer") as FTT.Characters.PlayerController;
            }
            if (_player == null) return;
            if (_player.GlobalPosition.DistanceTo(Boss.GlobalPosition) <= RevealDistance) Reveal();
        }

        /// <summary>
        /// V7 boss intro ritual: a name card over a 1.5 s establishing beat
        /// (the boss held in place), the boss's signature telegraph shown once
        /// for free, then the HUD bar sweeps in. Skipped on repeat attempts —
        /// the seen-set lives on StoryManager and survives a Timeline Collapse.
        /// </summary>
        private void Reveal() {
            if (IsRevealed) return;
            IsRevealed = true;
            bool skipIntro = FTT.Core.StoryManager.Instance?.HasSeenBossIntro(Data?.BossID) == true;
            if (!skipIntro && Data != null && Boss != null && IsInstanceValid(Boss)) {
                FTT.Core.StoryManager.Instance?.RecordBossIntroSeen(Data.BossID);
                _introBeatSecondsRemaining = IntroBeatSeconds;
                Boss.SetStoryRewindFrozen(true);
                HUD?.ShowBossIntroCard(
                    string.IsNullOrWhiteSpace(Data.DisplayNameKey) ? "boss" : Data.DisplayNameKey,
                    IntroBeatSeconds + 0.5f);
                return;
            }
            ShowBar();
        }

        private bool AdvanceIntroBeat(float dt) {
            if (_introBeatSecondsRemaining <= 0f) return false;
            _introBeatSecondsRemaining -= dt;
            if (_introBeatSecondsRemaining > 0f) return true;
            _introBeatSecondsRemaining = -1f;
            if (Boss != null && IsInstanceValid(Boss)) {
                Boss.SetStoryRewindFrozen(false);
                // The signature telegraph, shown once for free: the first
                // authored ability winds up fully, then cancels into recovery
                // with no payload — the player reads the pattern without paying.
                if (Data?.BossAbilities is { Length: > 0 } && Data.BossAbilities[0] != null) {
                    Boss.BeginAbility(Data.BossAbilities[0],
                        _player?.GlobalPosition ?? Boss.GlobalPosition);
                    _freeTelegraphPending = true;
                }
            }
            ShowBar();
            return true;
        }

        private bool _freeTelegraphPending;

        /// <summary>Cuts the free intro telegraph at its last wind-up frame so
        /// the full flash plays but the payload never fires.</summary>
        private void AdvanceFreeTelegraph() {
            if (!_freeTelegraphPending || Boss == null || !IsInstanceValid(Boss)) {
                _freeTelegraphPending = false;
                return;
            }
            if (Boss.IsTelegraphing) {
                if (Boss.AbilityFramesRemaining <= 1) {
                    Boss.CancelTelegraphIntoRecovery();
                    _freeTelegraphPending = false;
                }
                return;
            }
            // Zero-frame telegraph or an interruption: nothing left to guard.
            _freeTelegraphPending = false;
        }

        private void ShowBar() {
            // Package 8 B1: the authored phase thresholds travel with the reveal so
            // the HUD can notch the bar. They are the same array BossController
            // advances on, passed through rather than copied, so a notch cannot
            // disagree with the fight.
            HUD?.ShowBossBar(
                string.IsNullOrWhiteSpace(Data?.DisplayNameKey) ? "boss" : Data.DisplayNameKey,
                Boss?.CurrentHP ?? 0,
                Boss?.ScaledMaxHP ?? 1,
                Data?.PhaseThresholds);
            BossRevealed?.Invoke();
        }

        private void OnBossHPChanged(BossHPPayload payload) {
            if (!IsRevealed || IsDefeated) return;
            if (!string.IsNullOrEmpty(Data?.BossID) && payload.BossID != Data.BossID) return;
            HUD?.UpdateBossHP(payload.CurrentHP);
        }

        private void OnBossDefeated(BossDefeatedPayload payload) {
            if (IsDefeated) return;
            if (!string.IsNullOrEmpty(Data?.BossID) && payload.BossID != Data.BossID) return;
            IsDefeated = true;
            HUD?.HideBossBar();
            // Boss dust is resource-authored (docs/DUST_ECONOMY.md Section 1).
            if (AwardDustOnDefeat) EventBus.Instance?.RaiseChronalDustCollected(payload.ChronalDustDrop);
            BossDefeated?.Invoke(payload);
        }

        public void SetStoryRewindFrozen(bool frozen) {
            if (Boss != null && IsInstanceValid(Boss)) Boss.SetStoryRewindFrozen(frozen);
        }
    }
}
