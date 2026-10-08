using Godot;
using System;
using FTT.Core;
using FTT.Environment;
using FTT.UI;

namespace FTT.Enemies {

    /// <summary>
    /// Encounter wrapper for the Level 13 Mirror Paradox, parallel to
    /// <see cref="BossEncounterController"/>. It owns the same contract — spawn on
    /// reveal, HUD boss bar through the shared boss events, dust award on defeat,
    /// rewind freeze policy — but wraps <see cref="MirrorParadoxController"/> instead
    /// of <see cref="BossController"/>, because the mirror is a CPU-driven character
    /// clone rather than a <see cref="BossData"/> attack-pattern boss.
    /// </summary>
    public partial class MirrorParadoxEncounterController : Node2D, IStoryRewindSimulation {
        public const string DefaultBossDataPath = "res://resources/Bosses/mirror_paradox.tres";

        [ExportGroup("Encounter")]
        [Export] public BossData Data;
        /// <summary>Overrides the session's locked character; production levels leave this empty.</summary>
        [Export] public string CharacterIDOverride = "";
        [Export] public Vector2 SpawnOffset;
        [Export] public bool SpawnOnReady = true;
        /// <summary>Distance at which the HUD bar and intro fire; 0 reveals immediately.</summary>
        [Export] public float RevealDistance = 800f;
        /// <summary>
        /// P1 (2026-10-04): the arena the clone is leashed to (global; horizontal
        /// extent only; zero width = unbounded). The reveal stays the radius.
        /// </summary>
        [Export] public Rect2 ArenaBounds;
        [Export] public bool AwardDustOnDefeat = true;
        [Export] public ulong DecisionSeed;
        [Export] public StoryRewindPolicy MirrorRewindPolicy { get; set; } = StoryRewindPolicy.PreserveCurrentState;

        public MirrorParadoxController Mirror { get; private set; }
        public bool IsRevealed { get; private set; }
        public bool IsDefeated { get; private set; }

        /// <summary>Raised once when the player first comes within reveal range.</summary>
        public event Action MirrorRevealed;

        /// <summary>Raised once on defeat, after the dust award.</summary>
        public event Action<BossDefeatedPayload> MirrorDefeated;

        /// <summary>Assigned by the level after StorySceneBootstrapper builds the HUD.</summary>
        public StoryHUD HUD { get; set; }

        private FTT.Characters.PlayerController _player;
        private bool _eventsBound;

        public override void _Ready() {
            Data ??= FTT.Core.AuthoredResources.Load<BossData>(DefaultBossDataPath);
            BindEvents();
            if (SpawnOnReady) SpawnMirror();
        }

        /// <summary>
        /// Only global registrations are released here. Godot is already destroying
        /// the encounter subtree, and <see cref="MirrorParadoxController._ExitTree"/>
        /// releases the CPU input slot on its own — doing tree surgery from inside a
        /// parent's <c>_ExitTree</c> would fight that teardown. Levels that end the
        /// encounter while the scene stays loaded call
        /// <see cref="MirrorParadoxController.DespawnMirror"/> explicitly instead.
        /// </summary>
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

        /// <summary>Instantiates the mirror controller at the encounter anchor.</summary>
        public MirrorParadoxController SpawnMirror() {
            if (Mirror != null && IsInstanceValid(Mirror)) return Mirror;
            Data ??= FTT.Core.AuthoredResources.Load<BossData>(DefaultBossDataPath);
            Mirror = new MirrorParadoxController {
                Name = "MirrorParadox",
                Data = Data,
                CharacterIDOverride = CharacterIDOverride,
                SpawnOffset = SpawnOffset,
                DecisionSeed = DecisionSeed,
                RewindPolicy = MirrorRewindPolicy,
                SpawnOnReady = true,
                // The clone stands inert until Reveal() starts the fight.
                ActivateOnSpawn = false,
                ArenaBounds = ArenaBounds
            };
            AddChild(Mirror);
            return Mirror;
        }

        public override void _Process(double delta) {
            if (IsRevealed || IsDefeated || Mirror == null || !IsInstanceValid(Mirror)) return;
            if (RevealDistance <= 0f) {
                Reveal();
                return;
            }
            if (_player == null || !IsInstanceValid(_player)) {
                _player = GetTree()?.GetFirstNodeInGroup("StoryPlayer") as FTT.Characters.PlayerController;
            }
            if (_player == null) return;
            if (_player.GlobalPosition.DistanceTo(Mirror.GlobalPosition) <= RevealDistance) Reveal();
        }

        /// <summary>Shows the 1000-HP boss bar and fires the level's intro hook.</summary>
        public void Reveal() {
            if (IsRevealed) return;
            IsRevealed = true;
            Mirror?.BeginEncounter();
            HUD?.ShowBossBar(
                string.IsNullOrWhiteSpace(Data?.DisplayNameKey) ? "boss_mirror_paradox_name" : Data.DisplayNameKey,
                Mirror?.CurrentHP ?? 0,
                Mirror?.ScaledMaxHP ?? 1);
            MirrorRevealed?.Invoke();
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
            // Package 11 A10 (F05): the Mirror Paradox follows the same 25-dust
            // physical-pickup rule as every other boss — its wallet-direct path
            // is removed. The wallet is paid, the claim committed and the boss
            // results line attributed at COLLECTION, through the Single Icon
            // Rule's shared award spawner, with the explicit Boss attribution
            // Level 13 used to supply by hand.
            if (AwardDustOnDefeat) {
                int reward = LevelRewardDirectory.ResolveBossAward(
                    payload.ChronalDustDrop, out string rewardSourceID);
                if (reward > 0) {
                    StoryDropSystem.SpawnDustAward(
                        reward, payload.Position != Vector2.Zero ? payload.Position : GlobalPosition,
                        GetParent() ?? this, DustAwardSource.Boss, rewardSourceID);
                }
            }
            MirrorDefeated?.Invoke(payload);
        }

        public void SetStoryRewindFrozen(bool frozen) {
            if (Mirror != null && IsInstanceValid(Mirror)) Mirror.SetStoryRewindFrozen(frozen);
        }
    }
}
