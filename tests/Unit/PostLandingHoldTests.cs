using System;
using System.Collections.Generic;
using System.Linq;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>A world object on the Time Freeze path: it only latches a flag.</summary>
public partial class HoldFreezeProbe : Node2D, IStoryTimeFreezable {
    public bool IsFrozen;
    public void SetTimeFrozen(bool frozen) => IsFrozen = frozen;
}

/// <summary>A world object with only the rewind hook (the Chronal Extractor's shape).</summary>
public partial class HoldLatchProbe : Node2D, IStoryRewindSimulation {
    public bool IsLatched;
    public void SetStoryRewindFrozen(bool frozen) => IsLatched = frozen;
}

/// <summary>A puzzle control / Font stand-in reached through an InteractionArea.</summary>
public partial class HoldInteractableProbe : Node2D, IInteractable {
    public int Interactions;
    public string InteractionID => "post_landing_hold_probe";
    public string PromptKey => "hud_time_freeze_ready";
    public bool CanInteract(PlayerController player) => true;
    public void Interact(PlayerController player) => Interactions++;
}

/// <summary>
/// Package 12 W1 — R02 (the Story revive edge) and R03 (the Post-Landing Hold),
/// against <c>design-godot.md</c> "Landing Sequence" / "Post-Landing Hold" and
/// <c>docs/design-contracts/TEMPORAL_STATE_CONTRACT.md</c>. One case per rule:
/// Respawning is never touched; the union frozen set; no credit; blocked world
/// actions; every clock runs; protection from the thaw; the bracketing events
/// and the thaw cue; Reduced Temporal Effects keeps the cue; a pending boss
/// transition plays after the thaw; the in-session placement holds; and the
/// hold is never persisted or regranted by a reload.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PostLandingHoldTests {
    private const double Step = 1.0 / 60.0;

    /// <summary>A player, the rewind manager and a host to free, in the tree.</summary>
    private sealed class HoldRig : IDisposable {
        public readonly Node2D Host;
        public readonly PlayerController Player;
        public readonly ChronalRewindManager Manager;

        public HoldRig(string name) {
            var tree = (SceneTree)Engine.GetMainLoop();
            Host = new Node2D { Name = name };
            tree.Root.AddChild(Host);
            StoryManager.Instance?.SetRewinds(3);
            Player = CharacterFactory.CreateCharacter("einstein", 0);
            Host.AddChild(Player);
            Player.GlobalPosition = new Vector2(400f, 850f);
            Manager = new ChronalRewindManager { Name = name + "Manager" };
            Host.AddChild(Manager);
        }

        public T Add<T>(T node, string group) where T : Node {
            Host.AddChild(node);
            if (!string.IsNullOrEmpty(group)) node.AddToGroup(group);
            return node;
        }

        public void Record(int frames) {
            for (int frame = 0; frame < frames; frame++) {
                Player.GlobalPosition = new Vector2(400f + frame * 4f, 850f);
                Manager._PhysicsProcess(Step);
            }
        }

        /// <summary>Kills the hero and plays the rewind out to the landing.</summary>
        public void DieAndLand() {
            Player.ApplyDamage(Player.CurrentHP);
            AssertThat(Manager.IsRewinding).IsTrue();
            for (int i = 0; i < 900 && Manager.IsRewinding; i++) Manager._PhysicsProcess(Step);
            AssertThat(Manager.IsRewinding).IsFalse();
        }

        public void Tick(int frames) {
            for (int frame = 0; frame < frames; frame++) Manager._PhysicsProcess(Step);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Host)) Host.Free();
            StoryManager.Instance?.SetRewinds(3);
        }
    }

    // === R02 ===============================================================

    [TestCase]
    public void AStoryRecoveryNeverEntersOrExitsRespawning() {
        using var rig = new HoldRig("RespawningNeverHost");
        var transitions = new List<(CharacterState From, CharacterState To)>();
        void Record(CharacterState from, CharacterState to) => transitions.Add((from, to));
        rig.Player.StateTransitioned += Record;
        try {
            rig.Record(60);
            rig.DieAndLand();
            rig.Tick(ChronalRewindManager.PostLandingHoldFrames);

            AssertThat(transitions.Any(t => t.From == CharacterState.Respawning || t.To == CharacterState.Respawning))
                .OverrideFailureMessage("R02: Respawning is Fighter-only; a Story recovery entered or left it.")
                .IsFalse();
            AssertThat(transitions.Any(t => t.From == CharacterState.Dead && t.To == CharacterState.Idle))
                .OverrideFailureMessage("The Story revive edge must be Dead -> Idle for a grounded landing.")
                .IsTrue();
            AssertThat(rig.Player.CurrentState).IsEqual(CharacterState.Idle);

        } finally {
            rig.Player.StateTransitioned -= Record;
        }

        // The same edge picks Airborne for an ungrounded landing sample.
        var bare = new PlayerController { CurrentHP = 0 };
        var bareTransitions = new List<(CharacterState From, CharacterState To)>();
        bare.StateTransitioned += (from, to) => bareTransitions.Add((from, to));
        try {
            bare.TransitionTo(CharacterState.Dead);
            bare.CompleteStoryRewind(new Vector2(500f, 300f), 40, grounded: false);
            AssertThat(bare.CurrentState).IsEqual(CharacterState.Airborne);
            AssertThat(bareTransitions.Any(t => t.From == CharacterState.Respawning || t.To == CharacterState.Respawning))
                .IsFalse();
            // The edge is open only during the placement: an ordinary transition
            // still cannot raise the dead.
            bare.TransitionTo(CharacterState.Dead);
            bare.TransitionTo(CharacterState.Idle);
            AssertThat(bare.CurrentState).IsEqual(CharacterState.Dead);
        } finally {
            bare.Free();
        }
    }

    // === R03: the frozen set ================================================

    [TestCase]
    public void TheHoldFreezesTheUnionOfTheTimeFreezeAndRewindSetsForSixtyFrames() {
        // Plan D3(a): the union — so Extractors, the Mirror and the hero's own
        // constructs are frozen too, which Time Freeze alone deliberately skips.
        var union = ChronalRewindManager.PostLandingHoldGroups.ToList();
        foreach (string group in ChronalRewindManager.FrozenSimulationGroups) AssertThat(union.Contains(group)).IsTrue();
        foreach (string group in TimeFreezeController.FrozenTimeGroups) AssertThat(union.Contains(group)).IsTrue();
        AssertThat(union.Contains("chronal_extractor")).IsTrue();
        AssertThat(union.Contains("persistent_construct")).IsTrue();
        AssertThat(union.Contains(MirrorParadoxController.MirrorGroup)).IsTrue();

        using var rig = new HoldRig("UnionFrozenSetHost");
        var enemy = rig.Add(new EnemyController { Name = "HoldEnemy" }, "Enemies");
        var hazard = rig.Add(new HoldFreezeProbe { Name = "HoldHazard" }, "story_hazard");
        var loot = rig.Add(new HoldFreezeProbe { Name = "HoldLoot" }, "story_loot");
        var puzzle = rig.Add(new HoldFreezeProbe { Name = "HoldPuzzle" }, "puzzle_object");
        var extractor = rig.Add(new HoldLatchProbe { Name = "HoldExtractor" }, "chronal_extractor");
        var construct = rig.Add(new HoldLatchProbe { Name = "HoldConstruct" }, "persistent_construct");

        rig.Record(30);
        rig.DieAndLand();
        AssertThat(rig.Manager.IsPostLandingHoldActive).IsTrue();
        HoldFreezeProbe late = null;
        for (int frame = 0; frame < ChronalRewindManager.PostLandingHoldFrames - 1; frame++) {
            if (frame == 20) {
                // A room revealed mid-hold freezes before its actors can act.
                late = rig.Add(new HoldFreezeProbe { Name = "RevealedMidHold" }, "story_hazard");
                TimeFreezeController.FreezeActivatedRoom(rig.Host.GetTree());
                AssertThat(late.IsFrozen).IsTrue();
            }
            AssertThat(enemy.IsStoryRewindFrozen && hazard.IsFrozen && loot.IsFrozen && puzzle.IsFrozen
                    && extractor.IsLatched && construct.IsLatched)
                .OverrideFailureMessage($"A member of the union thawed early at hold frame {frame}.")
                .IsTrue();
            rig.Tick(1);
        }
        rig.Tick(1);
        AssertThat(rig.Manager.IsPostLandingHoldActive).IsFalse();
        AssertThat(enemy.IsStoryRewindFrozen || hazard.IsFrozen || loot.IsFrozen || puzzle.IsFrozen
                || extractor.IsLatched || construct.IsLatched || late.IsFrozen)
            .OverrideFailureMessage("The thaw must release the whole union.")
            .IsFalse();
    }

    // === R03: no credit =====================================================

    [TestCase]
    public void FrozenActorsAreInvulnerableAndGiveNoCreditDuringTheHold() {
        using var rig = new HoldRig("NoCreditHost");
        var hurtbox = rig.Add(new Hurtbox { Name = "FrozenTargetHurtbox", OwnerPlayerIndex = -1 }, null);
        int delivered = 0;
        hurtbox.OnHit += _ => { delivered++; return 7f; };
        // A checkpoint fracture / Extractor strike surface is the same Hurtbox path.
        var strikeSurface = rig.Add(new EnvironmentHurtboxAdapter { Name = "StrikeSurface", OwnerPlayerIndex = -1 }, null);
        int strikes = 0;
        strikeSurface.OnHit += _ => { strikes++; return 1f; };
        var payload = new HitPayload { AttackerIndex = 0, Damage = 7f, AttackClass = AttackClass.Basic };

        rig.Record(30);
        rig.DieAndLand();
        AssertThat(ChronalRewindManager.IsWorldHeld(rig.Host.GetTree())).IsTrue();
        float meterBefore = rig.Player.CurrentUltimateMeter;
        AssertThat(hurtbox.TakeHit(payload)).IsEqual(0f);
        AssertThat(strikeSurface.TakeHit(payload)).IsEqual(0f);
        AssertThat(delivered)
            .OverrideFailureMessage("A frozen actor received a hit during the hold.").IsEqual(0);
        AssertThat(strikes)
            .OverrideFailureMessage("A checkpoint/extractor strike landed during the hold.").IsEqual(0);
        AssertThat(rig.Player.CurrentUltimateMeter).IsEqual(meterBefore);

        rig.Tick(ChronalRewindManager.PostLandingHoldFrames);
        AssertThat(ChronalRewindManager.IsWorldHeld(rig.Host.GetTree())).IsFalse();
        AssertThat(hurtbox.TakeHit(payload)).IsEqual(7f);
        AssertThat(delivered).IsEqual(1);
    }

    // === R03: world actions wait for the thaw ===============================

    [TestCase]
    public void TimeFreezeInteractionAndPuzzleControlsWaitForTheThaw() {
        AssertThat(TimeFreezeController.CanActivate(CharacterState.Idle, false, 0f, true, worldHeld: true)).IsFalse();
        AssertThat(TimeFreezeController.CanActivate(CharacterState.Idle, false, 0f, true, worldHeld: false)).IsTrue();

        using var rig = new HoldRig("BlockedActionsHost");
        StoryManager.Instance?.SetTimeFreezeCooldown(0f);
        var freeze = rig.Add(new TimeFreezeController { Name = "HoldTimeFreeze" }, null);
        freeze.SetPhysicsProcess(false);
        var target = rig.Add(new HoldInteractableProbe { Name = "HoldFont" }, null);
        var area = new InteractionArea { Name = "HoldInteraction" };
        target.AddChild(area);

        rig.Record(30);
        rig.DieAndLand();
        AssertThat(rig.Player.IsRecoveryWorldHeld).IsTrue();
        AssertThat(freeze.TryBeginTimeFreeze())
            .OverrideFailureMessage("Time Freeze activated during the Post-Landing Hold.").IsFalse();
        AssertThat(area.TryInteract(rig.Player))
            .OverrideFailureMessage("An interaction (Font / puzzle control) landed during the hold.").IsFalse();
        AssertThat(target.Interactions).IsEqual(0);

        rig.Tick(ChronalRewindManager.PostLandingHoldFrames);
        AssertThat(rig.Player.IsRecoveryWorldHeld).IsFalse();
        AssertThat(area.TryInteract(rig.Player)).IsTrue();
        AssertThat(target.Interactions).IsEqual(1);
        AssertThat(freeze.TryBeginTimeFreeze()).IsTrue();
        freeze.EndFreeze(early: true);
        StoryManager.Instance?.SetTimeFreezeCooldown(0f);
    }

    // === R03: clocks =========================================================

    [TestCase]
    public void EveryClockRunsThroughTheHold() {
        using var rig = new HoldRig("ClocksRunHost");
        StoryManager.Instance?.SetTimeFreezeCooldown(10f);
        var freeze = rig.Add(new TimeFreezeController { Name = "HoldCooldownClock" }, null);
        freeze.SetPhysicsProcess(false);
        try {
            AssertThat(freeze.CooldownRemaining).IsEqual(10f);
            rig.Record(30);
            rig.Player.ApplyDamage(rig.Player.CurrentHP);
            // The death-rewind PRESENTATION pauses Integrity and the cooldown...
            AssertThat((StoryManager.Instance.IntegrityClockPauseScopes & IntegrityClockPause.DeathRewind) != 0).IsTrue();
            freeze._PhysicsProcess(Step);
            AssertThat(freeze.CooldownRemaining).IsEqual(10f);
            for (int i = 0; i < 900 && rig.Manager.IsRewinding; i++) rig.Manager._PhysicsProcess(Step);

            // ...but the hold is live play: Integrity drains, the cooldown runs,
            // and the hero's own clocks are not stopped.
            AssertThat(rig.Manager.IsPostLandingHoldActive).IsTrue();
            AssertThat((StoryManager.Instance.IntegrityClockPauseScopes & IntegrityClockPause.DeathRewind) != 0)
                .OverrideFailureMessage("Integrity must drain during the Post-Landing Hold.").IsFalse();
            for (int frame = 0; frame < 30; frame++) {
                freeze._PhysicsProcess(Step);
                rig.Tick(1);
            }
            AssertThat(freeze.CooldownRemaining).IsEqualApprox(9.5f, 0.05f);
            AssertThat(rig.Player.TimeFrozen)
                .OverrideFailureMessage("The hero's cooldowns, regen and statuses must run through the hold.")
                .IsFalse();
        } finally {
            StoryManager.Instance?.SetTimeFreezeCooldown(0f);
        }
    }

    // === R03: protection from the thaw =======================================

    [TestCase]
    public void TheTwoSecondProtectionStartsAtTheThawNotTheLanding() {
        using var rig = new HoldRig("ProtectionFromThawHost");
        rig.Record(30);
        rig.DieAndLand();
        AssertThat(rig.Player.IsPostRewindInvulnerable)
            .OverrideFailureMessage("Protection started at the landing.").IsFalse();
        rig.Tick(ChronalRewindManager.PostLandingHoldFrames - 1);
        AssertThat(rig.Player.IsPostRewindInvulnerable).IsFalse();
        rig.Tick(1);
        AssertThat(rig.Player.IsPostRewindInvulnerable)
            .OverrideFailureMessage("Protection must begin on the thaw tick.").IsTrue();
        AssertThat(PlayerController.StoryRewindInvulnerabilityFrames).IsEqual(120);
    }

    // === R03: events, thaw cue and the music duck ============================

    [TestCase]
    public void LandedAndThawedEventsBracketTheHoldAndTheThawCuePlaysJustBefore() {
        using var rig = new HoldRig("HoldEventsHost");
        var log = new List<string>();
        var holdPayloads = new List<RecoveryHoldPayload>();
        var presentations = new List<RewindPresentationPayload>();
        void Landed(RecoveryHoldPayload p) { log.Add("landed"); holdPayloads.Add(p); }
        void Thawed(RecoveryHoldPayload p) { log.Add("thawed"); holdPayloads.Add(p); }
        void Presented(RewindPresentationPayload p) => presentations.Add(p);
        EventBus.Instance.OnRecoveryLanded += Landed;
        EventBus.Instance.OnRecoveryWorldThawed += Thawed;
        EventBus.Instance.OnRewindPresentation += Presented;
        try {
            rig.Record(30);
            rig.DieAndLand();
            AssertThat(string.Join(",", log)).IsEqual("landed");
            AssertThat(holdPayloads[0].Cause).IsEqual(StoryRecoveryHoldCause.DeathRewind);
            AssertThat(holdPayloads[0].HoldFrames).IsEqual(ChronalRewindManager.PostLandingHoldFrames);
            AssertThat(holdPayloads[0].PlayerIndex).IsEqual(rig.Player.PlayerIndex);
            // The recovery duck is still held through the hold.
            RewindPresentationPayload landedPresentation = presentations.Last();
            AssertThat(landedPresentation.Phase).IsEqual(RewindPresentationPhase.Landed);
            AssertThat(landedPresentation.MusicDuckDecibels).IsEqualApprox(-12f, 0.001f);

            rig.Tick(ChronalRewindManager.PostLandingHoldFrames - ChronalRewindManager.PostLandingThawWarningFrames - 1);
            AssertThat(rig.Manager.ThawWarningsIssued).IsEqual(0);
            rig.Tick(1);
            AssertThat(rig.Manager.ThawWarningsIssued)
                .OverrideFailureMessage("The reused Time Freeze thaw warning must fire just before the thaw.")
                .IsEqual(1);
            AssertThat(string.Join(",", log)).IsEqual("landed");

            rig.Tick(ChronalRewindManager.PostLandingThawWarningFrames);
            AssertThat(string.Join(",", log)).IsEqual("landed,thawed");
            AssertThat(holdPayloads[1].Cause).IsEqual(StoryRecoveryHoldCause.DeathRewind);
            // The duck ends at the thaw.
            RewindPresentationPayload thawed = presentations.Last();
            AssertThat(thawed.Phase).IsEqual(RewindPresentationPhase.Thawed);
            AssertThat(thawed.MusicDuckDecibels).IsEqual(0f);
        } finally {
            EventBus.Instance.OnRecoveryLanded -= Landed;
            EventBus.Instance.OnRecoveryWorldThawed -= Thawed;
            EventBus.Instance.OnRewindPresentation -= Presented;
        }
    }

    [TestCase]
    public void ReducedTemporalEffectsKeepsTheThawWarning() {
        bool original = ComfortSettings.ReducedTemporalEffects;
        ComfortSettings.Apply(true);
        try {
            using var rig = new HoldRig("ComfortThawHost");
            rig.Record(30);
            rig.DieAndLand();
            rig.Tick(ChronalRewindManager.PostLandingHoldFrames);
            AssertThat(rig.Manager.ThawWarningsIssued)
                .OverrideFailureMessage("C01a: the thaw warning is a readability cue and survives the preset.")
                .IsEqual(1);
        } finally {
            ComfortSettings.Apply(original);
        }
    }

    // === R03: boss ordering ==================================================

    [TestCase]
    public void APendingBossTransitionPlaysOnlyAfterTheThaw() {
        using var rig = new HoldRig("BossAfterThawHost");
        BossController boss = BossSuspensionFixture.CreateRecoveringBoss(rig.Host);
        rig.Record(30);
        for (int tick = 0; tick < 200; tick++) boss._PhysicsProcess((float)Step);
        boss.TakeDamage(400);
        AssertThat(boss.IsHistoricalRecoverySuspended).IsTrue();
        AssertThat(rig.Manager.IsBossSuspensionActive).IsTrue();
        int beatLeft = boss.HistoricalRecoverySuspendFramesRemaining;

        // The hero dies inside the beat: the recovery owns the world first.
        rig.DieAndLand();
        AssertThat(rig.Manager.IsBossSuspensionActive).IsFalse();
        for (int frame = 0; frame < ChronalRewindManager.PostLandingHoldFrames - 1; frame++) {
            boss._PhysicsProcess((float)Step);
            rig.Tick(1);
        }
        AssertThat(boss.HistoricalRecoverySuspendFramesRemaining)
            .OverrideFailureMessage("The boss's beat advanced during the hold.")
            .IsEqual(beatLeft);
        rig.Tick(1);
        AssertThat(rig.Manager.IsPostLandingHoldActive).IsFalse();
        AssertThat(rig.Manager.IsBossSuspensionActive)
            .OverrideFailureMessage("The still-valid boss transition must play after the thaw.")
            .IsTrue();
    }

    // === R03: the in-session placement holds =================================

    [TestCase]
    public void TheAnchorSnapAndCollapseResumePlacementsEndInTheSameHold() {
        using var rig = new HoldRig("PlacementHoldHost");
        var enemy = rig.Add(new EnemyController { Name = "PlacementEnemy" }, "Enemies");
        var extractor = rig.Add(new HoldLatchProbe { Name = "PlacementExtractor" }, "chronal_extractor");
        var landed = new List<StoryRecoveryHoldCause>();
        void Landed(RecoveryHoldPayload p) => landed.Add(p.Cause);
        EventBus.Instance.OnRecoveryLanded += Landed;
        try {
            foreach (StoryRecoveryHoldCause cause in new[] {
                         StoryRecoveryHoldCause.AnchorSnap, StoryRecoveryHoldCause.CollapseResume }) {
                AssertThat(rig.Manager.BeginPlacementHold(cause)).IsTrue();
                AssertThat(rig.Manager.PostLandingHoldCause).IsEqual(cause);
                AssertThat(enemy.IsStoryRewindFrozen && extractor.IsLatched).IsTrue();
                AssertThat(rig.Player.IsPostRewindInvulnerable).IsFalse();
                // A second placement cannot stack a second hold.
                AssertThat(rig.Manager.BeginPlacementHold(cause)).IsFalse();
                rig.Tick(ChronalRewindManager.PostLandingHoldFrames);
                AssertThat(enemy.IsStoryRewindFrozen || extractor.IsLatched).IsFalse();
                AssertThat(rig.Player.IsPostRewindInvulnerable).IsTrue();
            }
            AssertThat(string.Join(",", landed)).IsEqual("AnchorSnap,CollapseResume");
        } finally {
            EventBus.Instance.OnRecoveryLanded -= Landed;
        }
    }

    // === R03: persistence ====================================================

    [TestCase]
    public void TheHoldIsTransientAndAReloadNeverGrantsOne() {
        // Nothing about the hold is a save field.
        string[] persisted = typeof(StorySaveData).GetFields().Select(f => f.Name)
            .Concat(typeof(StorySaveData).GetProperties().Select(p => p.Name))
            .Concat(typeof(StoryAttemptState).GetFields().Select(f => f.Name))
            .Concat(typeof(StoryAttemptState).GetProperties().Select(p => p.Name))
            .ToArray();
        AssertThat(persisted.Any(name => name.Contains("Hold", StringComparison.OrdinalIgnoreCase)))
            .OverrideFailureMessage("The Post-Landing Hold must never be persisted.")
            .IsFalse();

        StoryManager story = StoryManager.Instance;
        SaveManager saves = SaveManager.Instance;
        const int scratchSlot = 2;
        StorySaveData original = saves.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        CampaignLevel originalLevel = story.CurrentLevel;
        story.SuppressSceneLoadsForTesting = true;
        try {
            // In session: an Acts I-II collapse resumed from its anchor arms a
            // CollapseResume hold for exactly that level's scene, consumed once.
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            saves.SaveSlots[scratchSlot] = new StorySaveData { SelectedCharacterID = "einstein" };
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.BeginLevelRun();
            story.BeginTimelineCollapse("orleans_checkpoint_1", TimelineCollapseCause.Death);
            AssertThat(story.HasPendingRecoveryPlacementHold).IsFalse();
            AssertThat(story.RestartCollapsedLevel(resumeFromTimelineAnchor: true)).IsTrue();
            AssertThat(story.PendingRecoveryPlacementHold).IsEqual(StoryRecoveryHoldCause.CollapseResume);
            AssertThat(story.TryConsumeRecoveryPlacementHold("res://scenes/campaign/HubWorld.tscn", out _))
                .OverrideFailureMessage("A hold is consumed only by the scene it was armed for.").IsFalse();
            AssertThat(story.TryConsumeRecoveryPlacementHold(story.GetCurrentLevelPath(), out StoryRecoveryHoldCause cause))
                .IsTrue();
            AssertThat(cause).IsEqual(StoryRecoveryHoldCause.CollapseResume);
            AssertThat(story.TryConsumeRecoveryPlacementHold(story.GetCurrentLevelPath(), out _)).IsFalse();

            // A fresh restart from the beginning is not a recovery placement.
            story.BeginTimelineCollapse("orleans_checkpoint_1", TimelineCollapseCause.Death);
            AssertThat(story.RestartCollapsedLevel(resumeFromTimelineAnchor: false)).IsTrue();
            AssertThat(story.HasPendingRecoveryPlacementHold).IsFalse();

            // Reload: a save parked mid-level loads without any hold, even when
            // the session had one armed.
            // The Act III Anchor Snap arms its own in-session hold.
            story.PrepareDirectLevel(CampaignLevel.NeoEarth, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;
            story.BeginLevelRun();
            story.BeginAnchorSnap("level_14_neo_earth_checkpoint_1");
            AssertThat(story.PendingRecoveryPlacementHold).IsEqual(StoryRecoveryHoldCause.AnchorSnap);
            var parked = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.NeoEarth),
                LastCheckpointID = "level_14_neo_earth_checkpoint_1"
            };
            parked.AttemptState = StoryAttemptState.CreateFresh("level_14_neo_earth", 2);
            saves.SaveSlots[scratchSlot] = parked;
            story.ResumeCampaign(scratchSlot, parked);
            AssertThat(story.HasPendingRecoveryPlacementHold)
                .OverrideFailureMessage("Plan D3(a): a reload never grants a Post-Landing Hold.")
                .IsFalse();
        } finally {
            story.SuppressSceneLoadsForTesting = false;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            saves.SaveSlots[scratchSlot] = original;
            story.PrepareDirectLevel(originalLevel, "einstein", Difficulty.Normal);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }
}
