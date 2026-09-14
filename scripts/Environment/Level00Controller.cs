using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Level 0 - Chronal Integration tutorial. Three documented parts: the
    /// fracture presentation, guided calibration against a training dummy, and
    /// advanced mobility gates followed by the holographic combat trial. The
    /// calibration step order and gating live in
    /// <see cref="TutorialCalibrationScript"/> (audit M-3).
    ///
    /// <para><b>V7.6 rebuild (Package 11 A5).</b> Under the V7.5 Legacy Unlock
    /// Schedule the hero owns basics, Block, Rally, the death rewind, Time
    /// Freeze and the Ultimate Meter at Level 0 — and nothing else. The Special,
    /// Ultimate and Movement-Ability calibrations are therefore <b>deleted</b>
    /// (they demanded abilities that do not exist yet), and three beats take
    /// their place: <b>Grab Calibration</b> against a shielded dummy, <b>Hitstun
    /// Agency Calibration</b> (DI, then a mandatory landing tech that repeats
    /// until it lands), and <b>Meter &amp; Defy History</b>, which fills the
    /// meter and spends it refusing a scripted lethal hit with the F13 seal
    /// going lit → broken on screen. Part 3's traversal is authored for base
    /// jump reach alone; its movement-ability corridor is gone.</para>
    /// </summary>
    public partial class Level00Controller : Node2D {
        private enum TutorialPhase { Fracture, Calibration, Mobility, CombatTrial, Complete }

        private PlayerController _player;
        private TrainingDummy _dummy;
        private StorySceneServices _services;
        private LevelManager _levelManager;

        private TutorialPhase _phase = TutorialPhase.Fracture;
        private readonly TutorialCalibrationScript _calibration = new();

        private Area2D _doubleJumpGate;
        private Area2D _rollGate;
        private bool _doubleJumpGateCleared;
        private bool _rollGateCleared;

        // === V7.6 Grab Calibration ======================================
        // The grab reaches EnemyControllers in the "Enemies" group, not the
        // TrainingDummy, so the shielded target is a standard-tier drone parked
        // beside the dummy. It is held in the rewind-freeze state — passive,
        // never attacking — and its "shield" is a guard flag this controller
        // enforces by restoring the HP every swing takes off, which is exactly
        // the designed read: everything the player swings is absorbed, and the
        // prompt keeps pointing at the grab.
        private EnemyController _grabDummy;
        private int _grabDummyFullHP;
        private bool _grabGuardUp;
        private bool _grabConnected;
        private ColorRect _grabGuardVisual;

        // === V7.6 Hitstun Agency Calibration =============================
        /// <summary>Knockback of the scripted launch, in authored hit-payload units/frame.</summary>
        internal static readonly Vector2 ScriptedLaunchKnockback = new(3.2f, -7.5f);
        /// <summary>Hitstun the scripted launch applies, in seconds.</summary>
        internal const float ScriptedLaunchHitstunSeconds = 0.75f;
        /// <summary>
        /// Tutorial-only extended hitstop on the launch. The shared table gives a
        /// normal hit 3-6 frames; 36 frames (0.6 s) is long enough to read the DI
        /// prompt and commit a direction, and it exists nowhere outside Level 0.
        /// </summary>
        internal const int TutorialLaunchHitstopFrames = 36;
        /// <summary>One extra freeze on the FIRST tech attempt: the "slowed approach".</summary>
        internal const int FirstTechApproachFreezeFrames = 24;
        /// <summary>Frames between a missed tech and the next free launch.</summary>
        private const int LaunchRetryDelayFrames = 90;

        private int _launchCountdownFrames = -1;
        private bool _launchInFlight;
        private bool _firstApproachFreezeSpent;

        // === V7.6 Meter & Defy History Calibration =======================
        /// <summary>Frames the lit seal is held before the scripted lethal hit lands.</summary>
        private const int DefyBeatDelayFrames = 120;
        private int _defyCountdownFrames = -1;

        private int _enemiesKilled;
        private int _totalEnemies;
        private bool _levelComplete;

        public override void _Ready() {
            BuildLevel();
            SpawnPlayer();

            _levelManager = new LevelManager {
                Name = "LevelManager",
                LevelID = "level_00_tutorial",
                LevelDisplayName = "tutorial_level_title"
            };
            AddChild(_levelManager);
            _levelManager.SetCheckpointPosition("l00_start", new Vector2(400, 850));

            _services = StorySceneBootstrapper.Attach(
                this, "res://resources/Dialogue/level_00_dialogue.tres",
                audioSetPath: AudioSetPaths.Tutorial);
            _services.HUD?.SetLevelTitle("tutorial_level_title");
            _services.HUD?.SetObjective("tutorial_objective_fracture");

            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
                EventBus.Instance.OnDialogueComplete += OnDialogueComplete;
                EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
                EventBus.Instance.OnBlockAbsorbed += OnBlockAbsorbed;
                EventBus.Instance.OnBlockBroken += OnBlockBroken;
            }

            // Defer one frame so the dialogue UI is fully in the tree before pausing.
            CallDeferred(MethodName.StartFracturePresentation);
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
                EventBus.Instance.OnDialogueComplete -= OnDialogueComplete;
                EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
                EventBus.Instance.OnBlockAbsorbed -= OnBlockAbsorbed;
                EventBus.Instance.OnBlockBroken -= OnBlockBroken;
            }
            if (_dummy != null && IsInstanceValid(_dummy)) _dummy.HitLanded -= OnDummyHit;
            // Pooled combat-trial enemies are parented here; hand them back or the
            // pool keeps freed references once this scene unloads.
            PoolManager.Instance?.ReleaseActiveUnder(this);
        }

        // === Part 1: fracture presentation ===

        private void StartFracturePresentation() {
            _services?.Dialogue?.StartSequence("level_00.intro");
        }

        private void OnDialogueComplete(string dialogueID) {
            switch (dialogueID) {
                case "level_00.intro":
                    BeginCalibration();
                    break;
                case "level_00.block_intro":
                    BeginBlockLesson();
                    break;
                // V7.6: the sequence ID is retained (V7.5 identifier retention)
                // but its beat is now the Meter & Defy History calibration —
                // the meter is taught as the fuel that refuses death, not as
                // the fuel for a cast the hero has not earned yet. A6 rewrites
                // its English against that.
                case "level_00.ultimate_intro":
                    BeginMeterAndDefyLesson();
                    break;
                case "level_00.mobility_intro":
                    BeginMobilityGates();
                    break;
                case "level_00.complete":
                    ShowCompletionResults();
                    break;
            }
        }

        // === Part 2: calibration ===

        private void BeginCalibration() {
            _phase = TutorialPhase.Calibration;

            _dummy = new TrainingDummy { Name = "CalibrationDummy", Position = new Vector2(1000, 850) };
            _dummy.HitLanded += OnDummyHit;
            AddChild(_dummy);

            _services.HUD?.SetObjective("tutorial_step_attack", 0, TutorialCalibrationScript.RequiredBasicHits);
        }

        private void OnDummyHit(int damage) {
            if (_phase != TutorialPhase.Calibration) return;
            // V7.1 Rally beat: striking back after the dummy's scripted hit
            // reclaims the echo and closes the lesson.
            if (_calibration.Step == TutorialCalibrationStep.RallyReclaim) {
                if (_calibration.RegisterRallyReclaimHit()) {
                    if (_dummy != null && IsInstanceValid(_dummy)) _dummy.EndScriptedAttacks();
                    _services.Dialogue?.StartSequence("level_00.block_intro");
                }
                return;
            }
            bool advanced = _calibration.RegisterBasicHit();
            if (_calibration.Step == TutorialCalibrationStep.BasicHits) {
                _services.HUD?.SetObjective("tutorial_step_attack",
                    _calibration.BasicHitsLanded, TutorialCalibrationScript.RequiredBasicHits);
            }
            if (advanced) BeginRallyLesson();
        }

        /// <summary>
        /// V7.1 Rally calibration: the dummy lands one scripted hit so part of
        /// the blow lingers as an echo, and the prompt teaches the reclaim —
        /// "strike back before it fades to reclaim it."
        /// </summary>
        private void BeginRallyLesson() {
            if (_dummy != null && IsInstanceValid(_dummy)) _dummy.BeginScriptedAttacks(_player);
            _services.HUD?.SetObjective("tutorial_step_rally");
        }

        /// <summary>Arms the dummy's telegraphed swipe so the player can practice blocking.</summary>
        private void BeginBlockLesson() {
            if (_dummy != null && IsInstanceValid(_dummy)) _dummy.BeginScriptedAttacks(_player);
            _services.HUD?.SetObjective("tutorial_step_block",
                _calibration.HitsBlocked, TutorialCalibrationScript.RequiredBlockedHits);
        }

        private void OnBlockAbsorbed(int playerIndex, int remainingCharges) {
            if (_phase != TutorialPhase.Calibration || playerIndex != 0) return;
            bool advanced = _calibration.RegisterBlockedHit();
            if (_calibration.Step == TutorialCalibrationStep.Block) {
                _services.HUD?.SetObjective("tutorial_step_block",
                    _calibration.HitsBlocked, TutorialCalibrationScript.RequiredBlockedHits);
            }
            if (advanced) CompleteBlockLesson();
        }

        private void OnBlockBroken(int playerIndex) {
            if (_phase != TutorialPhase.Calibration || playerIndex != 0) return;
            // A guard break is the complete depletion lesson in one stroke.
            if (_calibration.RegisterGuardBreak()) CompleteBlockLesson();
        }

        private void CompleteBlockLesson() {
            if (_dummy != null && IsInstanceValid(_dummy)) _dummy.EndScriptedAttacks();
            BeginGrabLesson();
        }

        // === V7.6 Grab Calibration ======================================

        /// <summary>
        /// Parks a standard-tier drone beside the training dummy, freezes it so
        /// it never fights back, and raises its "shield": while the guard is up
        /// every point of damage the player deals is restored, so swings are
        /// visibly absorbed and the prompt keeps pointing at the grab.
        /// "A raised shield stops a blade, not a hand."
        /// </summary>
        private void BeginGrabLesson() {
            _grabDummy = EnemyFactory.SpawnHologramDrone(this, new Vector2(1160, 850));
            if (_grabDummy != null) {
                _grabDummyFullHP = _grabDummy.CurrentHP;
                // The rewind-freeze state is the existing "inert but present"
                // mode: no AI, no attacks, no motion. It is released the moment
                // the grab connects so the throw plays out normally.
                _grabDummy.SetStoryRewindFrozen(true);
                _grabGuardUp = true;
                _grabGuardVisual = new ColorRect {
                    Name = "GrabLessonGuard",
                    Size = new Vector2(56, 92),
                    Position = new Vector2(-28, -92),
                    Color = new Color(0.35f, 0.75f, 1f, 0.4f),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                _grabDummy.AddChild(_grabGuardVisual);
            }
            _services.HUD?.SetObjective("tutorial_step_grab");
        }

        /// <summary>
        /// Grab-lesson bookkeeping. Runs every physics frame while the step is
        /// live: restores the guarded HP (the absorb), releases the freeze when
        /// the grab connects, and closes the lesson when the throw resolves.
        /// </summary>
        private void ProcessGrabLesson() {
            if (_calibration.Step != TutorialCalibrationStep.Grab) return;
            if (_grabDummy == null || !IsInstanceValid(_grabDummy)) {
                // Never strand the tutorial on a missing drone.
                if (_calibration.RegisterGrabThrow()) CompleteGrabLesson();
                return;
            }

            if (_grabGuardUp && _grabDummy.CurrentHP < _grabDummyFullHP) {
                _grabDummy.CurrentHP = _grabDummyFullHP;
                _calibration.RegisterGrabLessonSwingAbsorbed();
                // Re-prompt: the player is swinging at a raised shield.
                _services.HUD?.SetObjective("tutorial_step_grab_absorbed");
            }

            if (!_grabConnected && _player != null && IsInstanceValid(_player)
                && _player.GrabbedEnemy == _grabDummy && _player.GrabPhase >= 4) {
                _grabConnected = true;
                _grabGuardUp = false;
                _grabDummy.SetStoryRewindFrozen(false);
                if (_grabGuardVisual != null && IsInstanceValid(_grabGuardVisual)) {
                    _grabGuardVisual.QueueFree();
                    _grabGuardVisual = null;
                }
            }

            if (_grabConnected && (_player == null || !IsInstanceValid(_player) || _player.GrabPhase == 0)) {
                if (_calibration.RegisterGrabThrow()) CompleteGrabLesson();
            }
        }

        private void CompleteGrabLesson() {
            // "Grabs beat blocks. Strikes beat grabs." — the follow-up names the
            // rule both ways before the next beat starts.
            _services.HUD?.SetObjective("tutorial_step_grab_complete");
            BeginHitstunAgencyLesson();
        }

        // === V7.6 Hitstun Agency Calibration ============================

        /// <summary>
        /// Arms the first free scripted launch. Everything about it is the real
        /// verb layer — the stashed impulse, the ±15° DI read at hitstop expiry,
        /// the tumble, the landing tech — except that it costs the player
        /// nothing: no HP, no Rally echo, no meter, no rewind charge.
        /// </summary>
        private void BeginHitstunAgencyLesson() {
            _services.HUD?.SetObjective("tutorial_step_di");
            _launchCountdownFrames = 60;
        }

        private void ProcessHitstunAgencyLesson() {
            if (_player == null || !IsInstanceValid(_player)) return;
            if (_calibration.Step is not (TutorialCalibrationStep.HitstunDI
                or TutorialCalibrationStep.LandingTech)) return;

            if (_launchCountdownFrames > 0) {
                _launchCountdownFrames--;
                return;
            }
            if (_launchCountdownFrames == 0) {
                _launchCountdownFrames = -1;
                _launchInFlight = true;
                _firstApproachFreezeSpent = _calibration.ScriptedLaunchesDelivered > 0;
                _player.ApplyTutorialScriptedLaunch(
                    ScriptedLaunchKnockback, ScriptedLaunchHitstunSeconds, TutorialLaunchHitstopFrames);
                return;
            }
            if (!_launchInFlight) return;

            if (_calibration.Step == TutorialCalibrationStep.HitstunDI) {
                // The extended tutorial freeze has run out, so the DI read has
                // resolved — whether or not a direction was held. The beat
                // proceeds either way; the lesson is that the option exists.
                if (_player.HitstopFramesRemaining > 0) return;
                if (_calibration.RegisterDirectionalInfluenceBeat()) {
                    _services.HUD?.SetObjective("tutorial_step_tech");
                }
                return;
            }

            // Landing tech. The first attempt gets the designed "slowed
            // approach": one extra freeze as the fall steepens, bought with the
            // existing hitstop mechanism rather than a global time scale.
            if (!_firstApproachFreezeSpent && !_player.IsOnFloor() && _player.Velocity.Y > 180f) {
                _firstApproachFreezeSpent = true;
                _player.ApplyHitstop(FirstTechApproachFreezeFrames);
                return;
            }
            if (_player.HitstopFramesRemaining > 0) return;

            if (_player.IsInTechLockout) {
                _launchInFlight = false;
                if (_calibration.RegisterLandingTech()) CompleteHitstunAgencyLesson();
                return;
            }
            // Landed (or hitstun simply ran out) without a tech: play the full
            // knockdown and repeat. The launch is free, so a miss costs nothing.
            if (_player.CurrentState != CharacterState.Stunned && _player.IsOnFloor()) {
                _launchInFlight = false;
                if (!_calibration.RegisterLandingTechMissed()) return;
                _services.HUD?.SetObjective("tutorial_step_tech_retry");
                _launchCountdownFrames = LaunchRetryDelayFrames;
            }
        }

        private void CompleteHitstunAgencyLesson() {
            _services.Dialogue?.StartSequence("level_00.ultimate_intro");
        }

        // === V7.6 Meter & Defy History Calibration ======================

        /// <summary>
        /// Fills the Influence Meter to 100% and lights the F13 Defy seal, then
        /// arms the scripted lethal-tagged hit that Defy History refuses. No
        /// extra meter award, no extra Defy use: the calibration spends exactly
        /// the one the player is being shown.
        /// </summary>
        private void BeginMeterAndDefyLesson() {
            if (_player == null || !IsInstanceValid(_player)) return;
            if (_player.StoryDefyHistoryUsed) {
                // A resumed attempt already spent it; the lesson closes on its
                // coaching text rather than stranding the player.
                if (_calibration.SkipDefyLesson()) BeginRewindDemonstration();
                return;
            }
            // A scripted meter grant, not a landed hit — never a Rally reclaim.
            _player.AddInfluenceFromDamageDealt(FTT.Combat.UltimateMeter.MaxValue, collectsEcho: false);
            PublishDefySeal(DefySealState.Ready);
            _services.HUD?.SetObjective("tutorial_step_defy");
            _defyCountdownFrames = DefyBeatDelayFrames;
        }

        private void ProcessMeterAndDefyLesson() {
            if (_calibration.Step != TutorialCalibrationStep.MeterAndDefy) return;
            if (_defyCountdownFrames < 0) return;
            if (_defyCountdownFrames > 0) {
                _defyCountdownFrames--;
                return;
            }
            _defyCountdownFrames = -1;
            if (_player == null || !IsInstanceValid(_player)) {
                if (_calibration.SkipDefyLesson()) BeginRewindDemonstration();
                return;
            }
            // The lethal-tagged hit. ApplyEnvironmentalDamage is the V7.3
            // chokepoint: it runs the whole victim-side pipeline, so Defy
            // History fires and consumes its own flag rather than leaking into
            // the next hit's accounting.
            _player.ApplyEnvironmentalDamage(_player.CurrentHP);
            PublishDefySeal(_player.StoryDefyHistoryUsed ? DefySealState.Spent : DefySealState.Building);
            _services.HUD?.SetObjective("tutorial_step_defy_spent");
            if (_calibration.RegisterDefyProc()) BeginRewindDemonstration();
        }

        /// <summary>
        /// A5 publishes the F13 seal for the tutorial's forced states. A1b takes
        /// over as the general publisher in Wave 2 (plan §2.9); the payload and
        /// the enum are shared, so nothing here changes when it does.
        /// </summary>
        private void PublishDefySeal(DefySealState state) {
            EventBus.Instance?.RaiseDefySealChanged(new DefySealPayload {
                PlayerIndex = 0,
                State = state
            });
        }

        /// <summary>
        /// Hands off to the scripted death-rewind demonstration (A2's region
        /// picks up from the manual lesson that follows it).
        /// </summary>
        private void BeginRewindDemonstration() {
            _services.HUD?.SetObjective("tutorial_step_rewind");
            // Rewind is a death-save, not an input the player can perform, so the
            // tutorial demonstrates it: hold the objective on screen briefly, then
            // run a scripted rewind through the real manager.
            _rewindDemoCountdownFrames = RewindDemoDelayFrames;
        }

        private const int RewindDemoDelayFrames = 90;
        private int _rewindDemoCountdownFrames = -1;
        private int _rewindDemoAttempts;

        private void ProcessRewindDemo() {
            if (_phase != TutorialPhase.Calibration || _calibration.Step != TutorialCalibrationStep.UseRewind) return;
            if (_rewindDemoCountdownFrames < 0) return;
            if (_rewindDemoCountdownFrames > 0) {
                _rewindDemoCountdownFrames--;
                return;
            }
            if (_services.RewindManager != null && _services.RewindManager.TriggerScriptedRewind()) {
                // OnRewindTriggered advances the step once playback completes.
                _rewindDemoCountdownFrames = -1;
                return;
            }
            _rewindDemoAttempts++;
            if (_services.RewindManager == null || _rewindDemoAttempts >= 3) {
                // Never strand the tutorial: skip the demonstration if it cannot run.
                _rewindDemoCountdownFrames = -1;
                _calibration.SkipRewindDemonstration();
                _mobilityIntroPending = true;
            } else {
                _rewindDemoCountdownFrames = 60;
            }
        }

        private bool _mobilityIntroPending;

        private void OnRewindTriggered(Vector2 _) {
            if (_phase != TutorialPhase.Calibration) return;
            if (!_calibration.RegisterRewindComplete()) return;
            // The guided demonstration never spends the player's real pool.
            _services.RewindManager?.RefundRewind();
            BeginTimeFreezeDrill();
        }

        // === V7.6 Time Freeze escape drill (Package 11 A2) ====================
        //
        // The design asks for two separate time beats, in this order: (a) the
        // scripted DEATH REWIND demonstration above, which teaches the finite
        // pool and that the last charge still saves you, then (b) this five-second
        // ESCAPE DRILL — an invulnerable enemy the player cannot fight through and
        // a safe destination they can only reach with the world stopped.
        //
        // The drill provides a ready freeze on EVERY retry (DrillFreeFreeze) and
        // consumes no death-rewind charge, so a failed attempt never strands the
        // tutorial behind a 45-second wait. Outside this drill there is no
        // recharge exemption anywhere in the game.

        private Area2D _timeFreezeDrillGate;
        private FTT.Enemies.EnemyController _timeFreezeDrillBlocker;

        private void BeginTimeFreezeDrill() {
            if (_services.TimeFreeze == null || !IsInstanceValid(_services.TimeFreeze)) {
                // Never strand the tutorial: with no controller there is no drill.
                _calibration.SkipTimeFreezeLesson();
                _mobilityIntroPending = true;
                return;
            }
            _services.TimeFreeze.DrillFreeFreeze = true;
            _services.HUD?.SetObjective("tutorial_step_time_freeze");

            // The blocker paces the corridor mouth. It is unkillable for the
            // duration of the lesson, so the only way past is to stop time.
            _timeFreezeDrillBlocker = FTT.Enemies.EnemyFactory.Spawn(
                "chrono_slasher", this, new Vector2(1180, 850),
                new Vector2(1120, 850), new Vector2(1260, 850));
            if (_timeFreezeDrillBlocker != null && IsInstanceValid(_timeFreezeDrillBlocker)) {
                _timeFreezeDrillBlocker.DrillInvulnerable = true;
            }

            // The safe destination, past the blocker.
            _timeFreezeDrillGate = BuildGateZone(
                "TimeFreezeDrillGate",
                new Vector2(1420, 850),
                new Vector2(120, 180),
                new Color(0.4f, 0.85f, 1f, 0.35f),
                "tutorial_step_time_freeze");
        }

        /// <summary>
        /// Completion rule: the player used a Time Freeze during the drill AND
        /// reached the safe destination. Polled from <c>_PhysicsProcess</c>.
        /// </summary>
        private void ProcessTimeFreezeDrill() {
            if (_phase != TutorialPhase.Calibration
                || _calibration.Step != TutorialCalibrationStep.UseTimeFreeze) return;
            if (_services.TimeFreeze == null || !IsInstanceValid(_services.TimeFreeze)) return;
            if (_services.TimeFreeze.IsFrozen) _timeFreezeDrillUsed = true;
            if (!_timeFreezeDrillUsed || !ZoneContainsPlayer(_timeFreezeDrillGate)) return;
            if (!_calibration.RegisterTimeFreezeComplete()) return;
            MarkGateCleared(_timeFreezeDrillGate);
            EndTimeFreezeDrill();
            _mobilityIntroPending = true;
        }

        private bool _timeFreezeDrillUsed;

        private void EndTimeFreezeDrill() {
            // The exemption dies with the lesson: normal 45 s cooldown from here.
            if (_services.TimeFreeze != null && IsInstanceValid(_services.TimeFreeze)) {
                _services.TimeFreeze.DrillFreeFreeze = false;
            }
            if (_timeFreezeDrillBlocker != null && IsInstanceValid(_timeFreezeDrillBlocker)) {
                _timeFreezeDrillBlocker.DrillInvulnerable = false;
                if (FTT.Core.PoolManager.Instance != null) {
                    FTT.Core.PoolManager.Instance.Release(_timeFreezeDrillBlocker);
                } else {
                    _timeFreezeDrillBlocker.QueueFree();
                }
                _timeFreezeDrillBlocker = null;
            }
        }

        public override void _Process(double delta) {
            if (!_mobilityIntroPending) return;
            if (_services.RewindManager != null && _services.RewindManager.IsRewinding) return;
            _mobilityIntroPending = false;
            _services.Dialogue?.StartSequence("level_00.mobility_intro");
        }

        // === Part 3: mobility gates ===

        private void BeginMobilityGates() {
            _phase = TutorialPhase.Mobility;
            _services.HUD?.SetObjective("tutorial_step_double_jump");

            AddCheckpoint("l00_checkpoint_mobility", new Vector2(1500, 850));

            // Double-jump gate: a marker above a platform stack only reachable
            // with a full jump chain.
            //
            // Both hops are sized against Lincoln, the roster's floor: he clears
            // 93.75 px on one jump and 187.5 px on two (force 10.0 × 54 px/s
            // against 18 × (0.8 + 0.4 × 1.6) base gravity), so a 128 px step
            // demands the second jump and still leaves ~46% headroom. The stack
            // used to ask for 188 px from the floor and another 240 px on top,
            // which no character has ever cleared — the five single-jump kits
            // could not even reach the lower platform, and the 2026-08-10 jump
            // retune (feel batch §2.2) would have left it unclearable for
            // everyone. See that plan's §9.
            BuildPlatform(1700, 780, 200);
            BuildPlatform(1900, 650, 180);
            _doubleJumpGate = BuildGateZone("DoubleJumpGate", new Vector2(1900, 570), new Vector2(160, 140),
                new Color(0.2f, 0.9f, 0.5f, 0.25f), "tutorial_gate_double_jump");

            // V7.6: the movement-ability corridor is DELETED. The Movement
            // Ability unlocks after Level 1 under the Legacy Unlock Schedule, so
            // a Level 0 gate demanding it was an unclearable wall. Part 3's
            // traversal is authored for base jump reach alone; the ability gets
            // its own optional Wren drill when it is actually granted.

            // Roll gate: a marked strip the player must cross while rolling.
            _rollGate = BuildGateZone("RollGate", new Vector2(3100, 850), new Vector2(200, 110),
                new Color(0.4f, 0.6f, 1f, 0.25f), "tutorial_gate_roll");
        }

        private Area2D BuildGateZone(string name, Vector2 position, Vector2 size, Color color, string labelKey) {
            var zone = new Area2D {
                Name = name,
                Position = position,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            var col = new CollisionShape2D();
            col.Shape = new RectangleShape2D { Size = size };
            zone.AddChild(col);

            var visual = new ColorRect {
                Size = size,
                Position = -size / 2f,
                Color = color,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            zone.AddChild(visual);

            var label = new Label {
                Text = Tr(labelKey),
                Position = new Vector2(-size.X / 2f, -size.Y / 2f - 26),
                CustomMinimumSize = new Vector2(size.X, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color(color.R, color.G, color.B, 0.95f));
            zone.AddChild(label);

            AddChild(zone);
            return zone;
        }

        public override void _PhysicsProcess(double delta) {
            ProcessRewindDemo();
            if (_phase == TutorialPhase.Calibration) {
                // V7.6 beats. Each is phase-gated inside itself, so the order
                // here is documentation, not control flow.
                ProcessGrabLesson();
                ProcessHitstunAgencyLesson();
                ProcessMeterAndDefyLesson();
            ProcessTimeFreezeDrill();
            }
            if (_phase != TutorialPhase.Mobility || _player == null || !IsInstanceValid(_player)) return;

            if (!_doubleJumpGateCleared && ZoneContainsPlayer(_doubleJumpGate)) {
                _doubleJumpGateCleared = true;
                MarkGateCleared(_doubleJumpGate);
                _services.HUD?.SetObjective("tutorial_step_roll");
            }
            // V7.6: double jump then roll — the movement-ability corridor that
            // used to sit between them is gone.
            if (_doubleJumpGateCleared && !_rollGateCleared && ZoneContainsPlayer(_rollGate)
                && _player.CurrentState == CharacterState.Rolling) {
                _rollGateCleared = true;
                MarkGateCleared(_rollGate);
                BeginCombatTrial();
            }
        }

        private bool ZoneContainsPlayer(Area2D zone) {
            if (zone == null || !IsInstanceValid(zone)) return false;
            Godot.Collections.Array<Node2D> bodies = zone.GetOverlappingBodies();
            using var bodiesLifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body == _player) return true;
            }
            return false;
        }

        private static void MarkGateCleared(Area2D zone) {
            Godot.Collections.Array<Node> children = zone.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is ColorRect visual) visual.Color = new Color(0.2f, 0.9f, 0.4f, 0.45f);
            }
        }

        private void AddCheckpoint(string checkpointID, Vector2 position) {
            var trigger = new CheckpointTrigger {
                Name = checkpointID,
                CheckpointID = checkpointID,
                Position = position,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            var col = new CollisionShape2D();
            col.Shape = new RectangleShape2D { Size = new Vector2(60, 200) };
            trigger.AddChild(col);

            var visual = new ColorRect {
                Size = new Vector2(8, 120),
                Position = new Vector2(-4, -120),
                Color = new Color(0f, 0.9f, 0.9f, 0.5f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            trigger.AddChild(visual);
            AddChild(trigger);
        }

        // === Combat trial ===

        private void BeginCombatTrial() {
            _phase = TutorialPhase.CombatTrial;
            AddCheckpoint("l00_checkpoint_trial", new Vector2(3400, 850));
            SpawnWave1();
        }

        private void SpawnWave1() {
            _totalEnemies = StoryDifficultyTuning.ScaleEncounterCount(5, StoryDifficultyTuning.CurrentStoryDifficulty);
            _enemiesKilled = 0;
            UpdateWaveObjective("tutorial_objective_progress");

            Vector2[] spawns = {
                new(1200, 850), new(1800, 850), new(2400, 850), new(3000, 700), new(3600, 850)
            };
            for (int index = 0; index < _totalEnemies && index < spawns.Length; index++) {
                if (index == 2) {
                    EnemyFactory.SpawnHologramDrone(this, spawns[index],
                        waypointA: spawns[index] + new Vector2(-200, 0), waypointB: spawns[index] + new Vector2(200, 0));
                } else {
                    EnemyFactory.SpawnHologramDrone(this, spawns[index]);
                }
            }
            _totalEnemies = Mathf.Min(_totalEnemies, spawns.Length);
        }

        private void SpawnWave2() {
            _totalEnemies = StoryDifficultyTuning.ScaleEncounterCount(4, StoryDifficultyTuning.CurrentStoryDifficulty);
            _enemiesKilled = 0;

            Vector2[] spawns = { new(1500, 850), new(2000, 550), new(2800, 850), new(3200, 850) };
            for (int index = 0; index < _totalEnemies && index < spawns.Length; index++) {
                if (index == 3) {
                    EnemyFactory.SpawnChronoSlasher(this, spawns[index],
                        waypointA: spawns[index] + new Vector2(-200, 0), waypointB: spawns[index] + new Vector2(200, 0));
                } else {
                    EnemyFactory.SpawnHologramDrone(this, spawns[index]);
                }
            }
            _totalEnemies = Mathf.Min(_totalEnemies, spawns.Length);
            _waveTwoActive = true;
            UpdateWaveObjective("tutorial_objective_wave_two");
        }

        private bool _waveTwoActive;

        private void OnEnemyKilled(EnemyKilledPayload payload) {
            if (_phase != TutorialPhase.CombatTrial || _levelComplete) return;
            _enemiesKilled++;
            UpdateWaveObjective("tutorial_objective_progress");

            if (_enemiesKilled >= _totalEnemies) {
                if (!_waveTwoActive) SpawnWave2();
                else CompleteTutorial();
            }
        }

        private void UpdateWaveObjective(string key) {
            _services.HUD?.SetObjective(key, _enemiesKilled, _totalEnemies);
        }

        private void CompleteTutorial() {
            _levelComplete = true;
            _phase = TutorialPhase.Complete;

            if (StoryManager.Instance != null) StoryManager.Instance.TutorialComplete = true;
            // CompleteLevel raises OnLevelComplete, which advances the campaign
            // level and autosaves completion; do not also advance manually.
            _levelManager?.CompleteLevel();
            _services.HUD?.SetObjective("tutorial_objective_complete");
            _services.Dialogue?.StartSequence("level_00.complete");
        }

        private void ShowCompletionResults() {
            _services?.Audio?.ReleaseToAmbient();
            var results = LevelResultsPanel.CreateDefault();
            results.ReturnRequested += () => StoryManager.Instance?.ReturnToHub();
            AddChild(results);
            results.ShowResults("tutorial_level_title", StoryManager.Instance?.ChronalDustCollected ?? 0);
        }

        // === Level construction ===

        private void BuildLevel() {
            var bg = new ColorRect();
            bg.Name = "Background";
            bg.Size = new Vector2(4800, 1080);
            bg.Color = new Color(0.03f, 0.05f, 0.12f);
            AddChild(bg);

            BuildFloor(0, 900, 4800);

            BuildPlatform(300, 700, 250);
            BuildPlatform(700, 550, 200);
            BuildPlatform(1100, 700, 250);
            BuildPlatform(1500, 600, 220);
            BuildPlatform(2300, 550, 250);
            BuildPlatform(2700, 680, 220);
            BuildPlatform(3500, 720, 250);
            BuildPlatform(4000, 550, 200);

            BuildWall(0, 0, 1080);
            BuildWall(4780, 0, 1080);

            BuildFracturePresentation();
            BuildDecoration();
        }

        /// <summary>
        /// Part 1's ignition beat — the V7.6 two-colour visual grammar's opening
        /// statement (design §2 "The Visual Grammar of Resonance"; Package 11
        /// A6b fills in the treatment A5 left room for).
        ///
        /// <para>What this used to be: a single violet/purple <c>ColorRect</c>
        /// pulsing on a loop. A generic rift. It said nothing about who did this
        /// or what it did to the hero, and violet belongs to neither half of the
        /// grammar — the language leaked before it was ever established.</para>
        ///
        /// <para>The authored beat is four moves, in this order:</para>
        /// <list type="number">
        /// <item>A <b>cold beam cracks the nexus</b> — the Unbound's synthetic
        /// light, a jagged tear rather than a clean opening.</item>
        /// <item><b>Gold ignition on the hero</b> — history's resonance answers,
        /// warm, from the era into a person.</item>
        /// <item>The <b>beam breaks</b> — the cold recoils and guts out.</item>
        /// <item>The <b>Warden portal opens beside the Unbound rift</b>. This is
        /// the contract the whole campaign leans on afterwards: <b>two kinds of
        /// door</b>. Unbound rifts are jagged cold tears; Warden portals are
        /// clean steady geometry. Establishing them side by side, once, in the
        /// opening minutes is what makes every later door legible at a glance
        /// without a line of dialogue.</item>
        /// </list>
        ///
        /// <para>Placeholder <c>ColorRect</c> / <c>PointLight2D</c> treatment,
        /// consistent with the era-placeholder bar the rest of the campaign
        /// meets — the <i>grammar</i> is the deliverable, not the art. Every
        /// pigment comes from <see cref="FTT.UI.UIPalette"/>'s grammar block;
        /// <c>ResonanceGrammarTests</c> fails if this beat reintroduces a local
        /// literal or any violet.</para>
        /// </summary>
        private void BuildFracturePresentation() {
            var rift = new Node2D {
                Name = "ChronalFracture",
                Position = new Vector2(250, 700),
                // The intro dialogue pauses the tree, and the beat has to play
                // underneath it rather than waiting for it to finish.
                ProcessMode = ProcessModeEnum.Always
            };
            AddChild(rift);

            // --- Beat 1: the cold beam, a JAGGED tear -----------------------
            // Jaggedness is geometry, not colour: offset shards of differing
            // width and height, never a single clean column. A Warden portal
            // (below) is built from the opposite vocabulary on purpose.
            var beam = new Node2D {
                Name = "UnboundBeam",
                // Starts low so beat 1 reads as the beam BITING rather than as
                // something that was always there.
                Modulate = new Color(1f, 1f, 1f, 0.18f)
            };
            rift.AddChild(beam);

            float[] shardOffsets = { -18f, -4f, 9f, 21f };
            float[] shardHeights = { 150f, 196f, 168f, 128f };
            float[] shardWidths = { 12f, 18f, 10f, 14f };
            for (int index = 0; index < shardOffsets.Length; index++) {
                beam.AddChild(new ColorRect {
                    Name = $"ColdShard{index}",
                    Size = new Vector2(shardWidths[index], shardHeights[index]),
                    Position = new Vector2(shardOffsets[index], -shardHeights[index] * 0.55f),
                    Color = FTT.UI.UIPalette.UnboundColdDischarge,
                    RotationDegrees = index % 2 == 0 ? -4f : 5f
                });
            }

            var coldHalo = new ColorRect {
                Name = "ColdHalo",
                Size = new Vector2(96, 232),
                Position = new Vector2(-48, -116),
                Color = FTT.UI.UIPalette.UnboundColdDim
            };
            rift.AddChild(coldHalo);
            rift.MoveChild(coldHalo, 0);

            var label = new Label {
                Name = "FractureLabel",
                Text = Tr("tutorial_fracture_label"),
                Position = new Vector2(-90, -150),
                CustomMinimumSize = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", FTT.UI.UIPalette.UnboundCold);
            rift.AddChild(label);

            // --- Beat 2: the gold ignition on the hero ----------------------
            // Sited at the player's start anchor, not at the rift: the grammar
            // says gold flows FROM the era INTO its people, so the warm light
            // has to originate on the hero, answering the cold rather than
            // emanating from the same wound.
            var ignition = new Node2D {
                Name = "ResonanceIgnition",
                Position = new Vector2(150, 200),
                Modulate = new Color(1f, 1f, 1f, 0f),
                ProcessMode = ProcessModeEnum.Always
            };
            rift.AddChild(ignition);
            ignition.AddChild(new ColorRect {
                Name = "IgnitionCore",
                Size = new Vector2(34, 34),
                Position = new Vector2(-17, -17),
                Color = FTT.UI.UIPalette.ResonanceGoldBright
            });
            ignition.AddChild(new ColorRect {
                Name = "IgnitionHalo",
                Size = new Vector2(96, 96),
                Position = new Vector2(-48, -48),
                Color = new Color(FTT.UI.UIPalette.ResonanceGold, 0.3f)
            });

            // --- Beat 4: the Warden portal, CLEAN STEADY geometry -----------
            // Built beside the rift, deliberately: the comparison is the point.
            // An even frame, concentric, square to the world, no rotation and no
            // pulse. Nothing about it is torn.
            var portal = new Node2D {
                Name = "WardenPortal",
                Position = new Vector2(420, 0),
                Modulate = new Color(1f, 1f, 1f, 0f),
                ProcessMode = ProcessModeEnum.Always
            };
            rift.AddChild(portal);
            portal.AddChild(new ColorRect {
                Name = "PortalFrame",
                Size = new Vector2(84, 180),
                Position = new Vector2(-42, -104),
                Color = new Color(FTT.UI.UIPalette.WardenPortal, 0.45f)
            });
            portal.AddChild(new ColorRect {
                Name = "PortalCore",
                Size = new Vector2(60, 156),
                Position = new Vector2(-30, -92),
                Color = FTT.UI.UIPalette.WardenPortal
            });

            // --- The beat timeline ------------------------------------------
            // One chained tween so the order is authored in one readable place
            // rather than spread across four timers.
            Tween beat = rift.CreateTween();
            // 1. The beam bites: cold flares to full over the first second.
            beat.TweenProperty(beam, "modulate:a", 1f, 0.9);
            // 2. Gold answers on the hero.
            beat.TweenProperty(ignition, "modulate:a", 1f, 0.7)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            // 3. The beam breaks — the cold guts out rather than fading evenly.
            beat.TweenProperty(beam, "modulate:a", 0.28f, 0.5)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            beat.Parallel().TweenProperty(coldHalo, "modulate:a", 0.35f, 0.5);
            // 4. The Warden door opens beside the tear. Steady: a linear fade,
            //    no overshoot, no bounce — it behaves as unlike the rift as it
            //    looks.
            beat.TweenProperty(portal, "modulate:a", 1f, 0.8)
                .SetTrans(Tween.TransitionType.Linear);

            // The broken beam keeps a slow cold flicker afterwards: the wound is
            // still open, which is the level's whole premise. The portal is NOT
            // animated — steady geometry is half its definition.
            Tween flicker = rift.CreateTween().SetLoops();
            flicker.TweenInterval(3.0);
            flicker.TweenProperty(beam, "modulate:a", 0.16f, 1.1);
            flicker.TweenProperty(beam, "modulate:a", 0.34f, 1.1);
        }

        private void BuildFloor(float x, float y, float width) {
            var floor = new StaticBody2D();
            floor.CollisionLayer = CollisionLayers.Environment;
            floor.CollisionMask = 0;
            floor.Name = "Floor";
            floor.Position = new Vector2(x + width / 2f, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(width, 40);
            col.Shape = shape;
            col.Position = new Vector2(0, 20);
            floor.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(width, 40);
            visual.Position = new Vector2(-width / 2f, 0);
            visual.Color = new Color(0.1f, 0.15f, 0.25f);
            floor.AddChild(visual);

            var gridLine = new ColorRect();
            gridLine.Size = new Vector2(width, 2);
            gridLine.Position = new Vector2(-width / 2f, 0);
            gridLine.Color = new Color(0.0f, 0.6f, 0.8f, 0.4f);
            floor.AddChild(gridLine);

            AddChild(floor);
        }

        private void BuildPlatform(float x, float y, float width) {
            var plat = new StaticBody2D();
            plat.CollisionLayer = CollisionLayers.Environment;
            plat.CollisionMask = 0;
            plat.Name = $"Platform_{x}";
            plat.Position = new Vector2(x, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(width, 16);
            col.Shape = shape;
            plat.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(width, 16);
            visual.Position = new Vector2(-width / 2f, -8);
            visual.Color = new Color(0.08f, 0.12f, 0.22f);
            plat.AddChild(visual);

            var glow = new ColorRect();
            glow.Size = new Vector2(width - 10, 2);
            glow.Position = new Vector2(-(width - 10) / 2f, -8);
            glow.Color = new Color(0.0f, 0.7f, 0.9f, 0.3f);
            plat.AddChild(glow);

            AddChild(plat);
        }

        private void BuildWall(float x, float y, float height) {
            var wall = new StaticBody2D();
            wall.CollisionLayer = CollisionLayers.Environment;
            wall.CollisionMask = 0;
            wall.Name = $"Wall_{x}";
            wall.Position = new Vector2(x, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(20, height);
            col.Shape = shape;
            col.Position = new Vector2(10, height / 2f);
            wall.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(20, height);
            visual.Color = new Color(0.06f, 0.08f, 0.15f);
            wall.AddChild(visual);

            AddChild(wall);
        }

        private void BuildDecoration() {
            var title = new Label();
            title.Name = "LevelTitle";
            title.Text = Tr("tutorial_environment_title");
            title.Position = new Vector2(1800, 30);
            title.CustomMinimumSize = new Vector2(500, 30);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f, 0.7f));
            AddChild(title);

            for (int i = 0; i < 8; i++) {
                var gridPanel = new ColorRect();
                gridPanel.Size = new Vector2(3, 200);
                gridPanel.Position = new Vector2(600 * i, 700);
                gridPanel.Color = new Color(0.0f, 0.4f, 0.5f, 0.15f);
                AddChild(gridPanel);
            }
        }

        private void SpawnPlayer() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";

            _player = CharacterFactory.CreateCharacter(characterID, 0);
            _player.Name = "Player";
            _player.Position = new Vector2(400, 850);
            AddChild(_player);

            var camera = new Camera2D();
            camera.Name = "PlayerCamera";
            camera.Enabled = true;
            camera.LimitLeft = 0;
            camera.LimitRight = 4800;
            camera.LimitTop = 0;
            camera.LimitBottom = 1080;
            camera.PositionSmoothingEnabled = true;
            camera.PositionSmoothingSpeed = 5.0f;
            _player.AddChild(camera);
        }
    }
}
