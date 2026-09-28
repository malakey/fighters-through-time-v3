using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.UI;

namespace FTT.Enemies {

    /// <summary>
    /// Reusable boss-encounter wrapper a level scene drops into its arena. Owns the
    /// authored boss scene spawn, the HUD boss bar wiring through the boss events,
    /// the dust award on defeat, and the rewind freeze/restore policy. Level content
    /// keeps its own dialogue and objective flow by listening to the two events here.
    ///
    /// <para>
    /// Package 12 W9 (M19): a <see cref="BossData.IsSquad"/> boss is fought as
    /// several bodies. The encounter spawns one <see cref="BossController"/> per
    /// member, shows one HUD bar holding their summed HP, hands a fallen member's
    /// abilities to the survivors and advances them a phase
    /// (<see cref="BossPhaseTrigger.MemberDefeat"/>), and raises exactly one defeat
    /// payload — so exactly one boss award — when the last member falls.
    /// </para>
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

        /// <summary>The first (for a squad, member 0) body.</summary>
        public BossController Boss { get; private set; }
        public bool IsRevealed { get; private set; }
        public bool IsDefeated { get; private set; }

        /// <summary>Raised once when the player first comes within reveal range.</summary>
        public event Action BossRevealed;

        /// <summary>Raised once on defeat, after the dust award.</summary>
        public event Action<BossDefeatedPayload> BossDefeated;

        /// <summary>
        /// Raised once per phase the encounter enters, whatever body or trigger
        /// produced it. Level arenas hang their phase mechanics here (the Borgia
        /// scaffolding burn, the Tribunal pit collapse, the Inventor's coils, the
        /// Globe's arena trapdoors).
        /// </summary>
        public event Action<int> PhaseEntered;

        /// <summary>Assigned by the level after StorySceneBootstrapper builds the HUD.</summary>
        public StoryHUD HUD { get; set; }

        /// <summary>
        /// Every body this encounter fights: one for an ordinary boss, one per
        /// member (in member order) for a squad. <see cref="Boss"/> is the first.
        /// </summary>
        public IReadOnlyList<BossController> Members => _members;

        /// <summary>True when this encounter fields a multi-body squad (M19).</summary>
        public bool IsSquadEncounter => Data?.IsSquad == true;

        /// <summary>The highest phase any body has entered (0 before any transition).</summary>
        public int CurrentPhase { get; private set; }

        /// <summary>Squad members that have fallen so far (M19).</summary>
        public int FallenMemberCount { get; private set; }

        /// <summary>Horizontal spacing between squad members at spawn, in pixels.</summary>
        public const float SquadSpawnSpacing = 240f;

        private readonly List<BossController> _members = new();
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

        /// <summary>Instantiates the authored boss scene (one body per squad member) at the encounter anchor.</summary>
        public BossController SpawnBoss() {
            if (Boss != null && IsInstanceValid(Boss)) return Boss;
            PackedScene scene = BossScene ?? GD.Load<PackedScene>(DefaultBossScenePath);
            if (scene == null) {
                GD.PushWarning($"Boss encounter '{Name}' has no boss scene.");
                return null;
            }
            _members.Clear();
            string baseName = string.IsNullOrWhiteSpace(Data?.BossID) ? "Boss" : Data.BossID.ToPascalCase();
            int count = IsSquadEncounter ? Math.Max(1, Data.SquadMemberCount) : 1;
            for (int member = 0; member < count; member++) {
                BossController body = scene.Instantiate<BossController>();
                body.Name = IsSquadEncounter ? $"{baseName}Member{member}" : baseName;
                body.Data = Data;
                body.RewindPolicy = BossRewindPolicy;
                if (IsSquadEncounter) {
                    // M19: each member is its own body on its even share of the
                    // pool; the encounter raises the one defeat payload for them all.
                    body.SquadMemberIndex = member;
                    body.RaisesDefeatPayload = false;
                    float offset = (member - (count - 1) * 0.5f) * SquadSpawnSpacing;
                    body.Position = SpawnOffset + new Vector2(offset, 0f);
                } else {
                    body.Position = SpawnOffset;
                }
                body.PhaseEntered += OnBodyPhaseEntered;
                body.Died += OnBodyDied;
                _members.Add(body);
                AddChild(body);
            }
            Boss = _members[0];
            return Boss;
        }

        /// <summary>Sum of every body's current HP (a squad's one shared bar).</summary>
        public int CombinedCurrentHP {
            get {
                int total = 0;
                foreach (BossController body in _members) {
                    if (body != null && IsInstanceValid(body)) total += Math.Max(0, body.CurrentHP);
                }
                return total;
            }
        }

        /// <summary>Sum of every body's difficulty-scaled maximum (the bar's max).</summary>
        public int CombinedMaxHP {
            get {
                int total = 0;
                foreach (BossController body in _members) {
                    if (body != null && IsInstanceValid(body)) total += Math.Max(1, body.ScaledMaxHP);
                }
                return Math.Max(1, total);
            }
        }

        private void OnBodyPhaseEntered(BossController body, int phase) {
            if (phase <= CurrentPhase) return;
            CurrentPhase = phase;
            PhaseEntered?.Invoke(phase);
        }

        /// <summary>
        /// M19. A member falling hands its abilities to every survivor and, for a
        /// <see cref="BossPhaseTrigger.MemberDefeat"/> squad, advances them into the
        /// next phase; the last one falling defeats the squad and raises the single
        /// defeat payload. An ordinary boss raises its own payload and this is a no-op.
        /// </summary>
        private void OnBodyDied(BossController fallen) {
            if (!IsSquadEncounter || fallen == null) return;
            FallenMemberCount++;
            var survivors = new List<BossController>();
            foreach (BossController body in _members) {
                if (body != null && IsInstanceValid(body) && body != fallen && body.IsAlive) survivors.Add(body);
            }
            if (survivors.Count == 0) {
                EventBus.Instance?.RaiseBossDefeated(new BossDefeatedPayload {
                    BossID = Data?.BossID ?? "",
                    Position = fallen.GlobalPosition,
                    ChronalDustDrop = Data?.ChronalDustDrop ?? 25
                });
                return;
            }
            foreach (BossController survivor in survivors) {
                survivor.AbsorbSquadMember(fallen.SquadMemberIndex);
                if (Data?.PhaseTrigger == BossPhaseTrigger.MemberDefeat) {
                    survivor.EnterPhase(Math.Min(FallenMemberCount, Data.PhaseCount - 1));
                }
            }
            if (IsRevealed && !IsDefeated) HUD?.UpdateBossHP(CombinedCurrentHP);
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
                SetMembersFrozen(true);
                // Package 11 A3 (F01): the name-card ritual holds the world,
                // so it holds the Timeline Integrity clock with it.
                FTT.Core.StoryManager.Instance?.SetIntegrityClockPause(
                    FTT.Core.IntegrityClockPause.BossIntro, true);
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
            FTT.Core.StoryManager.Instance?.SetIntegrityClockPause(
                FTT.Core.IntegrityClockPause.BossIntro, false);
            if (Boss != null && IsInstanceValid(Boss)) {
                SetMembersFrozen(false);
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
            // disagree with the fight. M19: a squad shares one bar holding the sum
            // of its bodies; with even shares the authored 0.5 notch sits exactly
            // one member's pool down.
            HUD?.ShowBossBar(
                string.IsNullOrWhiteSpace(Data?.DisplayNameKey) ? "boss" : Data.DisplayNameKey,
                IsSquadEncounter ? CombinedCurrentHP : Boss?.CurrentHP ?? 0,
                IsSquadEncounter ? CombinedMaxHP : Boss?.ScaledMaxHP ?? 1,
                Data?.PhaseThresholds);
            BossRevealed?.Invoke();
        }

        private void OnBossHPChanged(BossHPPayload payload) {
            if (!IsRevealed || IsDefeated) return;
            if (!string.IsNullOrEmpty(Data?.BossID) && payload.BossID != Data.BossID) return;
            HUD?.UpdateBossHP(IsSquadEncounter ? CombinedCurrentHP : payload.CurrentHP);
        }

        private void OnBossDefeated(BossDefeatedPayload payload) {
            if (IsDefeated) return;
            if (!string.IsNullOrEmpty(Data?.BossID) && payload.BossID != Data.BossID) return;
            // M19: a squad's payload is raised only by OnBodyDied once the last
            // member falls, so this runs exactly once for the whole squad.
            IsDefeated = true;
            HUD?.HideBossBar();
            // V7.3 Single Icon Rule: the award is a physical pickup at the
            // boss's fall position — the wallet is paid (and the boss results
            // line attributed) at collection. Never expires; Large tier.
            // Package 11 A10 (F05): the amount is the level's single 25-dust
            // boss reward, claimed once per attempt against the manifest's boss
            // source ID, so repeated phases of one boss share it. An unledgered
            // context falls back to the authored BossData value.
            if (AwardDustOnDefeat) {
                int reward = FTT.Environment.LevelRewardDirectory.ResolveBossAward(
                    payload.ChronalDustDrop, out string rewardSourceID);
                if (reward > 0) {
                    StoryDropSystem.SpawnDustAward(
                        reward, payload.Position,
                        GetParent() ?? this, DustAwardSource.Boss, rewardSourceID);
                }
            }
            BossDefeated?.Invoke(payload);
        }

        public void SetStoryRewindFrozen(bool frozen) => SetMembersFrozen(frozen);

        private void SetMembersFrozen(bool frozen) {
            foreach (BossController body in _members) {
                if (body != null && IsInstanceValid(body)) body.SetStoryRewindFrozen(frozen);
            }
        }
    }
}
