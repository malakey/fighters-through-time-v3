using System;
using System.Collections.Generic;
using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// F12 Eraser debut route trigger (V7.6, Package 11 A12) — the scripted
    /// single-Eraser ambush that Level 4A places between Entry and PreBoss, at the
    /// hero's own nexus.
    ///
    /// <para>It is an <b>independent authored route trigger</b>, not a checkpoint
    /// benefit: crossing it starts the encounter and its Sarah bark, and it grants
    /// <b>no</b> checkpoint, no Checkpoint Mending, no rewind refill and no Timeline
    /// Integrity lock. Its timing never depends on a middle-checkpoint activation or
    /// on difficulty — Level 4A has no middle checkpoint at all.</para>
    ///
    /// <para>Idempotent reconstruction is the hard part of the contract, so the state
    /// is split three ways and each half is asked a different question:</para>
    /// <list type="bullet">
    ///   <item><see cref="EncounterCleared"/> — encounter state. Set when the ambush
    ///     is defeated or when a PreBoss reconstruction declares the whole approach
    ///     complete. A cleared encounter never respawns.</item>
    ///   <item><see cref="EncounterLive"/> — the live-wave guard. Repeated crossings
    ///     while the ambush is already up spawn nothing, so a player who walks back
    ///     and forth cannot stack duplicate waves.</item>
    ///   <item><see cref="BarkSeen"/> — a <b>presentation</b> flag, deliberately
    ///     separate. A reconstructed ambush must remain fully triggerable even though
    ///     the player has already heard the line once; a persistent "trigger seen"
    ///     flag must never suppress required enemies and strand the gate.</item>
    /// </list>
    ///
    /// <para><b>A7a handoff.</b> The Eraser elite itself (<c>resources/Enemies/eraser.tres</c>,
    /// Null Lance, Siphon Snare, Suppression) ships with A7a in Wave 2. Until then
    /// <see cref="EnemyID"/> defaults to the placeholder elite <c>chrono_guard_elite</c>
    /// and <see cref="SpawnEraserDebut"/> is a real encounter with a placeholder body.
    /// See the TODO on <see cref="EraserEnemyID"/>.</para>
    /// </summary>
    public partial class EraserDebutTrigger : Node2D {

        /// <summary>
        /// TODO(A7a, Wave 2): the authored Eraser elite resource ID. A7a ships
        /// <c>resources/Enemies/eraser.tres</c> with Null Lance + Siphon Snare and the
        /// new <c>StatusType.Suppression</c>; B3 then re-points all nine Level 4A
        /// variants from <see cref="PlaceholderEnemyID"/> to this constant in one
        /// change. Nothing else about this trigger moves.
        /// </summary>
        public const string EraserEnemyID = "eraser";

        /// <summary>The interim body: an authored elite that exists today (recon G §1.3).</summary>
        public const string PlaceholderEnemyID = "chrono_guard_elite";

        /// <summary>Sarah's non-blocking debut bark: "That one isn't guarding anything…".</summary>
        public const string DebutBarkKey = "eraser_debut_bark";

        [Export] public string TriggerID = "";

        /// <summary>Which elite actually spawns. Defaults to the interim placeholder.</summary>
        [Export] public string EnemyID = PlaceholderEnemyID;

        [Export] public Vector2 TriggerSize = new(80f, 420f);

        /// <summary>Where the ambush drops in, relative to the trigger.</summary>
        [Export] public Vector2 SpawnOffset = new(320f, 0f);

        /// <summary>Encounter state: the ambush is defeated (or declared complete on reconstruction).</summary>
        public bool EncounterCleared { get; private set; }

        /// <summary>Live-wave guard: an ambush is up right now.</summary>
        public bool EncounterLive { get; private set; }

        /// <summary>Presentation state only — never gates the encounter.</summary>
        public bool BarkSeen { get; private set; }

        /// <summary>F05: the debut's reward is claimed once per attempt, never re-issued.</summary>
        public bool RewardClaimed { get; private set; }

        /// <summary>How many times the ambush has actually been spawned. Diagnostic/test seam.</summary>
        public int SpawnCount { get; private set; }

        /// <summary>Raised when the ambush begins (presentation and the level's bark hook).</summary>
        public event Action<EraserDebutTrigger> DebutBegan;

        /// <summary>Raised when the ambush is cleared.</summary>
        public event Action<EraserDebutTrigger> DebutCleared;

        private readonly List<Node> _liveEnemies = new();
        private readonly List<EnemyController> _watched = new();
        private Area2D _trigger;

        public override void _Ready() {
            _trigger = new Area2D {
                Name = "RouteTrigger",
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            _trigger.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = TriggerSize }
            });
            // Spawning collision bodies inside the in/out flush is forbidden, so the
            // ambush is built right after it. Direct calls to SpawnEraserDebut (which
            // is how tests drive this) stay fully synchronous.
            _trigger.BodyEntered += body => {
                if (body is not PlayerController) return;
                Callable.From(() => SpawnEraserDebut()).CallDeferred();
            };
            AddChild(_trigger);
        }

        /// <summary>
        /// Begins the ambush if it should begin. Idempotent in both directions: a
        /// cleared encounter never respawns, and a live one is never duplicated.
        /// Returns true only when a wave was actually created.
        ///
        /// <para>Grants nothing — no checkpoint, no Mending, no rewind refill, no
        /// Integrity lock. It spawns an encounter and raises a bark, and that is the
        /// whole of its authority.</para>
        /// </summary>
        public bool SpawnEraserDebut() {
            if (EncounterCleared || EncounterLive) return false;
            if (NexusResonanceSource.WorldTimeSuspendedProbe?.Invoke() == true) return false;

            PruneDeadEnemies();
            EncounterLive = true;
            SpawnCount++;

            // The single-Eraser composition is authored, not scaled: F12 says one
            // Eraser, and StoryDifficultyTuning scales its HP/damage at spawn.
            Node spawned = EnemyFactory.Spawn(EnemyID, this, GlobalPosition + SpawnOffset, null, null);
            if (spawned != null) {
                _liveEnemies.Add(spawned);
                Watch(spawned);
            } else {
                // Headless / no pool: the encounter still counts as begun so the
                // route gate cannot strand, and clears immediately.
                EncounterLive = false;
                EncounterCleared = true;
            }

            BarkSeen = true;
            DebutBegan?.Invoke(this);
            if (EncounterCleared) DebutCleared?.Invoke(this);
            return true;
        }

        private void OnAmbushEnemyGone() {
            PruneDeadEnemies();
            if (_liveEnemies.Count > 0 || !EncounterLive) return;
            EncounterLive = false;
            EncounterCleared = true;
            DebutCleared?.Invoke(this);
        }

        private void PruneDeadEnemies() =>
            _liveEnemies.RemoveAll(node => node == null || !IsInstanceValid(node) || !node.IsInsideTree());

        /// <summary>
        /// Subscribes to the ambush body's exit. Pooled enemies are recycled, so the
        /// subscription is tracked and released in <see cref="Unwatch"/> — otherwise an
        /// unrelated later release of that same pooled instance would keep calling back
        /// into this trigger.
        /// </summary>
        private void Watch(Node enemy) {
            if (enemy is not EnemyController controller || _watched.Contains(controller)) return;
            controller.TreeExited += OnAmbushEnemyGone;
            _watched.Add(controller);
        }

        private void Unwatch() {
            foreach (EnemyController controller in _watched) {
                if (IsInstanceValid(controller)) controller.TreeExited -= OnAmbushEnemyGone;
            }
            _watched.Clear();
        }

        public override void _ExitTree() => Unwatch();

        /// <summary>
        /// Reconstruction after a checkpoint reload. Before PreBoss the ambush is
        /// rebuilt (<paramref name="approachComplete"/> false) and stays triggerable;
        /// after PreBoss the saved baseline treats every required approach encounter
        /// as complete. A claimed reward stays claimed either way.
        /// </summary>
        public void RestoreFromCheckpoint(bool approachComplete, bool rewardAlreadyClaimed) {
            Unwatch();
            EncounterLive = false;
            _liveEnemies.Clear();
            EncounterCleared = approachComplete;
            RewardClaimed = rewardAlreadyClaimed || (approachComplete && RewardClaimed);
            // BarkSeen is deliberately untouched: presentation history is not
            // encounter state, in either direction.
        }

        /// <summary>
        /// Claims the debut's share of the level's required-encounter dust, once.
        /// Returns false when it was already claimed — the "already-collected rewards
        /// remain claimed" half of the reconstruction contract.
        /// </summary>
        public bool TryClaimReward() {
            if (RewardClaimed) return false;
            RewardClaimed = true;
            return true;
        }

        /// <summary>Test seam: mark the ambush defeated without killing a pooled body.</summary>
        internal void MarkClearedForTest() {
            Unwatch();
            _liveEnemies.Clear();
            if (!EncounterLive && EncounterCleared) return;
            EncounterLive = false;
            EncounterCleared = true;
            DebutCleared?.Invoke(this);
        }
    }
}
