using System;
using System.Collections.Generic;
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
    ///
    /// <para><b>The Combat Trial is an encounter lock</b> (gameplay update
    /// 2026-10-12, GU-L0): one standalone <see cref="EncounterLock"/> over the
    /// bay's east end (<see cref="TrialLockMinX"/>–<see cref="CalibrationBayEastWallX"/>),
    /// built when the trial starts, its two waves spawning as the hero walks in
    /// and once the first is down. Its clear completes the tutorial, replacing the
    /// global kill counter that also counted the grab lesson's drone (now
    /// released after its lesson). The roll gate, the trial anchor and the
    /// Restoration Font moved west of the lock. The hold stays scripted, and the
    /// Time Freeze drill stays exempt: its point is passing an enemy that cannot
    /// be killed.</para>
    /// </summary>
    public partial class Level00Controller : Node2D {
        /// <summary>
        /// Package 13 W3 (S02): Part 1 is now a fight. <c>Hold</c> is beat 5 (the
        /// scripted, unlosable hold), <c>Rift</c> is beats 6–7 (the beam breaks,
        /// the watcher, the walk to the Warden rift), <c>Arrival</c> is Part 2's
        /// mandatory dialogue and Wren's Full / Skip offer; the calibration
        /// phases after it run only on Full.
        /// </summary>
        public enum TutorialPhase { Hold, Rift, Arrival, Calibration, Mobility, CombatTrial, Complete }

        private PlayerController _player;
        private TrainingDummy _dummy;
        private StorySceneServices _services;
        private LevelManager _levelManager;

        private TutorialPhase _phase = TutorialPhase.Hold;

        /// <summary>The current tutorial phase. Test seam.</summary>
        public TutorialPhase Phase => _phase;
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
        /// <summary>Frames the objective is read before the drone swarm comes.</summary>
        private const int DefyBeatDelayFrames = 120;
        private int _defyCountdownFrames = -1;
        private readonly List<EnemyController> _defyDrones = new();
        private bool _defySwarmActive;
        private static readonly Vector2[] DefySwarmOffsets = {
            new Vector2(-220f, -40f), new Vector2(220f, -40f), new Vector2(340f, -40f)
        };

        private bool _levelComplete;
        private readonly List<StaticBody2D> _openingSurfaces = new();
        private readonly List<CanvasItem> _studyHiddenItems = new();
        private StoryOpeningPresentation _studyOpening;
        private Tween _fractureIntroBeat;
        private readonly List<CanvasItem> _studyHiddenHeroVisuals = new();
        public const double StudyOpeningSeconds = 2.5;
        public bool StudyOpeningActive => _studyOpening != null && IsInstanceValid(_studyOpening);

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
                EventBus.Instance.OnDefyExecuted += OnDefyExecuted;
            }

            BeginStudyOpening();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
                EventBus.Instance.OnDialogueComplete -= OnDialogueComplete;
                EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
                EventBus.Instance.OnBlockAbsorbed -= OnBlockAbsorbed;
                EventBus.Instance.OnBlockBroken -= OnBlockBroken;
                EventBus.Instance.OnDefyExecuted -= OnDefyExecuted;
            }
            if (_dummy != null && IsInstanceValid(_dummy)) _dummy.HitLanded -= OnDummyHit;
            // The hold's seams are scene-scoped: never leave a floor or a player
            // freeze behind (the player is ours, but be explicit).
            if (_player != null && IsInstanceValid(_player)) {
                _player.ScriptedHPFloor = 0;
                _player.DefyDisabledByScript = false;
            }
            ResetHoldLocalPresentation();
            ReleaseDefyDrones();
            // Pooled combat-trial enemies are parented here; hand them back or the
            // pool keeps freed references once this scene unloads.
            PoolManager.Instance?.ReleaseActiveUnder(this);
        }

        // === Package 13 W3: Part 1 beat 5 — the hold (S02) ===================
        //
        // The beam's pull stalls: the Wardens have caught it. Visored locals of
        // the hero's own era walk out of the drained world to drag the hero into
        // the column, and an unnamed voice ("???", no portrait — Sarah, recognized
        // in Part 2) coaches the fight: the 3-hit string, then Block against one
        // telegraphed swing that repeats until it is blocked. It cannot be lost
        // (HP floor 1) and nothing here uses or grants rewind charges, Rally,
        // meter, dust or Integrity: the locals are summoned (no F05 award, and
        // Level 0's ledger pays nothing anyway), the tutorial has no Integrity
        // clock, and the meter and echo the fight would leave are handed back
        // every physics frame. Step order lives in Level00HoldFight.

        private readonly Level00HoldFight _hold = new();
        private readonly System.Collections.Generic.List<EnemyController> _holdLocals = new();
        private EnemyController _guardLocal;
        private int _guardTelegraphFrames = -1;
        private bool _guardSwingBlocked;
        private float _holdMeterBaseline;
        private Area2D _riftGate;
        private TwoOptionPrompt _calibrationPrompt;

        /// <summary>The hold's step. Test seam.</summary>
        public HoldFightStep HoldStep => _hold.Step;

        /// <summary>The Full / Skip prompt, while Wren's offer is open. Test seam.</summary>
        public TwoOptionPrompt CalibrationPrompt => _calibrationPrompt;

        /// <summary>The calibration script. Test seam.</summary>
        internal TutorialCalibrationScript CalibrationScript => _calibration;

        /// <summary>Where the hold's locals walk out of the tear.</summary>
        public static readonly Vector2[] HoldLocalSpawns = {
            new(980, 850), new(1160, 850), new(1340, 850)
        };

        /// <summary>Where the Translation lands the hero in the Calibration Bay.</summary>
        public static readonly Vector2 BayArrivalPosition = new(820, 850);

        /// <summary>The Eraser watcher's time on screen (S10: "about 2 s").</summary>
        public const float WatcherSeconds = 2f;

        /// <summary>The hold's scripted swing: small, Basic-class, blockable.</summary>
        public const float GuardSwingDamage = 5f;

        // === Part 3 double-jump gate geometry (2026-10-08) ===================

        /// <summary>The top of the Calibration Bay floor (BuildFloor's collision starts here).</summary>
        public const float TutorialFloorTopY = 900f;
        /// <summary>The Calibration Bay's size (the camera's limits and the trial lock's level bounds).</summary>
        public const float BayWidth = 4800f;
        public const float BayHeight = 1080f;
        /// <summary>BuildPlatform's slab thickness.</summary>
        public const float TutorialPlatformThickness = 16f;
        /// <summary>The double-jump gate's first step: centre (1700, 780), top 772 — a 128 px single hop.</summary>
        public const float DoubleJumpFirstStepX = 1700f;
        public const float DoubleJumpFirstStepY = 780f;
        /// <summary>
        /// The gated step: centre (1900, 540), top 532 — 240 px above the first
        /// step's top, beyond every hero's single jump and inside every double jump.
        /// The gate zone sits directly on it.
        /// </summary>
        public const float DoubleJumpGatedStepX = 1900f;
        public const float DoubleJumpGatedStepY = 540f;

        private bool _holdStarted;

        /// <summary>The quiet study precedes the existing extraction and hold fight.</summary>
        private void BeginStudyOpening() {
            if (!StudyOpeningActive) { CallDeferred(MethodName.StartHoldFight); return; }
            _player.ProcessMode = ProcessModeEnum.Disabled;
            if (_player.Data?.CharacterID == "einstein") {
                Texture2D study = ResourceLoader.Load<Texture2D>("res://assets/environments/mockup_a/story/level_00_study.png");
                if (study != null) {
                    Sprite2D plate = _studyOpening.GetNode<Sprite2D>("RegisteredPlate");
                    plate.Texture = study;
                    plate.Scale = _studyOpening.Profile.Frame.Size / study.GetSize();
                    foreach (string name in new[] { "AnimatedSprite2D", "NameLabel" }) {
                        if (_player.GetNodeOrNull<CanvasItem>(name) is CanvasItem visual && visual.Visible) {
                            _studyHiddenHeroVisuals.Add(visual);
                            visual.Hide();
                        }
                    }
                }
            }
            foreach (Node child in GetChildren()) {
                if (child is CanvasItem item && item != _player && item != _studyOpening && item.Visible) {
                    _studyHiddenItems.Add(item);
                    item.Hide();
                }
            }
            _fractureIntroBeat?.Pause();
            var timer = GetTree().CreateTimer(StudyOpeningSeconds, processAlways: false);
            timer.Timeout += () => {
                if (IsInstanceValid(this) && IsInsideTree()) StartHoldFight();
            };
        }

        private void EndStudyOpening() {
            if (!StudyOpeningActive) return;
            _studyOpening.Deactivate();
            _studyOpening = null;
            foreach (CanvasItem item in _studyHiddenItems) {
                if (IsInstanceValid(item)) item.Show();
            }
            _studyHiddenItems.Clear();
            foreach (CanvasItem visual in _studyHiddenHeroVisuals) {
                if (IsInstanceValid(visual)) visual.Show();
            }
            _studyHiddenHeroVisuals.Clear();
            _player.ProcessMode = ProcessModeEnum.Inherit;
            _player.GlobalPosition = new Vector2(400, 850);
            StoryCameraConfiner.FindOn(_player)?.SnapToTarget();
            _fractureIntroBeat?.Play();
        }

        /// <summary>Beat 5 begins: the HP floor goes on and the unnamed voice speaks.</summary>
        public void StartHoldFight() {
            if (_holdStarted || _phase != TutorialPhase.Hold) return;
            EndStudyOpening();
            _holdStarted = true;
            if (_player != null && IsInstanceValid(_player)) {
                _player.ScriptedHPFloor = Level00HoldFight.HeroHPFloor;
                // P21: the unlosable hold fight does not offer Defy.
                _player.DefyDisabledByScript = true;
                _holdMeterBaseline = _player.CurrentUltimateMeter;
            }
            _services?.HUD?.SetObjective("tutorial_objective_hold");
            // The voice speaks first; the locals come when it finishes. A missing
            // dialogue stack must never strand the beat.
            if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.hold_start")) {
                BeginHoldLocals();
            }
        }

        private void BeginHoldLocals() {
            if (!_hold.BeginFight()) return;
            string heroID = _player?.Data?.CharacterID ?? "";
            string mob = Level00HoldFight.LocalMobFor(heroID);
            for (int index = 0; index < Level00HoldFight.LocalCount && index < HoldLocalSpawns.Length; index++) {
                EnemyController local = EnemyFactory.SpawnSummoned(mob, this, HoldLocalSpawns[index]);
                if (local == null) continue;
                _holdLocals.Add(local);
            }
            if (_holdLocals.Count == 0) {
                // Never strand the story: no locals, no fight, the beam still breaks.
                _hold.ForceComplete();
                CompleteHold();
            }
        }

        /// <summary>
        /// Every physics frame of the hold: hand back the meter and Rally echo the
        /// fight would otherwise leave behind, and drive the Guard lesson's
        /// telegraphed swing.
        /// </summary>
        private void ProcessHoldFight() {
            if (_phase != TutorialPhase.Hold) return;
            if (_player != null && IsInstanceValid(_player)) {
                _player.ClearRallyEcho();
                if (_player.CurrentUltimateMeter > _holdMeterBaseline) {
                    _player.DrainUltimateMeter(_player.CurrentUltimateMeter - _holdMeterBaseline);
                }
            }
            if (_hold.Step != HoldFightStep.Guard || _guardTelegraphFrames < 0) return;
            if (_guardLocal == null || !IsInstanceValid(_guardLocal)) {
                if (_hold.ForceComplete()) CompleteHold();
                return;
            }
            // Telegraph: pulse the local toward the Basic warning tint so the
            // wind-up reads, exactly like the calibration dummy's swipe.
            float progress = 1f - (float)_guardTelegraphFrames / Level00HoldFight.GuardTelegraphFrames;
            float pulse = 0.5f + 0.5f * Mathf.Sin(progress * Mathf.Pi * 4f);
            _guardLocal.Modulate = Colors.White.Lerp(new Color(1f, 0.85f, 0.3f), 0.6f * pulse + 0.2f);
            if (_guardTelegraphFrames > 0) {
                _guardTelegraphFrames--;
                return;
            }
            _guardLocal.Modulate = Colors.White;
            if (_hold.RegisterGuardSwing(DeliverGuardSwing())) {
                _guardTelegraphFrames = -1;
                ReleaseGuardLocal();
                _services?.HUD?.SetObjective("tutorial_objective_hold");
            } else {
                _guardTelegraphFrames = Level00HoldFight.GuardTelegraphFrames + Level00HoldFight.GuardRepeatFrames;
            }
        }

        /// <summary>
        /// Resolves one telegraphed swing through the real hurtbox/block path, so a
        /// held guard absorbs it and raises the ordinary block event. Returns true
        /// when the swing was blocked. Public so tests can resolve a swing without
        /// stepping the telegraph.
        /// </summary>
        public bool DeliverGuardSwing() {
            if (_player == null || !IsInstanceValid(_player) || _guardLocal == null || !IsInstanceValid(_guardLocal)) {
                return false;
            }
            var hurtbox = _player.GetNodeOrNull<FTT.Combat.Hurtbox>("Hurtbox");
            if (hurtbox == null) return false;
            _guardSwingBlocked = false;
            float applied = hurtbox.TakeHit(new FTT.Combat.HitPayload {
                AttackerIndex = 99,
                TargetIndex = _player.PlayerIndex,
                AttackID = "level_00_hold_guard_swing",
                HitboxID = "primary",
                AttackClass = FTT.Combat.AttackClass.Basic,
                Damage = GuardSwingDamage,
                Knockback = new Vector2(1.5f, -0.5f),
                HitstunDuration = 0.1f,
                HitOrigin = _guardLocal.GlobalPosition,
                AttackerFacingRight = _player.GlobalPosition.X >= _guardLocal.GlobalPosition.X,
                AppliedStatus = StatusType.None,
                StatusIntensity = 1f,
                ScreenShakeIntensity = 0.1f,
                ScreenShakeDuration = 0.05f
            });
            return _guardSwingBlocked || (applied <= 0f && _player.CurrentState == CharacterState.Blocking);
        }

        /// <summary>
        /// The Guard lesson: the last local stops fighting (held in the inert
        /// rewind-freeze state, unkillable until its swing is blocked) and winds up
        /// the telegraphed swing after the voice's "Guard — now!".
        /// </summary>
        private void BeginGuardLesson() {
            foreach (EnemyController local in _holdLocals) {
                if (local == null || !IsInstanceValid(local) || local.CurrentState == EnemyState.Dead) continue;
                _guardLocal = local;
                break;
            }
            if (_guardLocal == null) {
                if (_hold.ForceComplete()) CompleteHold();
                return;
            }
            _guardLocal.DrillInvulnerable = true;
            _guardLocal.SetStoryRewindFrozen(true);
            _services?.HUD?.SetObjective("tutorial_step_hold_guard");
            if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.hold_guard")) {
                ArmGuardTelegraph();
            }
        }

        private void ArmGuardTelegraph() => _guardTelegraphFrames = Level00HoldFight.GuardTelegraphFrames;

        private void ReleaseGuardLocal() {
            if (_guardLocal == null || !IsInstanceValid(_guardLocal)) return;
            _guardLocal.Modulate = Colors.White;
            _guardLocal.DrillInvulnerable = false;
            _guardLocal.SetStoryRewindFrozen(false);
        }

        /// <summary>Pooled bodies outlive this scene: never hand one back tinted or held.</summary>
        private void ResetHoldLocalPresentation() {
            foreach (EnemyController local in _holdLocals) {
                if (local == null || !IsInstanceValid(local)) continue;
                local.Modulate = Colors.White;
                local.DrillInvulnerable = false;
                if (local == _guardLocal && local.GetParent() == this) local.SetStoryRewindFrozen(false);
            }
        }

        private void OnHoldLocalKilled() {
            HoldFightStep before = _hold.Step;
            HoldFightStep after = _hold.RegisterLocalDefeated();
            if (before == HoldFightStep.FightOff && after == HoldFightStep.Guard) BeginGuardLesson();
            else if (after == HoldFightStep.Done && before != HoldFightStep.Done) CompleteHold();
        }

        /// <summary>"It's catching — hold on!" — then the beam breaks.</summary>
        private void CompleteHold() {
            if (_phase != TutorialPhase.Hold) return;
            if (_player != null && IsInstanceValid(_player)) {
                _player.ClearRallyEcho();
                if (_player.CurrentUltimateMeter > _holdMeterBaseline) {
                    _player.DrainUltimateMeter(_player.CurrentUltimateMeter - _holdMeterBaseline);
                }
            }
            if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.hold_catching")) {
                BreakTheBeam();
            }
        }

        // === Beats 6–8: the beam breaks, the watcher, the Warden rift =========

        /// <summary>
        /// Beat 6: the ignition completes and the cold column cracks against the
        /// gold; for about two seconds the Eraser watcher stands at the tear's
        /// edge (S10) — no line, prompt or UI; then the Warden portal opens beside
        /// the tear and the hero walks to it (beat 7).
        /// </summary>
        public void BreakTheBeam() {
            EndStudyOpening();
            if (_phase != TutorialPhase.Hold) return;
            _phase = TutorialPhase.Rift;
            if (_player != null && IsInstanceValid(_player)) {
                _player.ScriptedHPFloor = 0;
                _player.DefyDisabledByScript = false;
            }
            PlayBeamBreak();
            ShowEraserWatcher();
            _riftGate = BuildGateZone("WardenRiftGate", RiftGatePosition, new Vector2(120, 200),
                new Color(FTT.UI.UIPalette.WardenPortal, 0.2f), "tutorial_objective_rift");
            _services?.HUD?.SetObjective("tutorial_objective_rift");
        }

        /// <summary>Beat 7's destination: the Warden portal beside the tear.</summary>
        public static readonly Vector2 RiftGatePosition = new(670, 820);

        /// <summary>
        /// GU-B (gameplay update 2026-10-12): the watcher stands this far inside
        /// the left edge of what the camera shows when the beam breaks. Its old
        /// fixed spot, x 120 by the tear, is off-screen at the closer Story zoom
        /// once the hold has carried the hero east. Provisional.
        /// </summary>
        public const float WatcherViewInsetPixels = 160f;

        /// <summary>Where the watcher stood before the closer camera, used when no Story camera is found.</summary>
        private const float WatcherFallbackX = 120f;

        /// <summary>
        /// The watcher's spot: <see cref="WatcherViewInsetPixels"/> inside the
        /// hero camera's visible left edge, standing on the floor.
        /// </summary>
        public Vector2 EraserWatcherPosition() {
            StoryCameraConfiner camera = StoryCameraConfiner.FindOn(_player);
            float x = camera != null
                ? camera.VisibleWorldRect.Position.X + WatcherViewInsetPixels
                : WatcherFallbackX;
            return new Vector2(x, TutorialFloorTopY);
        }

        /// <summary>
        /// The watcher (S10): a lean visored figure with a long lance at the edge
        /// of the jagged tear, ~2 s, wordless. Placeholder silhouette; the Level 5
        /// debut reuses the same shape so the player recognizes it.
        /// </summary>
        private void ShowEraserWatcher() {
            var watcher = new Node2D {
                Name = "EraserWatcher",
                Position = EraserWatcherPosition(),
                Modulate = new Color(1f, 1f, 1f, 0f)
            };
            watcher.AddChild(new ColorRect {
                Name = "Silhouette",
                Size = new Vector2(22, 104),
                Position = new Vector2(-11, -104),
                Color = new Color(0.05f, 0.06f, 0.09f, 0.95f)
            });
            watcher.AddChild(new ColorRect {
                Name = "Visor",
                Size = new Vector2(16, 4),
                Position = new Vector2(-6, -94),
                Color = FTT.UI.UIPalette.UnboundCold
            });
            watcher.AddChild(new ColorRect {
                Name = "Lance",
                Size = new Vector2(4, 150),
                Position = new Vector2(14, -140),
                Color = new Color(0.12f, 0.14f, 0.2f, 0.95f)
            });
            AddChild(watcher);
            Tween appear = watcher.CreateTween();
            appear.TweenProperty(watcher, "modulate:a", 1f, 0.3);
            appear.TweenInterval(WatcherSeconds - 0.6);
            appear.TweenProperty(watcher, "modulate:a", 0f, 0.3);
            appear.TweenCallback(Callable.From(watcher.QueueFree));
        }

        /// <summary>
        /// Beat 8, the Translation: the hero steps into the Warden rift and wakes
        /// in the Calibration Bay — healed ("You're whole. Mostly."), with the
        /// fracture left behind — and Part 2's arrival dialogue plays.
        /// </summary>
        public void TranslateToBay() {
            if (_phase != TutorialPhase.Rift) return;
            _phase = TutorialPhase.Arrival;
            if (_riftGate != null && IsInstanceValid(_riftGate)) {
                _riftGate.QueueFree();
                _riftGate = null;
            }
            GetNodeOrNull<Node2D>("ChronalFracture")?.Hide();
            if (_player != null && IsInstanceValid(_player)) {
                _player.GlobalPosition = BayArrivalPosition;
                _player.Velocity = Vector2.Zero;
                _player.HealStory(_player.MaximumHP);
            }
            _services?.HUD?.SetObjective("tutorial_objective_fracture");
            if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.intro")) {
                OfferCalibration();
            }
        }

        // === Part 2: Wren's offer (S02/S09, D16) =============================

        private void OfferCalibration() {
            if (_phase != TutorialPhase.Arrival || _calibrationPrompt != null) return;
            _calibrationPrompt = TwoOptionPrompt.Create(
                "dlg_l00_wren_offer_1", "level00_choice_full", "level00_choice_skip", "speaker_wren");
            _calibrationPrompt.Chosen += OnCalibrationChosen;
            AddChild(_calibrationPrompt);
            if (_player != null && IsInstanceValid(_player)) _player.ProcessMode = ProcessModeEnum.Disabled;
            _calibrationPrompt.Open();
        }

        private void OnCalibrationChosen(int option) {
            if (_player != null && IsInstanceValid(_player)) _player.ProcessMode = ProcessModeEnum.Inherit;
            if (_calibrationPrompt != null && IsInstanceValid(_calibrationPrompt)) _calibrationPrompt.QueueFree();
            _calibrationPrompt = null;
            if (option == 0) ChooseFullCalibration();
            else ChooseSkipCalibration();
        }

        /// <summary>Full calibration: every Part 2 and Part 3 lesson, in order.</summary>
        public void ChooseFullCalibration() {
            if (_phase != TutorialPhase.Arrival) return;
            BeginCalibration();
        }

        /// <summary>
        /// Skip — I'll learn in the field: the skipped lessons arm the slot's
        /// first-use tooltips and the portal to Level 1 opens straight away. The
        /// story-critical exposition already played; skippers lose only the
        /// station flavour lines.
        /// </summary>
        public void ChooseSkipCalibration() {
            if (_phase != TutorialPhase.Arrival) return;
            FirstUseTooltips.ArmForSkippedCalibration(ActiveCampaignSave());
            CompleteTutorial(playCompletionLine: false);
        }

        private static StorySaveData ActiveCampaignSave() {
            if (GameManager.Instance == null || SaveManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length ? SaveManager.Instance.SaveSlots[slot] : null;
        }

        // === end Package 13 W3 Part 1/2 region ================================

        private void OnDialogueComplete(string dialogueID) {
            switch (dialogueID) {
                case "level_00.hold_start":
                    BeginHoldLocals();
                    break;
                case "level_00.hold_guard":
                    ArmGuardTelegraph();
                    break;
                case "level_00.hold_catching":
                    BreakTheBeam();
                    break;
                case "level_00.intro":
                    if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.wren_offer")) {
                        OfferCalibration();
                    }
                    break;
                case "level_00.wren_offer":
                    OfferCalibration();
                    break;
                // S08 station flavour lines: each plays once as its station
                // completes, then the next lesson starts.
                case "level_00.station_grab":
                    BeginHitstunAgencyLesson();
                    break;
                case "level_00.station_defy":
                    BeginRewindDemonstration();
                    break;
                case "level_00.station_rewind":
                    BeginTimeFreezeDrill();
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
            if (_phase == TutorialPhase.Hold && playerIndex == 0) {
                _guardSwingBlocked = true;
                return;
            }
            if (_phase != TutorialPhase.Calibration || playerIndex != 0) return;
            bool advanced = _calibration.RegisterBlockedHit();
            if (_calibration.Step == TutorialCalibrationStep.Block) {
                _services.HUD?.SetObjective("tutorial_step_block",
                    _calibration.HitsBlocked, TutorialCalibrationScript.RequiredBlockedHits);
            }
            if (advanced) CompleteBlockLesson();
        }

        private void OnBlockBroken(int playerIndex) {
            if (_phase == TutorialPhase.Hold && playerIndex == 0) {
                _guardSwingBlocked = true;
                return;
            }
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
            // GU-L0: the drone leaves with its lesson (it used to stay behind as a
            // live hostile, and the trial's kill counter counted it).
            _grabDroneDismissFrames = GrabDroneDismissFrames;
            // "Grabs beat blocks. Strikes beat grabs." — the follow-up names the
            // rule both ways before the next beat starts.
            _services.HUD?.SetObjective("tutorial_step_grab_complete");
            // S08: the Grab station's flavour line, then the next lesson.
            if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.station_grab")) {
                BeginHitstunAgencyLesson();
            }
        }

        /// <summary>
        /// GU-L0 (2026-10-13): how long the grab lesson's drone may fly from the
        /// throw before it is released to the pool (it is released as soon as it
        /// lands). Provisional.
        /// </summary>
        internal const int GrabDroneDismissFrames = 90;

        private int _grabDroneDismissFrames = -1;

        /// <summary>The grab lesson's drone while it exists. Test seam.</summary>
        public EnemyController GrabLessonDrone =>
            _grabDummy != null && IsInstanceValid(_grabDummy) ? _grabDummy : null;

        /// <summary>
        /// GU-L0: once the grab lesson closes, its drone lands its throw and is
        /// handed back to the pool — a dead drone (the throw can kill it) needs
        /// nothing. Polled every physics frame.
        /// </summary>
        private void ProcessGrabDroneDismissal() {
            if (_grabDroneDismissFrames < 0) return;
            if (_grabDummy == null || !IsInstanceValid(_grabDummy) || _grabDummy.IsQueuedForDeletion()
                || _grabDummy.CurrentState == EnemyState.Dead) {
                _grabDroneDismissFrames = -1;
                _grabDummy = null;
                return;
            }
            if (_grabDroneDismissFrames > 0 && (_grabDummy.IsThrownFlight || _grabDummy.IsHeldByPlayer)) {
                _grabDroneDismissFrames--;
                return;
            }
            _grabDroneDismissFrames = -1;
            EnemyController drone = _grabDummy;
            _grabDummy = null;
            // A pooled body outlives this scene: never hand one back held or
            // wearing the lesson's guard.
            _grabGuardUp = false;
            if (_grabGuardVisual != null && IsInstanceValid(_grabGuardVisual)) _grabGuardVisual.QueueFree();
            _grabGuardVisual = null;
            drone.SetStoryRewindFrozen(false);
            if (PoolManager.Instance != null) PoolManager.Instance.Release(drone);
            else drone.QueueFree();
        }

        /// <summary>Test seam: opens the grab lesson (its drone and guard) without the earlier lessons.</summary>
        internal void BeginGrabLessonForTesting() => BeginGrabLesson();

        /// <summary>Test seam: closes the grab lesson as a resolved throw does.</summary>
        internal void CompleteGrabLessonForTesting() => CompleteGrabLesson();

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
        /// The active Defy lesson (meter and Defy update 2026-10-31, plan 2.14). The Influence
        /// Meter is set straight to the Defy cost (a direct, unscaled grant), a swarm of drones
        /// pins the hero in a string with its HP floored at 1 so it cannot be lost, and the step
        /// completes only when the hero presses Defy and the blast fires.
        /// </summary>
        private void BeginMeterAndDefyLesson() {
            if (_player == null || !IsInstanceValid(_player)) return;
            // A scripted meter grant, not a landed hit: a direct set, never a Rally reclaim and
            // never scaled by a Story gain factor.
            _player.SetUltimateMeter(FTT.Combat.BasicComboRules.DefyInfluenceCost);
            _services.HUD?.SetObjective("tutorial_step_defy", DefyButtonLabel());
            _defyCountdownFrames = DefyBeatDelayFrames;
        }

        private void ProcessMeterAndDefyLesson() {
            if (_calibration.Step != TutorialCalibrationStep.MeterAndDefy) return;
            if (_player == null || !IsInstanceValid(_player)) return;
            if (_defyCountdownFrames > 0) {
                _defyCountdownFrames--;
                return;
            }
            if (_defyCountdownFrames == 0) {
                _defyCountdownFrames = -1;
                _defySwarmActive = true;
                // The step cannot be lost: the floor is restored on every exit.
                _player.ScriptedHPFloor = Level00HoldFight.HeroHPFloor;
            }
            if (!_defySwarmActive) return;
            // Never strand the lesson: the swarm refills when it is cleared, and the bar stays
            // at the cost however the fight went (a spend only happens in the Defy itself).
            if (_player.CurrentUltimateMeter < FTT.Combat.BasicComboRules.DefyInfluenceCost && !_player.IsDefying) {
                _player.SetUltimateMeter(FTT.Combat.BasicComboRules.DefyInfluenceCost);
            }
            bool anyAlive = false;
            foreach (EnemyController drone in _defyDrones) {
                if (drone != null && IsInstanceValid(drone) && drone.CurrentState != EnemyState.Dead) anyAlive = true;
            }
            if (!anyAlive) SpawnDefySwarm();
        }

        private void SpawnDefySwarm() {
            ReleaseDefyDrones();
            foreach (Vector2 offset in DefySwarmOffsets) {
                EnemyController drone = EnemyFactory.SpawnSummoned("hologram_drone", this, _player.GlobalPosition + offset);
                if (drone != null) _defyDrones.Add(drone);
            }
        }

        private void ReleaseDefyDrones() {
            foreach (EnemyController drone in _defyDrones) {
                if (drone == null || !IsInstanceValid(drone)) continue;
                if (PoolManager.Instance != null) PoolManager.Instance.Release(drone);
                else drone.QueueFree();
            }
            _defyDrones.Clear();
        }

        /// <summary>The hero's Defy blast fired: the lesson is learned (TutorialCalibration.RegisterDefyExecuted).</summary>
        private void OnDefyExecuted(DefyExecutedPayload payload) {
            if (_player == null || !IsInstanceValid(_player) || payload.PlayerIndex != _player.PlayerIndex) return;
            if (_calibration.Step != TutorialCalibrationStep.MeterAndDefy) return;
            _defySwarmActive = false;
            _defyCountdownFrames = -1;
            _player.ScriptedHPFloor = 0;
            ReleaseDefyDrones();
            _services.HUD?.SetObjective("tutorial_step_defy_spent");
            if (_calibration.RegisterDefyExecuted()) {
                // S08: the Meter & Defy station's flavour line, then the rewind.
                if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.station_defy")) {
                    BeginRewindDemonstration();
                }
            }
        }

        // The bound Defy button(s) for the objective text: the first keyboard and the first
        // gamepad binding of gameplay_defy, or the plain word when the action is not bound.
        private static string DefyButtonLabel() {
            const string Action = "gameplay_defy";
            if (!InputMap.HasAction(Action)) return "Defy";
            string keyboard = null;
            string pad = null;
            foreach (InputEvent inputEvent in InputMap.ActionGetEvents(Action)) {
                string text = inputEvent.AsText();
                int paren = text.IndexOf(" (", StringComparison.Ordinal);
                if (paren > 0) text = text.Substring(0, paren);
                if (inputEvent is InputEventKey) keyboard ??= text;
                else if (inputEvent is InputEventJoypadButton or InputEventJoypadMotion) pad ??= text;
            }
            if (keyboard != null && pad != null) return keyboard + " / " + pad;
            return keyboard ?? pad ?? "Defy";
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
            // S08: the Rewind drill's flavour line (an S01 tell), then Time Freeze.
            if (_services?.Dialogue == null || !_services.Dialogue.StartSequence("level_00.station_rewind")) {
                BeginTimeFreezeDrill();
            }
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
            if (!FTT.Core.TimeFreezeRules.Enabled
                || _services.TimeFreeze == null || !IsInstanceValid(_services.TimeFreeze)) {
                // Never strand the tutorial: with the verb off (gameplay review 2026-11-03, 10b++) or no
                // controller there is no drill; the calibration is already Done when the rule is off.
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

            AddCheckpoint("l00_checkpoint_mobility", MobilityCheckpointPosition);

            // Double-jump gate: a marker above a platform stack only reachable
            // with a full jump chain.
            //
            // Both hops were sized against Lincoln, the roster's floor, when he
            // cleared 93.75 px on one jump and 187.5 px on two (force 10.0 then),
            // so a 128 px step demanded the second jump. Since the 2026-08-15
            // jump compression (force 11.5) and the 2026-10-08 apex hang his
            // integrated single jump rises about 133 px (StoryAirPhysics), and
            // the 128/130 px steps stopped asking anyone for the second jump.
            // The gated step is now 240 px (2026-10-08): above every hero's single
            // jump (Cleopatra's ~214 px is the best) and under every double jump
            // (Lincoln's ~265 px is the worst), so the gate enforces the double
            // jump for the whole roster; TutorialCalibrationContentTests pins both
            // bounds. The first step stays a single hop for everyone. The stack
            // used to ask for 188 px from the floor and another 240 px on top,
            // which no character has ever cleared — the five single-jump kits
            // could not even reach the lower platform, and the 2026-08-10 jump
            // retune (feel batch §2.2) would have left it unclearable for
            // everyone. See that plan's §9.
            BuildPlatform(DoubleJumpFirstStepX, DoubleJumpFirstStepY, 200);
            BuildPlatform(DoubleJumpGatedStepX, DoubleJumpGatedStepY, 180);
            float gatedTop = DoubleJumpGatedStepY - TutorialPlatformThickness / 2f;
            _doubleJumpGate = BuildGateZone("DoubleJumpGate", new Vector2(DoubleJumpGatedStepX, gatedTop - 70f),
                new Vector2(160, 140), new Color(0.2f, 0.9f, 0.5f, 0.25f), "tutorial_gate_double_jump");

            // V7.6: the movement-ability corridor is DELETED. The Movement
            // Ability unlocks after Level 1 under the Legacy Unlock Schedule, so
            // a Level 0 gate demanding it was an unclearable wall. Part 3's
            // traversal is authored for base jump reach alone; the ability gets
            // its own optional Wren drill when it is actually granted.

            // Roll gate: a marked strip the player must cross while rolling.
            // GU-L0: it stood at 3100, inside what is now the Combat Trial's
            // lock; it moved west of it, so the trial opens with the hero outside
            // the lock, beside its anchor and font.
            _rollGate = BuildGateZone("RollGate", RollGatePosition, new Vector2(200, 110),
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
            // Package 13 W3: Part 1's hold and the walk to the Warden rift.
            ProcessHoldFight();
            if (_phase == TutorialPhase.Rift && ZoneContainsPlayer(_riftGate)) TranslateToBay();
            ProcessRewindDemo();
            ProcessGrabDroneDismissal();
            if (_phase == TutorialPhase.Calibration) {
                // V7.6 beats. Each is phase-gated inside itself, so the order
                // here is documentation, not control flow.
                ProcessGrabLesson();
                ProcessHitstunAgencyLesson();
                ProcessMeterAndDefyLesson();
            ProcessTimeFreezeDrill();
            }
            if (_phase == TutorialPhase.CombatTrial) RefreshTrialObjective();
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

        // === Combat trial (GU-L0: one encounter lock over the bay) ===

        private void BeginCombatTrial() {
            _phase = TutorialPhase.CombatTrial;
            AddCheckpoint("l00_checkpoint_trial", TrialCheckpointPosition);
            BuildRestorationFont();
            BuildTrialLock();
            RefreshTrialObjective();
        }

        /// <summary>
        /// Test seam: the Combat Trial without walking the calibration and the
        /// mobility gates (the <c>StoryManager.SuppressSceneLoadsForTesting</c>
        /// precedent).
        /// </summary>
        internal void BeginCombatTrialForTesting() => BeginCombatTrial();

        // === D13: the Restoration Font (2026-10-11) ===

        /// <summary>The mobility gates' checkpoint position.</summary>
        public static readonly Vector2 MobilityCheckpointPosition = new(1500, 850);

        /// <summary>
        /// The roll gate (GU-L0: it stood at 3100, inside what is now the trial's
        /// lock; it sits west of it, past the double-jump steps).
        /// </summary>
        public static readonly Vector2 RollGatePosition = new(2150, 850);

        /// <summary>
        /// The Combat Trial's checkpoint position (the font's anchor). GU-L0: it
        /// stood at 3400, inside what is now the trial's lock; it stands beside
        /// the roll gate, west of the lock, where the trial begins.
        /// </summary>
        public static readonly Vector2 TrialCheckpointPosition = new(2300, 850);

        /// <summary>The Calibration Bay's east wall.</summary>
        public const float CalibrationBayEastWallX = 4780f;

        /// <summary>
        /// D13 (PROVISIONAL-FONT-PLACEMENT): the Tutorial's Restoration Font ID.
        /// Level 0 does not extend <see cref="StoryLevelControllerBase"/>.
        /// </summary>
        public const string RestorationFontID = "level_00.restoration_font";

        /// <summary>
        /// D13: on the Calibration Bay floor past the trial anchor. The design's
        /// "between the mid checkpoint and the boss" has no meaning here, and the
        /// Part 1 home-era hold shares this space, so the font is built only when
        /// the Combat Trial starts — never during the hold, the calibration, the
        /// mobility gates or the Skip path (no Warden wellspring in the hero's own
        /// era: the "two kinds of door" grammar). GU-L0: 4300 -> 2450, out of the
        /// trial's lock (from 2580), 150 px past the trial anchor.
        /// </summary>
        public static readonly Vector2 RestorationFontPosition = new(2450f, TutorialFloorTopY);

        /// <summary>The font the Combat Trial placed, or null before it. Test seam.</summary>
        public RestorationFont PlacedRestorationFont { get; private set; }

        private void BuildRestorationFont() {
            if (PlacedRestorationFont != null) return;
            PlacedRestorationFont = RestorationFont.Create(RestorationFontID, RestorationFontPosition);
            // Completion re-keys the use registry to the next level, so the font
            // refuses once the tutorial is complete.
            PlacedRestorationFont.RefusesWhile = () => _levelComplete;
            AddChild(PlacedRestorationFont);
        }

        // === The trial's lock ===

        /// <summary>
        /// The trial lock's stable ID. Level 0 has no mid-level resume and no
        /// checkpoint after the lock, so it never appears in a baseline.
        /// </summary>
        public const string TrialLockID = "level_00_tutorial_lock_combat_trial";

        /// <summary>
        /// The trial lock's west edge: the gap between the 2300 and 2700 platforms,
        /// so the entry barrier (2560–2580) cuts neither; the bay's east wall is
        /// its other side (2200 px).
        /// </summary>
        public const float TrialLockMinX = 2580f;

        /// <summary>
        /// The two wave tables' IDs. Level 0 pays no persistent dust and ships an
        /// explicitly empty reward manifest (DustEconomyTests), so — unlike the
        /// campaign's locks — these name no manifest row.
        /// </summary>
        public const string TrialWave1ID = "level_00.trial_wave_1";
        public const string TrialWave2ID = "level_00.trial_wave_2";

        /// <summary>
        /// Wave 1: five hologram drones (Easy three), across the bay inside the
        /// lock (they used to spawn from 1200 to 3600, most of them behind a hero
        /// standing at 3100).
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] TrialWave1Spawns = {
            ("hologram_drone", new Vector2(2950, 850), null, null),
            ("hologram_drone", new Vector2(3350, 850), null, null),
            ("hologram_drone", new Vector2(3750, 850), new Vector2(3550, 850), new Vector2(3950, 850)),
            ("hologram_drone", new Vector2(4150, 700), null, null),
            ("hologram_drone", new Vector2(4500, 850), null, null)
        };

        /// <summary>Wave 2, once wave 1 is down: three drones and a slasher (Easy two drones).</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] TrialWave2Spawns = {
            ("hologram_drone", new Vector2(3000, 850), null, null),
            ("hologram_drone", new Vector2(3500, 600), null, null),
            ("hologram_drone", new Vector2(4000, 850), null, null),
            ("chrono_slasher", new Vector2(4300, 850), new Vector2(4100, 850), new Vector2(4500, 850))
        };

        /// <summary>The trial's lock, once the trial has begun. Test and bot seam.</summary>
        public EncounterLock TrialLock { get; private set; }

        private int _trialWave1Planned;
        private int _trialWave2Planned;
        private string _trialObjectiveKey = "";
        private int _trialObjectiveDefeated = -1;

        /// <summary>
        /// Builds and arms the trial's lock: the entry barrier stands open, wave 1
        /// spawns the moment the hero walks into the bay, wave 2 once wave 1 is
        /// down, and the clear completes the tutorial.
        /// </summary>
        private void BuildTrialLock() {
            if (TrialLock != null) return;
            EncounterLockWave wave1 = EncounterLockWave.FromTable(TrialWave1ID, TrialWave1Spawns, EncounterWaveTrigger.OnEngage);
            EncounterLockWave wave2 = EncounterLockWave.FromTable(TrialWave2ID, TrialWave2Spawns, EncounterWaveTrigger.AfterPreviousCleared);
            TrialLock = EncounterLock.Create(new EncounterLockSpec {
                LockID = TrialLockID,
                Area = EncounterLockSpec.Span(TrialLockMinX, CalibrationBayEastWallX, BayHeight),
                LevelBounds = new Rect2(0f, 0f, BayWidth, BayHeight),
                EntryBarrierX = TrialLockMinX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f,
                // No exit barrier: the bay's east wall closes the far side.
                BarrierFloorY = TutorialFloorTopY,
                Waves = new[] { wave1, wave2 },
                GoDirection = EncounterGoDirection.None
            });
            TrialLock.StateChanged += OnTrialLockStateChanged;
            AddChild(TrialLock);
            TrialLock.Hero = _player;
            TrialLock.Camera = StoryCameraConfiner.FindOn(_player);
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            _trialWave1Planned = EncounterLockRules.PlannedSpawnCount(TrialWave1Spawns.Length, -1, true, difficulty);
            _trialWave2Planned = EncounterLockRules.PlannedSpawnCount(TrialWave2Spawns.Length, -1, true, difficulty);
            TrialLock.Arm();
        }

        private void OnTrialLockStateChanged(EncounterLock encounterLock, EncounterLockState previous,
            EncounterLockState current) {
            if (current == EncounterLockState.Cleared) CompleteTutorial();
        }

        /// <summary>
        /// The trial objective from the lock: wave 1's progress, then wave 2's
        /// (the tutorial's own two objectives; the HUD's lock counter shows the
        /// running total beside them). Posted only when it changes.
        /// </summary>
        private void RefreshTrialObjective() {
            if (_phase != TutorialPhase.CombatTrial || _levelComplete) return;
            if (TrialLock == null || !IsInstanceValid(TrialLock)) return;
            int remaining = TrialLock.RemainingCount;
            string key;
            int defeated;
            int total;
            if (!TrialLock.AllWavesSpawned) {
                key = "tutorial_objective_progress";
                total = _trialWave1Planned;
                defeated = Mathf.Clamp(total - Mathf.Max(0, remaining - _trialWave2Planned), 0, total);
            } else {
                key = "tutorial_objective_wave_two";
                total = _trialWave2Planned;
                defeated = Mathf.Clamp(total - remaining, 0, total);
            }
            if (key == _trialObjectiveKey && defeated == _trialObjectiveDefeated) return;
            _trialObjectiveKey = key;
            _trialObjectiveDefeated = defeated;
            _services?.HUD?.SetObjective(key, defeated, total);
        }

        private void OnEnemyKilled(EnemyKilledPayload payload) {
            // GU-L0: only the hold counts kills now; the trial is its lock's.
            if (_phase == TutorialPhase.Hold) OnHoldLocalKilled();
        }

        /// <summary>
        /// Ends Level 0. The Full path closes on the Combat Trial's S08 flavour line
        /// before the results; the Skip path (<paramref name="playCompletionLine"/>
        /// false) goes straight to them — "Skip opens the portal to Level 1
        /// immediately". Both land on the Bridge, where Sarah's S11 triage line
        /// charges the portal (the hub prologue set).
        /// </summary>
        private void CompleteTutorial(bool playCompletionLine = true) {
            if (_levelComplete) return;
            _levelComplete = true;
            _phase = TutorialPhase.Complete;

            if (StoryManager.Instance != null) StoryManager.Instance.TutorialComplete = true;
            // CompleteLevel raises OnLevelComplete, which advances the campaign
            // level and autosaves completion; do not also advance manually.
            _levelManager?.CompleteLevel();
            _services?.HUD?.SetObjective("tutorial_objective_complete");
            if (!playCompletionLine || _services?.Dialogue == null
                || !_services.Dialogue.StartSequence("level_00.complete")) {
                ShowCompletionResults();
            }
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
            bg.ZIndex = -10;
            bg.Size = new Vector2(4800, 1080);
            bg.Color = new Color(0.03f, 0.05f, 0.12f);
            AddChild(bg);

            BuildFloor(0, TutorialFloorTopY, 4800);

            BuildPlatform(300, 700, 250);
            BuildPlatform(700, 550, 200);
            BuildPlatform(1100, 700, 250);
            BuildPlatform(1500, 600, 220);
            BuildPlatform(2300, 550, 250);
            BuildPlatform(2700, 680, 220);
            BuildPlatform(3500, 720, 250);
            BuildPlatform(4000, 550, 200);

            BuildWall(0, 0, 1080);
            BuildWall(CalibrationBayEastWallX, 0, 1080);

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
            // Package 13 W3 (S02): the timeline is split around the hold fight.
            // Beats 1–2 play on load — the beam bites, the ignition BEGINS — and
            // the column stays up while the locals come (beat 5). Beats 3–4 (the
            // beam breaks, the Warden door opens) play from PlayBeamBreak once
            // the hold is won, which is what the fight buys the ignition time for.
            _fractureBeam = beam;
            _fractureHalo = coldHalo;
            _fracturePortal = portal;
            Tween beat = rift.CreateTween();
            _fractureIntroBeat = beat;
            // 1. The beam bites: cold flares to full over the first second.
            beat.TweenProperty(beam, "modulate:a", 1f, 0.9);
            // 2. Gold answers on the hero.
            beat.TweenProperty(ignition, "modulate:a", 1f, 0.7)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        private Node2D _fractureBeam;
        private ColorRect _fractureHalo;
        private Node2D _fracturePortal;

        /// <summary>
        /// Beats 3–4 of the ignition grammar, after the hold: the beam breaks — the
        /// cold guts out rather than fading evenly — and the Warden door opens
        /// beside the tear, steady (a linear fade, no overshoot, no bounce).
        /// </summary>
        private void PlayBeamBreak() {
            Node2D rift = GetNodeOrNull<Node2D>("ChronalFracture");
            if (rift == null || _fractureBeam == null || !IsInstanceValid(_fractureBeam)) return;
            Tween beat = rift.CreateTween();
            beat.TweenProperty(_fractureBeam, "modulate:a", 0.28f, 0.5)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            if (_fractureHalo != null && IsInstanceValid(_fractureHalo)) {
                beat.Parallel().TweenProperty(_fractureHalo, "modulate:a", 0.35f, 0.5);
            }
            if (_fracturePortal != null && IsInstanceValid(_fracturePortal)) {
                beat.TweenProperty(_fracturePortal, "modulate:a", 1f, 0.8)
                    .SetTrans(Tween.TransitionType.Linear);
            }

            // The broken beam keeps a slow cold flicker afterwards: the wound is
            // still open, which is the level's whole premise. The portal is NOT
            // animated — steady geometry is half its definition.
            Tween flicker = rift.CreateTween().SetLoops();
            flicker.TweenInterval(3.0);
            flicker.TweenProperty(_fractureBeam, "modulate:a", 0.16f, 1.1);
            flicker.TweenProperty(_fractureBeam, "modulate:a", 0.34f, 1.1);
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
            StoryOpeningPresentation.TrackSurface(floor, new Rect2(x, y, width, 40));
            _openingSurfaces.Add(floor);
        }

        private void BuildPlatform(float x, float y, float width) {
            var plat = new StaticBody2D();
            plat.CollisionLayer = CollisionLayers.Environment;
            plat.CollisionMask = 0;
            plat.Name = $"Platform_{x}";
            plat.Position = new Vector2(x, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(width, TutorialPlatformThickness);
            col.Shape = shape;
            plat.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(width, TutorialPlatformThickness);
            visual.Position = new Vector2(-width / 2f, -TutorialPlatformThickness / 2f);
            visual.Color = new Color(0.08f, 0.12f, 0.22f);
            plat.AddChild(visual);

            var glow = new ColorRect();
            glow.Size = new Vector2(width - 10, 2);
            glow.Position = new Vector2(-(width - 10) / 2f, -8);
            glow.Color = new Color(0.0f, 0.7f, 0.9f, 0.3f);
            plat.AddChild(glow);

            AddChild(plat);
            StoryOpeningPresentation.TrackSurface(plat, new Rect2(x - width / 2f, y - TutorialPlatformThickness / 2f, width, TutorialPlatformThickness));
            _openingSurfaces.Add(plat);
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

            // F8 (2026-10-04): the shared Story follow rig (StoryCameraRules) over
            // the same 4800 x 1080 limits; a scripted relocation (the rift to the
            // bay) is a teleport the rig cuts on rather than pans across.
            var camera = new StoryCameraConfiner {
                Name = "PlayerCamera",
                Enabled = true,
                ActiveBounds = new Rect2(0, 0, 4800, 1080)
            };
            _player.AddChild(camera);
            _studyOpening = StoryOpeningPresentation.Apply(this, _player, camera, 0, false, _openingSurfaces);
            camera.EnableFollow(_player);
        }
    }
}
