using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Shared Story level scaffolding for campaign levels 2-15 (Package 5 A1).
    /// Extracted from the proven <see cref="Level01Controller"/> flow: graybox
    /// geometry builders, the "LevelManager" child, player spawn with a
    /// <see cref="StoryCameraConfiner"/>, room transitions, checkpoint resume,
    /// dust tally, boss wiring, extractor placement, and the completion chain.
    ///
    /// Florence (level 1) and the Tutorial (level 0) deliberately stay on their
    /// own controllers: this base is for new levels only.
    ///
    /// Lifecycle a subclass sees, in order:
    /// <list type="number">
    /// <item>the "LevelManager" child is created and configured</item>
    /// <item><see cref="BuildLevel"/> - geometry, checkpoints, rooms, boss anchors</item>
    /// <item>the player spawns, checkpoint resume runs (may call
    ///       <see cref="MarkWavesClearedThrough"/>), the camera is created and
    ///       linked into every room trigger</item>
    /// <item><see cref="SpawnInitialEnemies"/></item>
    /// <item>shared Story services attach, title/objective post, boss HUD wires up</item>
    /// <item><see cref="OnLevelReady"/></item>
    /// <item>the entrance dialogue fires unless this is a mid-level resume</item>
    /// </list>
    /// </summary>
    public abstract partial class StoryLevelControllerBase : Node2D {

        // === Subclass contract ===

        /// <summary>Manifest content id, e.g. "level_02_orleans". Also the LevelManager LevelID.</summary>
        public abstract string LevelID { get; }

        /// <summary>Campaign slot this scene occupies; drives the checkpoint-resume scene-path guard.</summary>
        public abstract CampaignLevel Level { get; }

        /// <summary>Translation key for the HUD level title, e.g. "orleans_level_title".</summary>
        public abstract string LevelTitleKey { get; }

        /// <summary>Dialogue set resource, e.g. "res://resources/Dialogue/level_02_dialogue.tres".</summary>
        public abstract string DialogueSetPath { get; }

        /// <summary>Where the player stands with no saved checkpoint.</summary>
        public abstract Vector2 PlayerSpawnPosition { get; }

        /// <summary>Whole-level camera limits; individual rooms narrow this through room triggers.</summary>
        public abstract Rect2 LevelBounds { get; }

        /// <summary>
        /// Dialogue sequence prefix. Defaults to the "level_NN" slice of
        /// <see cref="LevelID"/>, matching the plan's `level_NN.entrance` convention.
        /// </summary>
        public virtual string DialoguePrefix {
            get {
                string id = LevelID ?? "";
                string[] parts = id.Split('_');
                return parts.Length >= 2 ? $"{parts[0]}_{parts[1]}" : id;
            }
        }

        public virtual string EntranceDialogueID => $"{DialoguePrefix}.entrance";
        public virtual string BossIntroDialogueID => $"{DialoguePrefix}.boss_intro";
        public virtual string ExitDialogueID => $"{DialoguePrefix}.exit";

        /// <summary>
        /// Authored <see cref="StageAudioSet"/> for this level (Package 8 B5).
        /// Derived from <see cref="LevelID"/> so a new level inherits its music with
        /// no extra authoring; override only to point somewhere non-standard.
        /// </summary>
        protected virtual string AudioSetPath => AudioSetPaths.ForStoryLevel(LevelID);

        /// <summary>Objective posted as soon as the HUD exists. Empty means "post nothing".</summary>
        protected virtual string InitialObjectiveKey => "";

        /// <summary>Objective posted when the boss encounter reveals.</summary>
        protected virtual string BossObjectiveKey => "";

        /// <summary>Objective posted once the boss is down.</summary>
        protected virtual string CompletionObjectiveKey => "";

        /// <summary>Delay between the boss dying and the exit dialogue starting.</summary>
        protected virtual float ExitDialogueDelaySeconds => 1.5f;

        // === Timeline Integrity, the level timer (V7.6 F01, Package 11 A3) ===

        /// <summary>
        /// The level's authored par, in seconds: the reference time the F01
        /// Integrity clock normalizes against, so a par run ends at 50% /
        /// 33.33% / 16.67% on Easy / Normal / Hard.
        ///
        /// <b>These are PROVISIONAL values, not measured ones.</b> V01a requires
        /// a per-hero median Normal required-route measurement that has not
        /// happened; every shipped par is seeded as
        /// <c>authored room count x 90 s</c>, rounded up to the nearest 30 s,
        /// and is recorded in <c>docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md</c>
        /// under <c>VERIFY-PAR-SECONDS</c>. Do not present them as measured.
        ///
        /// 0 means the level is untimed and the clock never arms. The Tutorial
        /// and Florence do not extend this base at all, which is how they opt
        /// out of the clock and the Collapse Tremor without a special case.
        /// </summary>
        public virtual float ParSeconds => 0f;

        /// <summary>
        /// Provisional F11 remaining-route budget for an anchor, as a fraction
        /// of <see cref="ParSeconds"/>: the conservative live time from that
        /// anchor to the pre-boss clock lock.
        ///
        /// Entry is 0.90 rather than 1.00 because par is a full-run median that
        /// includes optional detours, while F11 measures the MANDATORY route
        /// only. The PreBoss anchor has no remaining timed route at all, so its
        /// budget falls to the 25-point floor.
        ///
        /// Also provisional, also recorded under <c>VERIFY-PAR-SECONDS</c>.
        /// Override per level once real measurements exist.
        /// </summary>
        protected virtual float RemainingRouteFractionFor(CheckpointRole role) => role switch {
            CheckpointRole.Entry => 0.90f,
            CheckpointRole.Middle => 0.45f,
            _ => 0f
        };

        /// <summary>The scene's Collapse Tremor, on timed levels. Null on untimed ones.</summary>
        public CollapseTremorController Tremor { get; private set; }

        /// <summary>
        /// Authored dialogue beats that run BETWEEN the boss dying and the ordinary
        /// exit beat, in order. Empty (the default) hands defeat straight to
        /// <see cref="StartExitSequence"/>, which is what levels 2-7 and 9-12 do.
        ///
        /// A level that owes an extra scene here (Level 8's Cleopatra
        /// <c>level_08.postboss</c>) lists it instead of overriding
        /// <see cref="OnBossDefeated"/>: overriding the defeat handler wholesale
        /// loses the base's bookkeeping, so <see cref="IsBossDefeated"/> would never
        /// become true and the level would have to track its own flag.
        ///
        /// Each beat waits <see cref="ExitDialogueDelaySeconds"/> first, and a beat
        /// whose sequence will not start is skipped rather than stranding the player
        /// in a finished arena with no results overlay.
        /// </summary>
        protected virtual IReadOnlyList<string> PostBossDialogueIDs => NoPostBossBeats;

        private static readonly string[] NoPostBossBeats = Array.Empty<string>();

        /// <summary>Build graybox geometry, checkpoints, room triggers, puzzles, and boss anchors.</summary>
        protected abstract void BuildLevel();

        /// <summary>Enemies present the moment the level opens (later waves use wave triggers).</summary>
        protected virtual void SpawnInitialEnemies() { }

        /// <summary>
        /// Resume hook. Called once during spawn when the active save restores a
        /// checkpoint in this level; mark every wave ahead of that checkpoint as
        /// already fought so a resumed run does not replay cleared encounters.
        /// </summary>
        protected virtual void MarkWavesClearedThrough(string checkpointID) { }

        /// <summary>Runs after services attach and before the entrance dialogue.</summary>
        protected virtual void OnLevelReady() { }

        /// <summary>Any completed dialogue that is not the exit sequence.</summary>
        protected virtual void OnDialogueSequenceComplete(string dialogueID) { }

        // === Runtime state available to subclasses ===

        public PlayerController Player { get; private set; }
        public LevelManager Levels { get; private set; }
        public StorySceneServices Services { get; private set; }
        public StoryHUD HUD => Services?.HUD;
        public StoryCameraConfiner Camera { get; private set; }

        /// <summary>Scene music and environment cues (Package 8 B5).</summary>
        public StoryAudioDirector Audio => Services?.Audio;

        /// <summary>
        /// Dust the wallet actually received while this level was live (the results
        /// total). Tallied from <c>OnChronalDustCollected</c> — the same event
        /// <see cref="StoryManager"/> banks — so this number can never disagree with
        /// what the player was paid (audit H-1/M-1).
        /// </summary>
        public int DustEarnedThisLevel { get; private set; }

        /// <summary>Boss-defeat portion of <see cref="DustEarnedThisLevel"/> (results itemization).</summary>
        public int BossDustEarned { get; private set; }

        /// <summary>Extractor-destruction portion of <see cref="DustEarnedThisLevel"/>.</summary>
        public int ExtractorDustEarned { get; private set; }

        /// <summary>
        /// Package 11 A10 (F05): the secret / other-optional portion — the half
        /// of a level's optional pool that is not a machine row.
        /// </summary>
        public int OptionalDustEarned { get; private set; }

        /// <summary>
        /// Required-encounter remainder of the total: every wallet award that was
        /// not a boss, extractor or optional payout — pickup collections from
        /// authored mandatory kills, plus rare chest finds. Derived rather than
        /// counted so the lines always sum to the exact wallet total even if an
        /// award lands out of order.
        /// </summary>
        public int MobDustEarned => Mathf.Max(
            0, DustEarnedThisLevel - BossDustEarned - ExtractorDustEarned - OptionalDustEarned);

        /// <summary>True when the run resumed at a saved checkpoint rather than the entrance.</summary>
        public bool ResumedMidLevel { get; private set; }

        /// <summary>Checkpoint the resume restored, or "" for a fresh entry.</summary>
        public string ResumedCheckpointID { get; private set; } = "";

        public bool LevelComplete { get; private set; }

        private readonly List<RoomTransitionTrigger> _roomTriggers = new();
        private readonly List<BossEncounterController> _bossEncounters = new();
        private readonly List<ChronalExtractor> _extractors = new();

        public IReadOnlyList<RoomTransitionTrigger> RoomTriggers => _roomTriggers;
        public IReadOnlyList<BossEncounterController> BossEncounters => _bossEncounters;
        public IReadOnlyList<ChronalExtractor> Extractors => _extractors;

        private bool _eventsBound;

        // === Default graybox palette (subclasses override per era) ===

        protected virtual Color FloorColor => new(0.22f, 0.22f, 0.26f);
        protected virtual Color FloorEdgeColor => new(0.42f, 0.42f, 0.5f);
        protected virtual Color PlatformColor => new(0.3f, 0.3f, 0.36f);
        protected virtual Color WallColor => new(0.16f, 0.16f, 0.2f);
        protected virtual Color HazardColor => new(0.6f, 0.2f, 0.1f);

        protected const float DefaultFloorThickness = 40f;
        protected const float DefaultPlatformThickness = 16f;
        protected const float DefaultWallThickness = 20f;

        // === Lifecycle ===

        public override void _Ready() {
            CreateLevelManager();
            // Package 12 W8 (N01): a pre-seal load must be known before the
            // level builds, so the boss encounter is authored defeated rather
            // than spawned and fought again.
            DetectPreSealLoad();
            BuildLevel();
            // Package 12 W8: base-owned content hooks every level (and every
            // sealed 4A BuildLevel) gets — the F05 secret cache and the N01
            // sealing anchor, both at per-level authored positions.
            BuildAuthoredSecretCache();
            BuildGenericSealingAnchor();
            SpawnPlayer();
            SpawnInitialEnemies();
            ApplyResumedAttemptState();
            // V7.6 F01: arm the level clock AFTER the world is built (so the
            // authored Extractor population is known) and after the resumed
            // attempt is applied (so a resume's already-broken machines are
            // already out of the living count). The F01 DENOMINATOR is fixed
            // from the authored starting population, never from survivors.
            ArmIntegrityClock();
            AttachStoryServices();
            BindEvents();
            ApplyResumeCameraBounds();
            OnLevelReady();
            if (IsRestoringAwaitingSeal) RestoreAwaitingSeal();
            // Deferred through a Callable rather than MethodName: the base is
            // abstract and its subclasses may be constructed in code, so the
            // dispatch must not depend on a bound script method name.
            if (!ResumedMidLevel) Callable.From(StartEntranceDialogue).CallDeferred();
        }

        public override void _ExitTree() {
            UnbindEvents();
            ReleasePooledContent();
        }

        /// <summary>
        /// Hands every pooled object this level owns back to <see cref="PoolManager"/>.
        /// Pooled enemies, their projectiles, and loot are parented under the level, so
        /// unloading the scene would destroy them while the pool still holds them in its
        /// active list - the next level's spawns then inherit freed references and every
        /// pool sweep throws. Scoped by ancestry rather than by the "Enemies" group so a
        /// level tearing down can never reclaim another live scene's objects (only one
        /// level is live at a time today, but the hub and the test fixtures are not).
        /// </summary>
        private void ReleasePooledContent() => PoolManager.Instance?.ReleaseActiveUnder(this);

        /// <summary>
        /// Arms the F01 Integrity clock and, on a timed level, attaches the
        /// Collapse Tremor.
        ///
        /// The Tremor is attached HERE rather than in
        /// <c>StorySceneBootstrapper</c> (which A2 owns this wave) precisely
        /// because the untimed Tutorial and Florence do not extend this base —
        /// so opting them out costs no special case at all. The hub, which
        /// shares the bootstrapper, is likewise untouched.
        /// </summary>
        private void ArmIntegrityClock() {
            StoryManager story = StoryManager.Instance;
            if (story == null) return;
            story.BeginIntegrityClock(ParSeconds, _extractors.Count);
            if (ParSeconds <= 0f) return;
            Tremor = new CollapseTremorController { Name = "CollapseTremorController" };
            AddChild(Tremor);
        }

        private void CreateLevelManager() {
            Levels = new LevelManager {
                Name = "LevelManager",
                LevelID = LevelID,
                LevelDisplayName = LevelTitleKey
            };
            AddChild(Levels);
        }

        private void AttachStoryServices() {
            Services = StorySceneBootstrapper.Attach(this, DialogueSetPath, audioSetPath: AudioSetPath);
            Services.HUD?.SetLevelTitle(LevelTitleKey);
            if (!string.IsNullOrWhiteSpace(InitialObjectiveKey)) {
                Services.HUD?.SetObjective(InitialObjectiveKey);
            }
            foreach (BossEncounterController encounter in _bossEncounters) {
                if (IsInstanceValid(encounter)) encounter.HUD = Services.HUD;
            }
        }

        private void BindEvents() {
            if (_eventsBound || EventBus.Instance == null) return;
            _eventsBound = true;
            EventBus.Instance.OnChronalDustCollected += OnDustAwarded;
            EventBus.Instance.OnDustAwardCollected += OnDustAwardAttributed;
            EventBus.Instance.OnDialogueComplete += HandleDialogueComplete;
            EventBus.Instance.OnRewindTriggered += OnRewindLanded;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnChronalDustCollected -= OnDustAwarded;
            EventBus.Instance.OnDustAwardCollected -= OnDustAwardAttributed;
            EventBus.Instance.OnDialogueComplete -= HandleDialogueComplete;
            EventBus.Instance.OnRewindTriggered -= OnRewindLanded;
        }

        /// <summary>
        /// A 15 s rewind routinely lands in an earlier room, whose forward-only
        /// transition trigger will not re-fire; without this the camera stays
        /// confined to the room the player died in and the landing is
        /// off-screen. Mirrors <see cref="ApplyResumeCameraBounds"/>.
        /// </summary>
        private void OnRewindLanded(Vector2 landingPosition) {
            if (Camera == null) return;
            RoomTransitionTrigger room = FindRoomTriggerContaining(landingPosition.X);
            if (room != null) Camera.SetBounds(room.CameraBounds);
        }

        private void StartEntranceDialogue() {
            // Callable.From(...) wraps a bare managed delegate, so - unlike
            // CallDeferred(MethodName.X) - the engine does NOT drop the queued call
            // when the target node is freed. A level torn down in the same frame it
            // readied (fast scene change, a test fixture) would otherwise run this
            // against a disposed node and its disposed DialogueManager.
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            if (!string.IsNullOrWhiteSpace(EntranceDialogueID)) {
                Services?.Dialogue?.StartSequence(EntranceDialogueID);
            }
        }

        private void HandleDialogueComplete(string dialogueID) {
            if (dialogueID == ExitDialogueID) {
                ShowCompletionResults();
                return;
            }
            // Read before the hook runs: a subclass is free to change what the
            // chain looks like from inside OnDialogueSequenceComplete.
            bool finishedPostBossBeat = IsActivePostBossBeat(dialogueID);
            OnDialogueSequenceComplete(dialogueID);
            if (finishedPostBossBeat) AdvancePostBossChain();
        }

        /// <summary>
        /// Wallet-receipt tally (audit H-1). The physical <see cref="ChronalDustPickup"/>
        /// is the single awarding path for kill dust — <see cref="StoryDropSystem"/>
        /// spawns it and collection raises the award — so the level controller must
        /// never re-raise a kill's drop; it only listens to what the wallet was paid.
        /// Boss and extractor awards flow through the same event (raised once by
        /// <see cref="BossEncounterController"/> / <see cref="ChronalExtractor"/>),
        /// so the total needs no per-source special cases.
        /// </summary>
        private void OnDustAwarded(int amount) => DustEarnedThisLevel += Mathf.Max(0, amount);

        /// <summary>
        /// V7.3 Single Icon Rule: boss/extractor awards are physical pickups,
        /// so attribution now lands at COLLECTION time via this payload event —
        /// the same moment the wallet event above banks the amount. The two can
        /// therefore never disagree, and the double-pay invariant (audit H-1)
        /// holds by construction: one wallet raise, one attribution, both at
        /// the collection site.
        /// </summary>
        private void OnDustAwardAttributed(DustAwardCollectedPayload payload) {
            switch (payload.Source) {
                case DustAwardSource.Boss:
                    AttributeBossDust(payload.Amount);
                    NoteBossPickupCollected();
                    break;
                case DustAwardSource.Extractor: AttributeExtractorDust(payload.Amount); break;
                // Package 11 A10 (F05): the sixth results category.
                case DustAwardSource.Secret: AttributeOptionalDust(payload.Amount); break;
            }
        }

        /// <summary>
        /// Attributes an already-awarded amount to the boss line of the results
        /// itemization. Attribution only — the wallet award entered
        /// <see cref="DustEarnedThisLevel"/> through the shared
        /// <c>OnChronalDustCollected</c> subscription; adding it to the total here
        /// as well would recreate the H-1 double-pay on the results screen. Kept
        /// protected for callers outside the pickup path (Level 13's Mirror
        /// Paradox pays wallet-direct and attributes explicitly).
        /// </summary>
        protected void AttributeBossDust(int amount) => BossDustEarned += Mathf.Max(0, amount);

        /// <summary>Extractor sibling of <see cref="AttributeBossDust"/>.</summary>
        protected void AttributeExtractorDust(int amount) => ExtractorDustEarned += Mathf.Max(0, amount);

        /// <summary>Secret / other-optional sibling of <see cref="AttributeBossDust"/> (Package 11 A10).</summary>
        protected void AttributeOptionalDust(int amount) => OptionalDustEarned += Mathf.Max(0, amount);

        // === Player, camera, and checkpoint resume ===

        private void SpawnPlayer() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";

            Player = CharacterFactory.CreateCharacter(characterID, 0);
            Player.Name = "Player";
            Player.Position = PlayerSpawnPosition;
            AddChild(Player);
            RestoreSavedCheckpoint();

            Camera = new StoryCameraConfiner {
                Name = "PlayerCamera",
                Enabled = true,
                ActiveBounds = LevelBounds,
                PositionSmoothingEnabled = true,
                PositionSmoothingSpeed = 5.0f
            };
            Player.AddChild(Camera);
            LinkRoomCameras();
        }

        /// <summary>
        /// Room triggers are built before the player exists, so their camera path
        /// is resolved here once the confiner is in the tree.
        /// </summary>
        private void LinkRoomCameras() {
            if (Camera == null) return;
            foreach (RoomTransitionTrigger trigger in _roomTriggers) {
                if (!IsInstanceValid(trigger)) continue;
                if (trigger.CameraPath == null || trigger.CameraPath.IsEmpty) {
                    trigger.CameraPath = trigger.GetPathTo(Camera);
                }
            }
        }

        /// <summary>
        /// Confines the camera to the authored room the run actually starts in.
        ///
        /// <see cref="RoomTransitionTrigger"/> only fires on a crossing, so a run
        /// resumed at checkpoint 1 or 2 stands *behind* every trigger it already
        /// passed and would keep whole-level limits for the rest of the level. Runs
        /// automatically between <see cref="BindEvents"/> and <see cref="OnLevelReady"/>.
        ///
        /// Camera bounds only, deliberately: calling
        /// <see cref="RoomTransitionTrigger.ActivateRoom"/> here would re-run the
        /// room's <c>onEntered</c> handler and replay its waves, floods, and hazard
        /// state on top of the checkpoint restore. A level that also needs the room's
        /// encounter root enabled on resume calls
        /// <see cref="FindRoomTriggerContaining"/> itself.
        ///
        /// Overlapping room bounds resolve to the last authored match.
        /// </summary>
        protected virtual void ApplyResumeCameraBounds() {
            if (!ResumedMidLevel || Camera == null || Player == null) return;
            float x = Player.Position.X;
            foreach (RoomTransitionTrigger trigger in _roomTriggers) {
                if (!IsInstanceValid(trigger)) continue;
                if (ContainsX(trigger.CameraBounds, x)) Camera.SetBounds(trigger.CameraBounds);
            }
        }

        /// <summary>First authored room trigger whose camera bounds span <paramref name="positionX"/>.</summary>
        protected RoomTransitionTrigger FindRoomTriggerContaining(float positionX) {
            foreach (RoomTransitionTrigger trigger in _roomTriggers) {
                if (IsInstanceValid(trigger) && ContainsX(trigger.CameraBounds, positionX)) return trigger;
            }
            return null;
        }

        private static bool ContainsX(Rect2 bounds, float x) => x >= bounds.Position.X && x <= bounds.End.X;

        /// <summary>
        /// Generic Florence-pattern resume: only restores when the active save is
        /// parked on THIS level's scene path, then hands the checkpoint id to
        /// <see cref="MarkWavesClearedThrough"/> so cleared waves stay cleared.
        /// </summary>
        protected void RestoreSavedCheckpoint() {
            if (Player == null || SaveManager.Instance == null || GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null) return;
            if (save.CurrentLevelID != StoryManager.GetLevelScenePath(Level)) return;

            Vector2 position = Player.Position;
            if (Levels != null && Levels.TryGetCheckpointPosition(save.LastCheckpointID, out Vector2 checkpoint)) {
                position = checkpoint;
                Levels.SetCheckpointPosition(save.LastCheckpointID, checkpoint);
                ResumedMidLevel = true;
                ResumedCheckpointID = save.LastCheckpointID;
                // Package 12 W2 (GAP-13): the authored encounter baseline is the
                // reconstruction input; MarkWavesClearedThrough keeps only the
                // anchor's non-encounter world state (gates, floods, escapes).
                ApplyResumedEncounterBaseline(save.LastCheckpointID);
                MarkWavesClearedThrough(save.LastCheckpointID);
            }
            // === Package 11 A3b region: F10 durable restore + T01a cleanup ===
            //
            // F10 supersedes V7.3's "restore the HP and meter the checkpoint
            // banked": ordinary loading restores the LATEST DURABLE values. Those
            // are exactly what save.CurrentHP / CurrentUltimateMeter now hold,
            // because StoryManager snapshots the live player once per live second
            // and folds the same capture into every critical event — before
            // Package 11 nothing between two fractures was durable at all.
            //
            // T01a checkpoint-load semantics: the hero's temporary buffs,
            // shields, debuffs, marks, attachments, grabs, attack phases and
            // movement history are discarded. That is mostly structural here —
            // reconstruction builds a brand-new PlayerController through
            // CharacterFactory, so there is nothing stale to carry — but the
            // status clear is stated rather than assumed, because "it happens to
            // be empty" is not a contract.
            //
            // The one value change is Rally: the pool is discarded WITHOUT
            // granting HP, and the still-uncredited damage-taken meter is settled
            // exactly ONCE, capped at the ordinary meter cap, in the same
            // transaction as the reconstruction. Committing the settlement
            // against the attempt revision is what stops a repeated load from
            // paying the same damage record twice.
            float settledMeter = RestoreDurableResources(Player, save, ResumedMidLevel);
            Player.ClearAllStatusEffects();
            Player.RestoreStoryCheckpoint(position, save.CurrentHP, settledMeter);
            // === end Package 11 A3b region ===
        }

        // === Package 12 W2 region: F10 durable resource restore (GAP-01) =====

        /// <summary>
        /// The one ordinary-load resource restore, shared by every campaign
        /// controller (Florence, which predates this base, calls it too).
        ///
        /// <para>On a mid-level resume it puts the hero's <b>actual</b> block
        /// charges, regen progress and shatter lockout, ability and Echo Step
        /// cooldowns and the D02d Wardenclyffe delay back on the freshly built
        /// player — nothing is refilled. The Rally pool is discarded (T01a) and
        /// its still-uncredited damage-taken meter is settled <b>exactly once</b>
        /// into the returned meter, capped at the ordinary meter cap. The
        /// settlement is written into the attempt record AND the save's meter in
        /// the same step, so whichever durable write lands next persists the
        /// settled pair; a crash before that write reloads the unsettled pair
        /// and settles it once again from disk — never twice against one
        /// record.</para>
        ///
        /// <para>A fresh entry restores nothing: its attempt was just minted, and
        /// a stale record from another attempt must never deplete a new hero.</para>
        /// </summary>
        /// <returns>The meter value the hero should be restored with.</returns>
        public static float RestoreDurableResources(PlayerController player, StorySaveData save, bool resumedMidLevel) {
            float settledMeter = save?.CurrentUltimateMeter ?? 0f;
            StoryManager story = StoryManager.Instance;
            if (story == null || save == null) return settledMeter;
            StoryPlayerResourceTimers timers = story.CurrentAttempt.PlayerResourceTimers;
            if (timers.RallyUncreditedMeter > 0f) {
                settledMeter = Mathf.Min(FTT.Combat.UltimateMeter.MaxValue, settledMeter + timers.RallyUncreditedMeter);
                timers.RallyUncreditedMeter = 0f;
                save.CurrentUltimateMeter = settledMeter;
                story.CurrentAttempt.Bump();
            }
            if (resumedMidLevel && player != null) player.RestoreStoryResourceTimers(timers);
            return settledMeter;
        }

        // === end Package 12 W2 region (durable resource restore) =============

        // === Package 12 W2 region: explicit encounter baselines (GAP-13) =====

        /// <summary>
        /// The current layout version of every explicit encounter-baseline map.
        /// Package 11's derived <c>{LevelID}_wave_N</c> IDs were version 1; a
        /// persisted checkpoint record carrying an older version is ignored in
        /// favour of the level's current map.
        /// </summary>
        public const int ExplicitEncounterBaselineVersion = 2;

        /// <summary>
        /// The level's explicit checkpoint → cleared-encounter map (F10:
        /// "rebuild ordinary encounters from an authored checkpoint baseline"
        /// with membership bound to <b>stable encounter IDs</b>). Every campaign
        /// controller overrides this; <c>null</c> keeps the Package 11 index
        /// derivation for synthetic harness levels only. Keys are stable
        /// checkpoint IDs, values the encounters that anchor restores as already
        /// fought. An anchor absent from the map clears nothing.
        /// </summary>
        protected virtual IReadOnlyDictionary<string, string[]> EncounterBaselineMap => null;

        /// <summary>Read-only view of <see cref="EncounterBaselineMap"/> for content validation.</summary>
        public IReadOnlyDictionary<string, string[]> AuthoredEncounterBaselines => EncounterBaselineMap;

        /// <summary>The layout version this level's baselines are registered with.</summary>
        protected virtual int EncounterBaselineVersion =>
            EncounterBaselineMap != null ? ExplicitEncounterBaselineVersion : 1;

        /// <summary>The baseline the last resume actually applied. Test seam and diagnostics.</summary>
        public IReadOnlyList<string> AppliedEncounterBaseline { get; private set; } = System.Array.Empty<string>();

        /// <summary>
        /// Marks one stable encounter as already fought. Campaign controllers map
        /// each of their encounter IDs onto the wave latch that suppresses its
        /// spawn; unknown IDs are ignored.
        /// </summary>
        protected virtual void MarkEncounterCleared(string encounterID) { }

        /// <summary>
        /// The resume-path reader. Prefers the baseline the attempt committed at
        /// activation — the persisted <see cref="StoryCheckpointRecord"/>, when
        /// it names this anchor and was written against the current layout
        /// version — and otherwise reads the level's registered map. Either way
        /// the IDs are authored data; nothing is inferred from where an enemy
        /// happened to be standing.
        /// </summary>
        protected void ApplyResumedEncounterBaseline(string checkpointID) {
            IReadOnlyList<string> baseline = ResolveResumeEncounterBaseline(checkpointID);
            foreach (string encounterID in baseline) MarkEncounterCleared(encounterID);
            AppliedEncounterBaseline = baseline;
        }

        /// <summary>The stable ID of a numbered wave: <c>{levelID}_wave_{n}</c>.</summary>
        public static string WaveEncounterID(string levelID, int wave) => $"{levelID}_wave_{wave}";

        /// <summary>Inverse of <see cref="WaveEncounterID"/> for this level; false for any other ID.</summary>
        protected static bool TryParseWaveEncounter(string levelID, string encounterID, out int wave) {
            wave = 0;
            string prefix = $"{levelID}_wave_";
            return encounterID != null
                && encounterID.StartsWith(prefix, System.StringComparison.Ordinal)
                && int.TryParse(encounterID.Substring(prefix.Length), out wave)
                && wave > 0;
        }

        /// <summary>
        /// The common numbered-wave map: the entry anchor clears nothing, the
        /// middle anchor clears <paramref name="middleWaves"/>, the PreBoss anchor
        /// clears <paramref name="preBossWaves"/>. Authored per level — the wave
        /// numbers are explicit arguments, never derived from anchor order.
        /// </summary>
        protected static IReadOnlyDictionary<string, string[]> NumberedWaveBaselines(
            string levelID, int[] middleWaves, int[] preBossWaves) {
            string[] Ids(int[] waves) {
                var ids = new string[waves.Length];
                for (int index = 0; index < waves.Length; index++) ids[index] = WaveEncounterID(levelID, waves[index]);
                return ids;
            }
            return new Dictionary<string, string[]> {
                [$"{levelID}_checkpoint_0"] = System.Array.Empty<string>(),
                [$"{levelID}_checkpoint_1"] = Ids(middleWaves),
                [$"{levelID}_checkpoint_2"] = Ids(preBossWaves)
            };
        }

        /// <summary>Pure half of <see cref="ApplyResumedEncounterBaseline"/>.</summary>
        public IReadOnlyList<string> ResolveResumeEncounterBaseline(string checkpointID) {
            if (string.IsNullOrWhiteSpace(checkpointID)) return System.Array.Empty<string>();
            StoryManager story = StoryManager.Instance;
            StoryCheckpointRecord record = story?.CurrentAttempt?.CheckpointRecord;
            if (record != null
                && string.Equals(record.AnchorID, checkpointID, System.StringComparison.Ordinal)
                && record.BaselineVersion == EncounterBaselineVersion
                && record.BaselineEncounterIDs != null) {
                return new List<string>(record.BaselineEncounterIDs);
            }
            if (story != null && story.HasCheckpointRole(checkpointID)) {
                return new List<string>(story.GetEncounterBaseline(checkpointID));
            }
            return EncounterBaselineMap != null
                && EncounterBaselineMap.TryGetValue(checkpointID, out string[] authored)
                && authored != null
                    ? new List<string>(authored)
                    : System.Array.Empty<string>();
        }

        // === end Package 12 W2 region (encounter baselines) ==================

        /// <summary>
        /// V7.3 mid-level resume: rebuilds the attempt's world-state on top of
        /// the freshly constructed scene — extractors already destroyed this
        /// attempt come back broken (no dust, no Integrity restore, no
        /// re-registration) and secrets already found stay found (no double
        /// count). StoryManager's registries were restored from the save by
        /// <see cref="StoryManager.BeginLevelRun"/> before the scene loaded.
        /// </summary>
        protected virtual void ApplyResumedAttemptState() {
            StoryManager story = StoryManager.Instance;
            if (!ResumedMidLevel || story == null) return;
            foreach (ChronalExtractor extractor in _extractors) {
                if (IsInstanceValid(extractor) && story.IsExtractorDestroyed(extractor.ObjectID)) {
                    extractor.RestoreDestroyedState();
                }
            }
            Godot.Collections.Array<Node> caches = GetTree().GetNodesInGroup(SecretCache.Group);
            using var cachesLifetime = caches.AsDisposable();
            foreach (Node node in caches) {
                if (node is SecretCache cache && story.IsSecretFound(cache.SecretID)) {
                    cache.MarkAlreadyFound();
                    // Package 12 W8 (GAP-03): found but never collected — the
                    // reload discarded the pickup, so its source re-issues once.
                    cache.RestorePendingAward();
                }
            }
        }

        // === HUD helpers ===

        protected void SetLevelTitle(string translationKey) => HUD?.SetLevelTitle(translationKey);

        protected void SetObjective(string translationKey, params object[] arguments) {
            if (string.IsNullOrWhiteSpace(translationKey)) return;
            HUD?.SetObjective(translationKey, arguments);
        }

        /// <summary>
        /// Starts an authored sequence, reporting whether it actually began. Virtual
        /// so a level (or a test) can substitute the dialogue source without
        /// reimplementing the beat chains that call it.
        /// </summary>
        protected virtual bool StartDialogue(string dialogueID) =>
            !string.IsNullOrWhiteSpace(dialogueID) &&
            Services?.Dialogue?.StartSequence(dialogueID) == true;

        // === Geometry builders (graybox; Package 2 TileMap conversion is separate) ===

        /// <summary>
        /// Optional per-level surface textures (Package 10). Null keeps the flat
        /// ColorRect graybox visuals; see <see cref="LevelSurfaceTextures"/>.
        /// </summary>
        protected virtual LevelSurfaceTextures SurfaceTextures => null;

        /// <summary>
        /// The "Visual" child every surface builder attaches: a nine-patch when the
        /// level supplies a texture for that surface and the caller did not force an
        /// explicit fill colour, the original ColorRect otherwise.
        /// </summary>
        private static Control BuildSurfaceVisual(LevelSurfaceTextures.Entry entry, bool hasExplicitFill,
            Vector2 size, Vector2 position, Color fallback) {
            if (entry?.Texture == null || hasExplicitFill) {
                return new ColorRect {
                    Name = "Visual",
                    Size = size,
                    Position = position,
                    Color = fallback,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
            }
            return new NinePatchRect {
                Name = "Visual",
                Size = size,
                Position = position,
                Texture = entry.Texture,
                PatchMarginLeft = entry.MarginLeft,
                PatchMarginRight = entry.MarginRight,
                PatchMarginTop = entry.MarginTop,
                PatchMarginBottom = entry.MarginBottom,
                AxisStretchHorizontal = entry.TileHorizontal
                    ? NinePatchRect.AxisStretchMode.Tile : NinePatchRect.AxisStretchMode.Stretch,
                AxisStretchVertical = entry.TileVertical
                    ? NinePatchRect.AxisStretchMode.Tile : NinePatchRect.AxisStretchMode.Stretch,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
        }

        protected ColorRect BuildRoomBackground(string name, float x, float width, float height, Color color) {
            var background = new ColorRect {
                Name = name,
                Size = new Vector2(width, height),
                Position = new Vector2(x, 0),
                Color = color,
                ZIndex = -10,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(background);
            return background;
        }

        protected StaticBody2D BuildFloor(float x, float y, float width, Color? fill = null, float thickness = DefaultFloorThickness) {
            var floor = new StaticBody2D {
                Name = $"Floor_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x + width / 2f, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            var collision = new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) },
                Position = new Vector2(0, thickness / 2f)
            };
            floor.AddChild(collision);

            Control floorVisual = BuildSurfaceVisual(SurfaceTextures?.Floor, fill != null,
                new Vector2(width, thickness), new Vector2(-width / 2f, 0), fill ?? FloorColor);
            floor.AddChild(floorVisual);
            if (floorVisual is ColorRect) {
                // The textured surface carries its own lit top edge.
                floor.AddChild(new ColorRect {
                    Name = "TopLine",
                    Size = new Vector2(width, 3),
                    Position = new Vector2(-width / 2f, 0),
                    Color = FloorEdgeColor,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }

            AddChild(floor);
            return floor;
        }

        protected StaticBody2D BuildPlatform(float x, float y, float width, Color? fill = null, float thickness = DefaultPlatformThickness) {
            var platform = new StaticBody2D {
                Name = $"Platform_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            platform.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) }
            });
            platform.AddChild(BuildSurfaceVisual(SurfaceTextures?.Platform, fill != null,
                new Vector2(width, thickness), new Vector2(-width / 2f, -thickness / 2f), fill ?? PlatformColor));

    AddChild(platform);
            return platform;
        }

        /// <summary>
        /// Drop-through platform on the OneWayPlatform layer. Matches the
        /// OneWayPlatformTemplate contract (group + one-way collision margin).
        /// </summary>
        protected OneWayPlatform BuildOneWayPlatform(float x, float y, float width, Color? fill = null, float thickness = 20f) {
            var platform = new OneWayPlatform {
                Name = $"OneWay_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.OneWayPlatform,
                CollisionMask = 0
            };

            platform.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) },
                OneWayCollision = true,
                OneWayCollisionMargin = 12f
            });
            platform.AddChild(BuildSurfaceVisual(SurfaceTextures?.OneWay, fill != null,
                new Vector2(width, thickness), new Vector2(-width / 2f, -thickness / 2f), fill ?? PlatformColor));

            AddChild(platform);
            return platform;
        }

        protected StaticBody2D BuildWall(float x, float y, float height, Color? fill = null, float thickness = DefaultWallThickness) {
            var wall = new StaticBody2D {
                Name = $"Wall_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            wall.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(thickness, height) },
                Position = new Vector2(thickness / 2f, height / 2f)
            });
            wall.AddChild(BuildSurfaceVisual(SurfaceTextures?.Wall, fill != null,
                new Vector2(thickness, height), Vector2.Zero, fill ?? WallColor));

            AddChild(wall);
            return wall;
        }

        protected Area2D BuildHazardSpikes(float x, float y, float width, int damage = 15, Color? fill = null, float thickness = 10f) {
            var hazard = new Area2D {
                Name = $"Spikes_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };

            hazard.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) }
            });
            hazard.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, thickness),
                Position = new Vector2(-width / 2f, -thickness / 2f),
                Color = fill ?? HazardColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            hazard.BodyEntered += body => {
                if (body is not PlayerController player) return;
                // Hazard damage can cascade into a lethal-hit rewind or a
                // guard break; mark the physics callback so those systems
                // defer the engine-blocked writes.
                using var scope = PhysicsCallbackGuard.Enter();
                ApplySpikeDamage(player, damage);
            };

            AddChild(hazard);
            return hazard;
        }

        /// <summary>
        /// V7.3: spike hazards route through the environmental-damage
        /// chokepoint like every other environmental source (Defy flag
        /// consumption, Rally echo, victim meter — never raw ApplyDamage).
        /// Static and internal so the pin test can exercise the exact path
        /// the BodyEntered lambda takes.
        /// </summary>
        internal static int ApplySpikeDamage(PlayerController player, int damage) =>
            player?.ApplyEnvironmentalDamage(damage) ?? 0;

        /// <summary>
        /// Places one authored Chronal Fracture.
        ///
        /// <b>V7.6 F12 (Package 11 A3): the authored ROLE drives everything.</b>
        /// Self-activation, the Hard "middle inactive" rule and the Integrity
        /// freeze all read <paramref name="role"/> — never the ID's numeric
        /// suffix, its array position or the level's checkpoint count. The
        /// stable IDs are unchanged, so saves and content tests keep resolving;
        /// the role is the alias map <see cref="StoryManager.RegisterCheckpointRole"/>
        /// records, which is what lets a saved <c>_checkpoint_1</c> mean Middle
        /// on a shared level and PreBoss on Level 4A without either guessing.
        /// </summary>
        protected CheckpointTrigger BuildCheckpoint(float x, float y, string id, CheckpointRole role) {
            // F12: Hard's middle fracture is inert in the shared Acts I-II
            // levels only. Act III keeps its Hard middle active (A3b), and 4A's
            // PreBoss anchor is never disabled merely because its ID ends `_1`.
            bool inert = IsMiddleAnchorInert(
                Level, role, GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal);
            var checkpoint = new CheckpointTrigger {
                Name = $"Checkpoint_{id}",
                CheckpointID = id,
                Role = role,
                Inert = inert,
                Position = new Vector2(x, y),
                RespawnOffset = new Vector2(0, -50),
                // V7.3 strike-to-activate: only the ENTRY fracture
                // self-activates — the player just arrived through it (design
                // exception); the middle and pre-boss fractures must be struck.
                SelfActivating = role == CheckpointRole.Entry,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            StoryManager.Instance?.RegisterCheckpointRole(id, role);
            if (inert) {
                // An inert anchor is not a recovery destination: no respawn
                // registration and no F11 budget. The earlier real anchor keeps
                // the longer remaining route, exactly as CHECKPOINT_RECOVERY
                // requires.
                AddCheckpointNodes(checkpoint);
                return checkpoint;
            }
            StoryManager.Instance?.SetRecoveryRouteSeconds(id, ParSeconds * RemainingRouteFractionFor(role));
            StoryManager.Instance?.RegisterEncounterBaseline(
                id, BaselineEncounterIDsFor(id, role), EncounterBaselineVersion);
            _liveCheckpointCount++;
            Levels?.RegisterCheckpoint(id, checkpoint.Position + checkpoint.RespawnOffset);
            AddCheckpointNodes(checkpoint);
            // === Package 11 A3b: the Warden Beacon rides every live Act III
            // anchor. Sarah hands it over at Level 13's entry and it opens the
            // Repository at ANY activated Act III checkpoint — so it is authored
            // with the anchor, and gated at interaction time on that anchor
            // actually having been activated.
            if (IsActIIIGauntletLevel) {
                WardenBeacon beacon = WardenBeacon.Create(id, checkpoint.Position + new Vector2(90f, 0f));
                _wardenBeacons.Add(beacon);
                AddChild(beacon);
            }
            return checkpoint;
        }

        /// <summary>
        /// The three Act III gauntlet levels. Package 12 W8 deleted the old
        /// ordinal <c>IsActIII</c> member, which was also true for Level 4A
        /// (enum value 16, played between Levels 4 and 5) and had no consumers.
        /// </summary>
        protected bool IsActIIIGauntletLevel => StoryManager.IsActIIILevel(Level);

        /// <summary>
        /// F12: Hard's <b>middle</b> fracture is inert — it activates nothing,
        /// anchors no respawn and registers no F11 budget — but only in Acts I-II.
        /// V7.6 suspends the rule for the Act III gauntlet, where all three
        /// anchors stay live on every difficulty. Entry and PreBoss are never
        /// inert, and no anchor is ever judged by its ID suffix.
        ///
        /// <para>Pure and shared so the rule has exactly one statement: the
        /// builder reads it and so does its test.</para>
        /// </summary>
        public static bool IsMiddleAnchorInert(CampaignLevel level, CheckpointRole role, Difficulty difficulty) =>
            role == CheckpointRole.Middle
            && difficulty == Difficulty.Hard
            && !StoryManager.IsActIIILevel(level);

        /// <summary>
        /// F10: the authored encounter baseline an anchor reconstructs from.
        /// "Bind baseline membership to stable encounter IDs, not an enemy's
        /// physical position at death."
        ///
        /// <para>The default derives the IDs from the level's own authored anchor
        /// order, reproducing the rule the campaign controllers already implement
        /// in <see cref="MarkWavesClearedThrough"/>: the entrance anchor clears
        /// nothing, and the anchor at authored index <c>i &gt; 0</c> restores
        /// <c>{LevelID}_wave_1 … _wave_(i+1)</c> as already cleared. Encounters
        /// beyond the baseline may respawn, but a respawned enemy whose reward is
        /// already claimed pays nothing again — that is the reward ledger's job,
        /// not the baseline's.</para>
        ///
        /// <para>A level whose route does not match overrides this. The IDs are
        /// authored data; nothing infers membership from where an enemy happened
        /// to be standing when the player died.</para>
        ///
        /// <para><b>Package 12 W2 (GAP-13):</b> the derivation matched only six
        /// of the fourteen campaign controllers, and on Hard it mis-indexed the
        /// PreBoss anchor behind an inert middle. Every campaign controller now
        /// authors <see cref="EncounterBaselineMap"/>, which wins; the derivation
        /// survives only for synthetic harness levels that author no map.</para>
        /// </summary>
        protected virtual IReadOnlyList<string> BaselineEncounterIDsFor(string checkpointID, CheckpointRole role) {
            IReadOnlyDictionary<string, string[]> map = EncounterBaselineMap;
            if (map != null) {
                return map.TryGetValue(checkpointID ?? "", out string[] authored) && authored != null
                    ? new List<string>(authored)
                    : new List<string>();
            }
            var ids = new List<string>();
            int waves = _liveCheckpointCount == 0 ? 0 : _liveCheckpointCount + 1;
            for (int wave = 1; wave <= waves; wave++) ids.Add($"{LevelID}_wave_{wave}");
            return ids;
        }

        /// <summary>Live (non-inert) anchors authored so far; the index the baseline derivation uses.</summary>
        private int _liveCheckpointCount;

        private void AddCheckpointNodes(CheckpointTrigger checkpoint) {
            checkpoint.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(60, 120) },
                Position = new Vector2(0, -60)
            });
            checkpoint.AddChild(new ColorRect {
                Name = "CheckpointVisual",
                Size = new Vector2(20, 80),
                Position = new Vector2(-10, -80),
                Color = new Color(0.0f, 0.7f, 0.9f, 0.6f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            var label = new Label {
                Name = "CheckpointLabel",
                Text = Tr("checkpoint"),
                Position = new Vector2(-40, -100),
                CustomMinimumSize = new Vector2(80, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));
            checkpoint.AddChild(label);

            AddChild(checkpoint);
        }

        // === F16 lethal pits & Collapse Tremor platforms (Package 11 A3) =====

        /// <summary>
        /// Authors one lethal boundary under a pit, chasm or void. Crossing it
        /// resolves a single non-hit fall death through
        /// <see cref="StoryKillBoundary"/> — never a damage event, so Defy
        /// History, the Rally echo and the meter are all untouched.
        ///
        /// Place it well below the deepest reachable geometry: camera framing
        /// never kills, only this does.
        /// </summary>
        protected StoryKillBoundary BuildKillBoundary(
            string boundaryID, float centerX, float y, float width, float height = 220f) {
            var boundary = new StoryKillBoundary {
                Name = $"KillBoundary_{boundaryID}",
                BoundaryID = boundaryID,
                Position = new Vector2(centerX, y)
            };
            boundary.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height)) }
            });
            // The readable edge/depth cue F16 requires. Deliberately a world
            // object rather than a HUD element, so it is visible during a Time
            // Freeze as well — a frozen world must still show the player where
            // the floor stops.
            boundary.AddChild(new ColorRect {
                Name = "EdgeCue",
                Size = new Vector2(Mathf.Max(1f, width), 10f),
                Position = new Vector2(-width * 0.5f, -height * 0.5f - 10f),
                Color = new Color(0.85f, 0.25f, 0.3f, 0.42f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            AddChild(boundary);
            return boundary;
        }

        /// <summary>
        /// A platform that destabilizes once the Collapse Tremor starts: it
        /// shakes, collapses, and always respawns after
        /// <see cref="CollapseTremorRules.FracturePlatformRespawnSeconds"/>, so
        /// the gap it spans can never become permanently uncrossable.
        ///
        /// Never use this for a pressure plate, a latched-switch gate, a
        /// <c>PathMovingPlatform</c> or anything on the pre-boss approach — the
        /// exclusion is enforced by simply not flagging those surfaces.
        /// </summary>
        protected CrumblingPlatform BuildFracturePlatform(
            string platformID, float x, float y, float width, Color? fill = null) {
            var platform = new CrumblingPlatform {
                Name = $"FracturePlatform_{platformID}",
                PlatformID = platformID,
                FractureEligible = true,
                RespawnDuration = CollapseTremorRules.FracturePlatformRespawnSeconds,
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            platform.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(width, DefaultPlatformThickness) }
            });
            platform.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, DefaultPlatformThickness),
                Position = new Vector2(-width * 0.5f, -DefaultPlatformThickness * 0.5f),
                Color = fill ?? PlatformColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            AddChild(platform);
            return platform;
        }

        /// <summary>Proximity trigger that runs <paramref name="onEntered"/> the first time the player crosses it.</summary>
        protected Area2D BuildWaveTrigger(string name, Vector2 position, Action onEntered, Vector2? size = null) {
            var trigger = new Area2D {
                Name = name,
                Position = position,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            trigger.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = size ?? new Vector2(60, 400) }
            });
            trigger.BodyEntered += body => {
                if (body is not PlayerController) return;
                // Wave callbacks spawn collision bodies, which the engine
                // forbids during the in/out flush this signal runs in; fire
                // the callback right after the flush.
                Callable.From(() => onEntered?.Invoke()).CallDeferred();
            };
            AddChild(trigger);
            return trigger;
        }

        /// <summary>
        /// Room boundary: retargets the camera confiner, activates the room's
        /// encounter root, and raises the shared room-transition event.
        /// </summary>
        protected RoomTransitionTrigger BuildRoomTransition(
            string roomID,
            Vector2 position,
            Rect2 cameraBounds,
            Vector2? triggerSize = null,
            Action<string> onEntered = null,
            Node encounterRoot = null) {
            var trigger = new RoomTransitionTrigger {
                Name = $"RoomTransition_{roomID}",
                RoomID = roomID,
                Position = position,
                CameraBounds = cameraBounds,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            trigger.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = triggerSize ?? new Vector2(80, 900) }
            });
            AddChild(trigger);
            if (encounterRoot != null) trigger.EncounterRootPath = trigger.GetPathTo(encounterRoot);
            if (onEntered != null) trigger.RoomEntered += id => onEntered(id);
            _roomTriggers.Add(trigger);
            LinkRoomCameras();
            return trigger;
        }

        /// <summary>
        /// Solid placeholder door standing ON <paramref name="basePosition"/> (its
        /// floor contact point) and extending upward. Call <see cref="OpenDoor"/>
        /// when the gate that seals it clears.
        /// </summary>
        protected StaticBody2D BuildDoor(string name, Vector2 basePosition, string lockedLabelKey = "level_door_locked",
            Vector2? size = null, Color? fill = null) {
            Vector2 doorSize = size ?? new Vector2(40, 400);
            var door = new StaticBody2D {
                Name = name,
                Position = basePosition,
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            door.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = doorSize },
                Position = new Vector2(0, -doorSize.Y / 2f)
            });
            door.AddChild(new ColorRect {
                Name = "Visual",
                Size = doorSize,
                Position = new Vector2(-doorSize.X / 2f, -doorSize.Y),
                Color = fill ?? new Color(0.45f, 0.35f, 0.2f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            var lockLabel = new Label {
                Name = "LockedLabel",
                Text = Tr(lockedLabelKey),
                Position = new Vector2(-90, -doorSize.Y - 40),
                CustomMinimumSize = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            lockLabel.AddThemeFontSizeOverride("font_size", 11);
            lockLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.6f, 0.2f));
            door.AddChild(lockLabel);

            AddChild(door);
            return door;
        }

        /// <summary>Removes a door built by <see cref="BuildDoor"/>; safe to call twice.</summary>
        protected static bool OpenDoor(ref StaticBody2D door) {
            if (door == null || !IsInstanceValid(door)) {
                door = null;
                return false;
            }
            door.QueueFree();
            door = null;
            return true;
        }

        protected Label BuildRoomDecoration(float offsetX, string roomNameKey, Color color) {
            var label = new Label {
                Name = $"RoomLabel_{roomNameKey}",
                Text = Tr(roomNameKey),
                Position = new Vector2(offsetX + 50, 30),
                CustomMinimumSize = new Vector2(300, 25),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", color);
            AddChild(label);
            return label;
        }

        // === Chronal Extractors ===

        public const string ExtractorTemplatePath = "res://scenes/templates/ChronalExtractorTemplate.tscn";

        /// <summary>
        /// Places one authored Chronal Extractor. Dust value stays resource-owned
        /// (docs/DUST_ECONOMY.md: 15 each) - never override it from level code.
        /// </summary>
        protected ChronalExtractor BuildExtractor(string extractorID, Vector2 position) {
            var packed = ResourceLoader.Load<PackedScene>(ExtractorTemplatePath);
            if (packed?.Instantiate() is not ChronalExtractor extractor) {
                GD.PushWarning($"Chronal Extractor template could not be instantiated for '{extractorID}'.");
                return null;
            }
            extractor.Name = $"Extractor_{extractorID}";
            extractor.ObjectID = extractorID;
            extractor.Position = position;
            // Itemization (audit M-1, reworked by the V7.3 Single Icon Rule):
            // destruction spawns a physical pickup; the wallet payment AND the
            // extractor-line attribution both land at collection through
            // OnDustAwardCollected — nothing to wire per instance here.
            AddChild(extractor);
            _extractors.Add(extractor);
            return extractor;
        }

        /// <summary>Places the level's extractors from an authored position table.</summary>
        protected void BuildExtractors(params (string ID, Vector2 Position)[] placements) {
            if (placements == null) return;
            foreach ((string id, Vector2 position) in placements) BuildExtractor(id, position);
        }

        // === Package 11 A3b: Act III Resonance Hold drain stand-ins ==========

        public const string ResonanceHoldTemplatePath =
            "res://scenes/templates/ResonanceHoldNodeTemplate.tscn";

        /// <summary>
        /// Places one Act III drain stand-in — a severed conduit (L13), a cradle
        /// intake valve (L14) or a firing-channel anchor pylon (L15).
        ///
        /// <para>It enters the same <c>_extractors</c> list as a Chronal
        /// Extractor, which is the whole point: the F01 denominator, the
        /// <c>+0.2</c> drain weight, the destroyed-registry entry, the resume
        /// rebuild and the F05 optional-dust share are an Extractor's exactly.
        /// Only the fiction and the strings differ.</para>
        /// </summary>
        protected ResonanceHoldNode BuildResonanceHoldNode(
            string nodeID, Vector2 position, ResonanceHoldVariant variant) {
            var packed = ResourceLoader.Load<PackedScene>(ResonanceHoldTemplatePath);
            if (packed?.Instantiate() is not ResonanceHoldNode node) {
                GD.PushWarning($"Resonance Hold node template could not be instantiated for '{nodeID}'.");
                return null;
            }
            node.Name = $"HoldNode_{nodeID}";
            node.ObjectID = nodeID;
            node.Position = position;
            node.Variant = variant;
            AddChild(node);
            _extractors.Add(node);
            return node;
        }

        /// <summary>Places the level's Act III drain stand-ins from its authored position table.</summary>
        protected void BuildResonanceHoldNodes(
            ResonanceHoldVariant variant, params (string ID, Vector2 Position)[] placements) {
            if (placements == null) return;
            foreach ((string id, Vector2 position) in placements) {
                BuildResonanceHoldNode(id, position, variant);
            }
        }

        /// <summary>The Act III drain stand-ins this level built. Test seam.</summary>
        public IReadOnlyList<ResonanceHoldNode> ResonanceHoldNodes {
            get {
                var nodes = new List<ResonanceHoldNode>();
                foreach (ChronalExtractor extractor in _extractors) {
                    if (extractor is ResonanceHoldNode hold) nodes.Add(hold);
                }
                return nodes;
            }
        }

        /// <summary>The Warden Beacons this level placed, one per live Act III anchor. Test seam.</summary>
        public IReadOnlyList<WardenBeacon> WardenBeacons => _wardenBeacons;

        private readonly List<WardenBeacon> _wardenBeacons = new();

        // === Package 12 W8 region: the F05 secret cache (GAP-03) =============

        /// <summary>
        /// The level's designated F05 secret source ID — the <c>{level}.secret</c>
        /// row its reward manifest reserves. Levels 2–15 are <c>level_NN.secret</c>;
        /// a Level 4A variant is <c>level_04a_&lt;hero&gt;.secret</c>, which is
        /// exactly <c>{DialoguePrefix}.secret</c> in both cases.
        /// </summary>
        public virtual string SecretSourceID => $"{DialoguePrefix}.secret";

        /// <summary>
        /// Where this level hides its secret cache, or null for a level that
        /// reserves no secret (the Tutorial, Florence). Authored per level —
        /// off the critical path and reachable with the Legacy-locked kit (base
        /// jumps and roll only) — and placed by the base after
        /// <see cref="BuildLevel"/>, so a sealed Level 4A <c>BuildLevel</c> gets
        /// it too. The position is the trigger's centre; stand on the surface
        /// under it to discover it.
        /// </summary>
        protected virtual Vector2? SecretCachePosition => null;

        /// <summary>Read-only view of <see cref="SecretCachePosition"/> for content validation.</summary>
        public Vector2? AuthoredSecretCachePosition => SecretCachePosition;

        /// <summary>The cache this level placed, or null. Test seam.</summary>
        public SecretCache SecretCache { get; private set; }

        private void BuildAuthoredSecretCache() {
            if (SecretCachePosition is not Vector2 position) return;
            SecretCache = SecretCache.Create(SecretSourceID, position);
            AddChild(SecretCache);
        }

        // === end Package 12 W8 region (secret cache) =========================

        // === Package 12 W8 region: the N01 sealing anchor (GAP-05, D12(a)) ====
        //
        // N01: boss defeat does not complete the level. It commits AwaitingSeal
        // (attemptState.sealReadiness: the boss-defeated fact, the stable anchor
        // ID and the pending physical boss pickup), plays the existing defeat
        // beats, then hands control back beside the boss pickup and a marked
        // sealing anchor. A single Interact — "Seal Timeline — Complete Level" —
        // commits the existing once-only completion transaction, and only then
        // does the restoration vignette (the level's exit beat) play and the
        // results follow. A load before that Interact reconstructs the defeated
        // boss (never respawned), the ready anchor and at most one pending boss
        // pickup. D12(a): AwaitingSeal is PERSISTED, not derived.

        /// <summary>The seal prompt every generic anchor shows.</summary>
        public const string SealPromptKey = "seal_timeline_prompt";

        /// <summary>The objective posted while the anchor waits for the player.</summary>
        public const string SealObjectiveKey = "seal_timeline_objective";

        /// <summary>The world notice posted when the seal is accepted.</summary>
        public const string SealedNoticeKey = "seal_timeline_sealed";

        /// <summary>The generic anchor's signage, before and after the boss falls.</summary>
        public const string SealAnchorDormantKey = "seal_anchor_dormant";
        public const string SealAnchorReadyKey = "seal_anchor_ready";

        /// <summary>
        /// The stable sealing-anchor ID, distinct from every checkpoint and Nexus
        /// source ID. Level 15 overrides it with its Prime Anchor's ID.
        /// </summary>
        public virtual string SealingAnchorID => $"{DialoguePrefix}.sealing_anchor";

        /// <summary>
        /// False for a level whose sealing interaction is authored elsewhere
        /// (Level 15's scene-placed Prime Anchor). Such a level still commits
        /// and restores AwaitingSeal through this base.
        /// </summary>
        protected virtual bool UsesGenericSealingAnchor => true;

        /// <summary>
        /// Where the generic anchor stands. Null derives it from the first boss
        /// encounter: on the boss's own standing height, beside the arena centre
        /// where the boss pickup falls — clear ground reachable with any kit and
        /// no resources. A level whose boss is not a <see cref="BossEncounterController"/>
        /// (Level 13's Mirror) authors it explicitly.
        /// </summary>
        protected virtual Vector2? SealingAnchorPosition => null;

        /// <summary>Horizontal offset of a derived anchor from the boss spawn point.</summary>
        public const float DerivedSealingAnchorOffsetX = -260f;

        /// <summary>The generic anchor this level placed, or null. Test seam.</summary>
        public TemporalCoreAnchor SealingAnchor { get; private set; }

        /// <summary>True while this load is reconstructing a pre-seal AwaitingSeal state.</summary>
        protected bool IsRestoringAwaitingSeal { get; private set; }

        /// <summary>True when this load resumed an attempt already awaiting its seal. Test seam.</summary>
        public bool ResumedAwaitingSeal { get; private set; }

        /// <summary>True once the player's seal Interact has been accepted.</summary>
        public bool SealAccepted { get; private set; }

        /// <summary>True between the anchor arming and the seal being accepted.</summary>
        public bool IsAwaitingSeal =>
            _bossDefeated && !SealAccepted && !LevelComplete
            && SealingAnchor != null && IsInstanceValid(SealingAnchor) && SealingAnchor.IsArmed;

        /// <summary>A level with a sealing stage: a generic anchor, or its own (Level 15).</summary>
        private bool HasSealStage => !UsesGenericSealingAnchor || SealingAnchor != null;

        private Vector2? ResolveSealingAnchorPosition() {
            if (SealingAnchorPosition is Vector2 authored) return authored;
            foreach (BossEncounterController encounter in _bossEncounters) {
                if (!IsInstanceValid(encounter)) continue;
                return encounter.Position + encounter.SpawnOffset + new Vector2(DerivedSealingAnchorOffsetX, 0f);
            }
            return null;
        }

        /// <summary>Where a restored pending boss pickup respawns: the arena centre.</summary>
        protected virtual Vector2 BossRewardRestorePosition {
            get {
                foreach (BossEncounterController encounter in _bossEncounters) {
                    if (IsInstanceValid(encounter)) return encounter.Position + encounter.SpawnOffset;
                }
                return SealingAnchor != null ? SealingAnchor.Position + new Vector2(-DerivedSealingAnchorOffsetX, 0f)
                    : PlayerSpawnPosition;
            }
        }

        private void BuildGenericSealingAnchor() {
            if (!UsesGenericSealingAnchor || ResolveSealingAnchorPosition() is not Vector2 position) return;
            SealingAnchor = TemporalCoreAnchor.CreateSealingAnchor(SealingAnchorID, position);
            SealingAnchor.CoreInserted += _ => AcceptSeal();
            AddChild(SealingAnchor);
            if (IsRestoringAwaitingSeal) SealingAnchor.Arm();
        }

        /// <summary>
        /// Reads whether this load resumes an attempt already awaiting its seal:
        /// the active save is parked in this level at a checkpoint, and the
        /// attempt record committed the boss-defeated fact. Runs before
        /// <see cref="BuildLevel"/>, so the encounter is built defeated.
        /// </summary>
        private void DetectPreSealLoad() {
            IsRestoringAwaitingSeal = false;
            StoryManager story = StoryManager.Instance;
            if (story == null || !story.HasLiveAttempt || SaveManager.Instance == null
                || GameManager.Instance == null) return;
            StorySealReadiness readiness = story.CurrentAttempt.SealReadiness;
            if (readiness == null || !readiness.BossDefeated) return;
            if (!string.Equals(story.CurrentAttempt.LevelID, LevelID, StringComparison.Ordinal)) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null || string.IsNullOrWhiteSpace(save.LastCheckpointID)) return;
            if (save.CurrentLevelID != StoryManager.GetLevelScenePath(Level)) return;
            IsRestoringAwaitingSeal = true;
            ResumedAwaitingSeal = true;
            // Set before BuildLevel/OnLevelReady so a level reading
            // IsBossDefeated (Level 15's Prime Anchor, the 4A objective) sees the
            // committed defeat.
            _bossDefeated = true;
            _bossIntroShown = true;
            _postBossBeatIndex = int.MaxValue;
        }

        /// <summary>
        /// Boss defeat commits AwaitingSeal once: the defeat fact, the anchor ID
        /// and whether the boss pickup is still uncollected. No completion, no
        /// deposit, no unlock. Critical-event write, like every F10 commit.
        /// </summary>
        private void CommitSealReadiness() {
            StoryManager story = StoryManager.Instance;
            if (story == null || !story.HasLiveAttempt || !HasSealStage) return;
            StorySealReadiness readiness = story.CurrentAttempt.SealReadiness ??= new StorySealReadiness();
            if (readiness.BossDefeated) return;
            readiness.BossDefeated = true;
            readiness.SealingAnchorID = SealingAnchorID;
            LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
            readiness.PendingBossPickup = ledger != null
                && !string.IsNullOrEmpty(ledger.BossSourceID)
                && !LevelRewardDirectory.IsClaimed(ledger.BossSourceID);
            story.CurrentAttempt.Bump();
            story.CommitCriticalEvent();
        }

        /// <summary>
        /// For a level whose boss is not a <see cref="BossEncounterController"/>
        /// (Level 13's Mirror): records the defeat and commits AwaitingSeal
        /// exactly as <see cref="OnBossDefeated"/> does, then hands off to the
        /// seal stage.
        /// </summary>
        protected void CommitNonStandardBossDefeat() {
            if (_bossDefeated) return;
            _bossDefeated = true;
            _postBossBeatIndex = int.MaxValue;
            CommitSealReadiness();
            EnterAwaitingSeal();
        }

        private void NoteBossPickupCollected() {
            StoryManager story = StoryManager.Instance;
            StorySealReadiness readiness = story?.HasLiveAttempt == true ? story.CurrentAttempt.SealReadiness : null;
            if (readiness == null || !readiness.BossDefeated || !readiness.PendingBossPickup) return;
            readiness.PendingBossPickup = false;
            story.CurrentAttempt.Bump();
        }

        /// <summary>
        /// Arms the sealing anchor once the defeat beats have finished, and waits
        /// for the player. A level with no sealing stage keeps the pre-N01 chain
        /// (straight into the exit beat); Level 15's own anchor is armed by the
        /// level and its exit override only posts the objective.
        /// </summary>
        protected void EnterAwaitingSeal() {
            if (SealAccepted || LevelComplete) return;
            if (SealingAnchor == null || !IsInstanceValid(SealingAnchor)) {
                StartExitSequence();
                return;
            }
            SealingAnchor.Arm();
            SetObjective(SealObjectiveKey);
        }

        /// <summary>
        /// The accepted seal: shut the remaining local siphons down (a terminal
        /// state, never a destruction, pickup or credit), clear AwaitingSeal,
        /// commit the once-only completion transaction, and only then start the
        /// restoration vignette — the exit beat — whose end presents the results.
        /// Idempotent: a duplicate press, callback or load cannot settle it again.
        /// </summary>
        public bool AcceptSeal() {
            if (SealAccepted || LevelComplete || !_bossDefeated) return false;
            SealAccepted = true;
            foreach (ChronalExtractor extractor in _extractors) {
                if (IsInstanceValid(extractor)) extractor.ShutDownBySeal();
            }
            CommitLevelCompletion();
            if (SealingAnchor != null && IsInstanceValid(SealingAnchor)) {
                EnvironmentNotice.Post(SealedNoticeKey, SealingAnchor);
            }
            StartExitSequence();
            return true;
        }

        /// <summary>
        /// The pre-seal load, after services attach: the defeat objective, the
        /// ready anchor and at most one pending boss pickup. The locked score and
        /// the durable F10 resources were already restored by the ordinary path.
        /// </summary>
        private void RestoreAwaitingSeal() {
            IsRestoringAwaitingSeal = false;
            SetObjective(CompletionObjectiveKey);
            RestorePendingBossPickup();
            if (SealingAnchor != null && IsInstanceValid(SealingAnchor)) {
                SealingAnchor.Arm();
                SetObjective(SealObjectiveKey);
            }
        }

        /// <summary>
        /// F05 + N01: an uncollected boss reward survives a pre-seal load exactly
        /// once. Claims persist and issues do not, so the reload re-issues the
        /// same source and spawns its single Large pickup at the arena centre;
        /// a claimed source spawns nothing.
        /// </summary>
        private void RestorePendingBossPickup() {
            StoryManager story = StoryManager.Instance;
            StorySealReadiness readiness = story?.HasLiveAttempt == true ? story.CurrentAttempt.SealReadiness : null;
            if (readiness == null || !readiness.PendingBossPickup) return;
            LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
            if (ledger == null || string.IsNullOrEmpty(ledger.BossSourceID)) return;
            if (LevelRewardDirectory.IsClaimed(ledger.BossSourceID)) {
                readiness.PendingBossPickup = false;
                story.CurrentAttempt.Bump();
                return;
            }
            if (!LevelRewardDirectory.TryIssueBossAward(out string sourceID, out int amount) || amount <= 0) return;
            RestoredBossPickup = StoryDropSystem.SpawnDustAward(
                amount, BossRewardRestorePosition, this, DustAwardSource.Boss, sourceID);
        }

        /// <summary>The boss pickup a pre-seal load respawned, or null. Test seam.</summary>
        public ChronalDustPickup RestoredBossPickup { get; private set; }

        // === end Package 12 W8 region (sealing anchor) =======================

        // === Boss encounter ===

        /// <summary>
        /// Wires the reusable <see cref="BossEncounterController"/> into the level's
        /// dialogue and objective flow: reveal posts the boss objective and starts
        /// the intro sequence; defeat posts the completion objective, tallies the
        /// authored dust, and (after a beat) starts the exit sequence.
        /// </summary>
        protected BossEncounterController BuildBossEncounter(
            string bossResourcePath,
            Vector2 position,
            string encounterName = null,
            float revealDistance = 800f,
            Vector2 spawnOffset = default) {
            var data = AuthoredResources.Load<BossData>(bossResourcePath);
            if (data == null) {
                GD.PushError($"Boss resource missing for level '{LevelID}': {bossResourcePath}");
                return null;
            }

            var encounter = new BossEncounterController {
                Name = encounterName ?? $"{data.BossID}_encounter",
                Position = position,
                Data = data,
                SpawnOffset = spawnOffset,
                RevealDistance = revealDistance,
                // Package 12 W8 (N01): a pre-seal load reconstructs the boss as
                // already defeated — it is never spawned and fought again.
                SpawnOnReady = !IsRestoringAwaitingSeal
            };
            encounter.BossRevealed += () => OnBossRevealed(encounter);
            encounter.BossDefeated += payload => OnBossDefeated(encounter, payload);
            // Package 8 B5: the climax layer is wired to the encounter directly rather
            // than to OnBossDefeated, which subclasses are free to override without
            // calling base — the music must not depend on that.
            encounter.BossRevealed += () => Audio?.SetBossEngaged(true);
            encounter.BossDefeated += _ => Audio?.SetBossEngaged(false);
            AddChild(encounter);
            _bossEncounters.Add(encounter);
            if (Services?.HUD != null) encounter.HUD = Services.HUD;
            return encounter;
        }

        private bool _bossIntroShown;
        private bool _bossDefeated;
        private int _postBossBeatIndex = -1;

        public bool IsBossDefeated => _bossDefeated;

        /// <summary>
        /// Post-boss beat currently on screen, or "" when the chain has not started
        /// or has already handed off to the exit sequence.
        /// </summary>
        public string ActivePostBossDialogueID {
            get {
                IReadOnlyList<string> beats = PostBossDialogueIDs ?? NoPostBossBeats;
                return _postBossBeatIndex >= 0 && _postBossBeatIndex < beats.Count
                    ? beats[_postBossBeatIndex]
                    : "";
            }
        }

        protected virtual void OnBossRevealed(BossEncounterController encounter) {
            if (_bossIntroShown) return;
            _bossIntroShown = true;
            SetObjective(BossObjectiveKey);
            StartDialogue(BossIntroDialogueID);
        }

        /// <summary>
        /// Boss-defeat bookkeeping. Prefer <see cref="PostBossDialogueIDs"/> or
        /// <see cref="StartPostBossSequence"/> over overriding this: everything a
        /// level normally wants to change lives after the flag, the objective, and
        /// the dust tally, and an override that skips them silently disables
        /// <see cref="IsBossDefeated"/>.
        /// </summary>
        protected virtual void OnBossDefeated(BossEncounterController encounter, BossDefeatedPayload payload) {
            if (_bossDefeated) return;
            _bossDefeated = true;
            SetObjective(CompletionObjectiveKey);
            // V7.3 Single Icon Rule: the encounter spawned a physical pickup;
            // the wallet payment and the boss-line attribution both land at
            // collection (OnDustAwardCollected) — attributing here as well
            // would label dust the wallet has not been paid.
            // Package 12 W8 (N01): the defeat commits AwaitingSeal — never
            // completion, a deposit or an unlock.
            CommitSealReadiness();
            StartPostBossSequence();
        }

        /// <summary>
        /// Everything that happens once the boss is down and the base has settled
        /// its bookkeeping: play every <see cref="PostBossDialogueIDs"/> beat in
        /// order, then <see cref="StartExitSequence"/>.
        ///
        /// Override to replace the whole tail — Level 15 swaps the exit/results
        /// chain for the campaign completion chain — while keeping
        /// <see cref="IsBossDefeated"/>, the completion objective, and the dust
        /// tally intact.
        /// </summary>
        protected virtual void StartPostBossSequence() => AdvancePostBossChain();

        /// <summary>
        /// Plays the next unplayed <see cref="PostBossDialogueIDs"/> beat after the
        /// standard beat delay, or starts the exit sequence when the chain is spent.
        /// Called again by the base each time one of those beats completes.
        /// </summary>
        protected void AdvancePostBossChain() {
            if ((PostBossDialogueIDs ?? NoPostBossBeats).Count <= _postBossBeatIndex + 1) {
                _postBossBeatIndex = int.MaxValue;
                EnterAwaitingSeal();
                return;
            }
            RunAfterBeatDelay(PlayNextPostBossBeat);
        }

        private void PlayNextPostBossBeat() {
            IReadOnlyList<string> beats = PostBossDialogueIDs ?? NoPostBossBeats;
            while (++_postBossBeatIndex < beats.Count) {
                if (StartDialogue(beats[_postBossBeatIndex])) return;
            }
            // Nothing left, or nothing would start: never leave the player in a
            // finished arena with no results overlay (or, since N01, no anchor).
            _postBossBeatIndex = int.MaxValue;
            EnterAwaitingSeal();
        }

        private bool IsActivePostBossBeat(string dialogueID) {
            IReadOnlyList<string> beats = PostBossDialogueIDs ?? NoPostBossBeats;
            return _postBossBeatIndex >= 0 && _postBossBeatIndex < beats.Count &&
                   beats[_postBossBeatIndex] == dialogueID;
        }

        /// <summary>Delayed hand-off from the boss beat to the exit dialogue.</summary>
        protected virtual void StartExitSequence() =>
            RunAfterBeatDelay(() => {
                if (!StartDialogue(ExitDialogueID)) ShowCompletionResults();
            });

        /// <summary>
        /// Runs <paramref name="step"/> after <see cref="ExitDialogueDelaySeconds"/>,
        /// or immediately when there is no tree to time against. The timer callback
        /// re-checks the level: a scene torn down mid-beat (fast scene change, a test
        /// fixture) must not resume the chain against a freed node.
        /// </summary>
        private void RunAfterBeatDelay(Action step) {
            float delay = Mathf.Max(0f, ExitDialogueDelaySeconds);
            if (delay <= 0f || GetTree() == null) {
                step();
                return;
            }
            SceneTreeTimer timer = GetTree().CreateTimer(delay);
            timer.Timeout += () => {
                if (!IsInstanceValid(this) || !IsInsideTree()) return;
                step();
            };
        }

        // === Completion ===

        /// <summary>
        /// Ends the level: raises OnLevelComplete (advance + autosave) and shows the
        /// results overlay whose Return button goes back to the Time-Ship hub.
        /// </summary>
        public LevelResultsPanel ShowCompletionResults() {
            if (_completionPresented) return null;
            _completionPresented = true;
            CommitLevelCompletion();
            // Package 8 B5: settle back onto the ambient bed under the results overlay.
            Audio?.ReleaseToAmbient();
            return PresentCompletion();
        }

        private bool _completionPresented;

        /// <summary>
        /// Package 12 W8 (N01): the once-only completion commit, split from its
        /// presentation. An accepted seal commits here <b>before</b> the
        /// restoration vignette starts; the results overlay that follows
        /// presents the committed result and never commits a second time.
        /// Raises <c>OnLevelComplete</c> (advance, deposit, autosave) exactly once.
        /// </summary>
        protected void CommitLevelCompletion() {
            if (LevelComplete) return;
            LevelComplete = true;
            // N01: AwaitingSeal ends inside the completion commit, so the
            // completion write can never carry a stale "ready to seal" record.
            StoryManager story = StoryManager.Instance;
            if (story?.HasLiveAttempt == true && story.CurrentAttempt.SealReadiness?.BossDefeated == true) {
                story.CurrentAttempt.SealReadiness = new StorySealReadiness();
                story.CurrentAttempt.Bump();
            }
            Levels?.CompleteLevel();
        }

        /// <summary>
        /// What the player sees once the level is over and <c>OnLevelComplete</c>
        /// has been raised. Default: the shared results overlay whose Return button
        /// goes back to the Time-Ship hub.
        ///
        /// Level 15 overrides this to run <c>CampaignCompletionSequence</c>
        /// (credits -> campaign completion -> main menu) instead of returning to the
        /// hub, and returns null. The one-shot <see cref="LevelComplete"/> guard and
        /// the level advance stay with the base either way.
        /// </summary>
        protected virtual LevelResultsPanel PresentCompletion() {
            var results = LevelResultsPanel.CreateDefault();
            // === Package 11 A3b: the Act III Gauntlet has no portal trip ======
            // Levels 13-15 play back to back with no return to the Time-Ship —
            // "we can hear you, we can't reach you". The results overlay and the
            // post-boss dialogue still play; only the hub leg is gone. Steps 5-6
            // of the completion flow (Return Prompt, Hub Spawn) simply do not run
            // for Levels 13 and 14, and the level-completion auto-deposit that
            // StoryManager already committed is the gauntlet's only banking
            // event. OnLevelComplete has advanced CurrentLevel by now, so
            // LoadCurrentLevel() is the next level, in-level.
            //
            // Level 15 never reaches here: it overrides PresentCompletion and
            // runs the campaign ending instead.
            results.ReturnRequested += () => {
                if (IsActIIIGauntletLevel) StoryManager.Instance?.LoadCurrentLevel();
                else StoryManager.Instance?.ReturnToHub();
            };
            AddChild(results);
            // Package 11 A10 (F05): the six-category itemisation — required
            // enemies, boss, Extractors, secret/other optional, losses, and the
            // Integrity tier bonus. Every value is what the wallet was actually
            // paid; the panel never computes an economy number of its own.
            results.ShowResults(
                LevelTitleKey,
                MobDustEarned,
                ExtractorDustEarned,
                BossDustEarned,
                OptionalDustEarned,
                LevelRewardDirectory.DustLostThisAttempt,
                LevelRewardDirectory.LastTierBonusDust,
                StoryManager.Instance?.LastLevelCompletionSeconds ?? 0f,
                StoryManager.Instance?.LastLevelRewindsUsed ?? 0);
            return results;
        }
    }
}
