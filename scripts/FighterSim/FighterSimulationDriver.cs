using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.FighterSim {

    /// <summary>
    /// Godot boundary for local Fighter Mode. It samples device-isolated input at
    /// 60 Hz and presents Klotho state; native CharacterBody2D gameplay is disabled.
    /// </summary>
    public partial class FighterSimulationDriver : Node {
        private static readonly Vector2 WorldOrigin = new(950f, 700f);
        private const float PixelsPerUnit = 62.5f;
        /// <summary>Local slot the CPU opponent occupies; folded into its seed.</summary>
        private const int CpuPlayerSlot = 1;

        private PlayerController _playerOne;
        private PlayerController _playerTwo;
        private FighterCpuController _cpuController;
        private PlayerInputFrame _previousCpuFrame;
        private bool _completionRaised;
        private FTT.Combat.FighterCamera _camera;
        private FTT.UI.LocalFighterPause _pauseMenu;
        private FTT.UI.MatchResults _results;
        private FTT.UI.FighterHUD _hud;
        private int _lastCountdownDigit = -1;
        private bool _matchStartRaised;
        private readonly int[] _lastStocks = { -1, -1 };
        private int _stockLossFreezeFrames;
        private KnockoutStep _knockoutStep = KnockoutStep.None;
        private double _knockoutTimer;
        private double _slowMotionCredit;
        private FighterMatchResult _pendingResult;
        private readonly Queue<ColorRect> _inactiveProxies = new();
        private readonly Dictionary<int, ColorRect> _projectileProxies = new();
        private readonly Dictionary<int, ColorRect> _persistentProxies = new();
        private readonly Dictionary<int, ColorRect> _hazardProxies = new();
        private readonly Dictionary<int, ColorRect> _orbProxies = new();
        private readonly Dictionary<int, ColorRect> _zoneProxies = new();
        private readonly HashSet<int> _seenProxyIDs = new();
        private readonly List<int> _releaseProxyIDs = new();
        private readonly List<FighterProjectileComponent> _projectiles = new(64);
        private readonly List<FighterPersistentObjectComponent> _persistentObjects = new(16);
        private readonly List<FighterHazardComponent> _hazards = new(16);
        private readonly List<FighterOrbComponent> _orbs = new(16);
        private readonly List<FighterZoneComponent> _zones = new(8);

        public event Action<FighterMatchResult> MatchCompleted;

        public FighterSimulation Simulation { get; private set; }
        public bool IsInitialized => Simulation != null;
        public long CurrentHash => Simulation?.CurrentHash ?? 0L;
        public int CurrentTick => Simulation?.CurrentTick ?? 0;

        /// <summary>True while the driver is playing the post-match KO sequence.</summary>
        public bool IsPlayingKnockoutSequence => _knockoutStep != KnockoutStep.None;

        /// <summary>
        /// Post-match presentation steps. Timings from design-godot.md Section 11
        /// "Match-Ending KO Sequence". These pace or defer <c>Simulation.Advance</c>
        /// on the driver's own presentation clock; the simulation itself has no
        /// notion of a cinematic and <c>Engine.TimeScale</c> is never touched.
        /// </summary>
        private enum KnockoutStep {
            None,
            HitFreeze,
            SlowMotion,
            Stamp,
            WinnerPose
        }

        private const double HitFreezeSeconds = 0.5;
        private const double SlowMotionSeconds = 1.5;
        private const double SlowMotionRate = 0.75;
        private const double StampSeconds = 0.6;
        private const double WinnerPoseSeconds = 2.0;
        private const int StockLossFreezeFrames = 12;
        private const float KnockoutFocusZoom = 1.35f;

        private const string PauseScenePath = "res://scenes/ui/LocalFighterPause.tscn";
        private const string ResultsScenePath = "res://scenes/ui/MatchResults.tscn";
        private const string KnockoutStingerPath = "res://audio/sfx/combat/ko_stinger.ogg";
        private const string VictoryFanfarePath = "res://audio/sfx/ui/victory_fanfare.ogg";

        public void Initialize(
            PlayerController playerOne,
            PlayerController playerTwo,
            MatchSettings settings,
            int stageHazardTypeID = 1,
            string stageID = "") {
            if (Simulation != null) throw new InvalidOperationException("Fighter simulation is already initialized.");
            _playerOne = playerOne ?? throw new ArgumentNullException(nameof(playerOne));
            _playerTwo = playerTwo ?? throw new ArgumentNullException(nameof(playerTwo));
            if (_playerOne.Data == null || _playerTwo.Data == null) {
                throw new InvalidOperationException("Both presentation fighters require CharacterData.");
            }

            DisableNativeGameplay(_playerOne);
            DisableNativeGameplay(_playerTwo);
            Simulation = new FighterSimulation(
                FighterLoadoutFactory.FromCharacterData(_playerOne.Data),
                FighterLoadoutFactory.FromCharacterData(_playerTwo.Data),
                Math.Max(1, settings.StockCount),
                MatchSeconds(settings),
                rules: RulesFor(settings, stageHazardTypeID),
                stageGeometry: FighterStageGeometry.ForStage(stageID ?? ""));
            SessionData session = GameManager.Instance?.CurrentSession ?? default;
            if (session.FighterOpponentType == FighterOpponentType.Cpu) {
                // Derive the CPU stream from the match seed plus its player slot, so a
                // rematch with identical settings reproduces the same opponent instead
                // of the old hardcoded literal. The CPU stays outside the snapshot.
                int cpuSeed = unchecked(Simulation.GetMatchState().WorldSeed * 397 + CpuPlayerSlot);
                _cpuController = new FighterCpuController(
                    session.CpuDifficulty,
                    cpuSeed,
                    FighterStageGeometry.ForStage(stageID ?? ""),
                    new FighterSimulationWorldObserver(Simulation));
            }
            WarmPresentationProxies(112);
            AddToGroup("FighterSimulation");
            AttachMatchFlowUI();
            SyncPresentation();
        }

        /// <summary>Match length in whole seconds, matching the lobby setting.</summary>
        public static int MatchSeconds(in MatchSettings settings) =>
            Math.Max(1, Mathf.RoundToInt(settings.TimeLimit));

        /// <summary>
        /// The single mapping from lobby <see cref="MatchSettings"/> to deterministic
        /// <see cref="FighterMatchRules"/>. Every rule the select screen exposes has
        /// to survive this hop, and the production countdown is applied here so a
        /// real match never starts live on frame zero.
        /// </summary>
        public static FighterMatchRules RulesFor(in MatchSettings settings, int stageHazardTypeID) => new(
            (int)settings.Mode,
            settings.ItemsEnabled && settings.ItemSpawnRate != ChronalOrbFrequency.Off,
            (int)settings.ItemSpawnRate,
            settings.StageHazardsEnabled && settings.HazardRate != HazardTriggerFrequency.Off,
            (int)settings.HazardRate,
            stageHazardTypeID,
            FighterMatchFlowRules.CountdownFrames);

        public override void _PhysicsProcess(double delta) {
            if (Simulation == null) return;
            if (_knockoutStep != KnockoutStep.None) {
                AdvanceKnockoutSequence(delta);
                return;
            }
            FighterMatchComponent match = Simulation.GetMatchState();
            if (match.MatchState == FighterMatchStates.Complete) {
                RaiseCompletionIfNeeded();
                return;
            }
            // A stock-loss impact freeze holds the presentation for a few frames
            // without advancing the simulation. Deferred frames are simply frames
            // that have not happened yet — no state is skipped or replayed.
            if (_stockLossFreezeFrames > 0) {
                _stockLossFreezeFrames--;
                return;
            }

            uint tick = unchecked((uint)Simulation.CurrentTick);
            // InputManager owns one edge calculation per engine physics frame. Preserve
            // those edges and only relabel the command with the simulation tick.
            PlayerInputFrame playerOneInput = InputManager.Instance?.GetFrame(0) ?? default;
            PlayerInputFrame playerTwoInput;
            if (_cpuController != null
                && Simulation.TryGetFighter(0, out FighterStateComponent playerOneState)
                && Simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent playerOneRuntime)
                && Simulation.TryGetFighter(1, out FighterStateComponent playerTwoState)
                && Simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent playerTwoRuntime)) {
                playerTwoInput = _cpuController.Sample(
                    tick,
                    in playerTwoState,
                    in playerTwoRuntime,
                    in playerOneState,
                    in playerOneRuntime,
                    in _previousCpuFrame);
                _previousCpuFrame = playerTwoInput;
            } else {
                playerTwoInput = InputManager.Instance?.GetFrame(1) ?? default;
            }
            playerOneInput.Tick = tick;
            playerTwoInput.Tick = tick;
            Simulation.Advance(playerOneInput, playerTwoInput);
            SyncPresentation();
            PublishMatchFlowPresentation();
            RaiseCompletionIfNeeded();
        }

        public bool TryGetFighter(int playerID, out FighterStateComponent state) {
            if (Simulation != null) return Simulation.TryGetFighter(playerID, out state);
            state = default;
            return false;
        }

        public bool TryGetRuntime(int playerID, out FighterRuntimeComponent runtime) {
            if (Simulation != null) return Simulation.TryGetFighterRuntime(playerID, out runtime);
            runtime = default;
            return false;
        }

        public string GetStateLabel(int playerID) {
            if (!TryGetFighter(playerID, out FighterStateComponent state)
                || !TryGetRuntime(playerID, out FighterRuntimeComponent runtime)) return "Unavailable";
            if (state.Stocks <= 0) return "Knocked Out";
            if (FighterMatchFlowRules.IsOnRespawnPlatform(in state)) return "Respawn Platform";
            if (Simulation.GetMatchState().MatchState == FighterMatchStates.Countdown) return "Countdown";
            if (runtime.UniversalMovementState is (int)UniversalMovementPhase.RollStartup
                or (int)UniversalMovementPhase.RollTravel
                or (int)UniversalMovementPhase.RollRecovery) return "Rolling";
            if (state.InvulnerabilityFrames > 0) return "Respawning";
            if (state.DazeFrames > 0) return "Dazed";
            if (state.HitstunFrames > 0) return "Stunned";
            if (FighterBasicAttackRules.IsSwinging(in runtime)) return "Attacking";
            if (FighterBasicAttackRules.IsBlockStance(in state, in runtime)) return "Blocking";
            return state.IsGrounded != 0 ? "Grounded" : "Airborne";
        }

        private void SyncPresentation() {
            PushSlotIndicators();
            SyncPlayer(_playerOne, 0);
            SyncPlayer(_playerTwo, 1);
            SyncSimulationEntities();
        }

        private void WarmPresentationProxies(int count) {
            for (int index = 0; index < count; index++) {
                var proxy = new ColorRect {
                    Name = $"SimulationProxy_{index}",
                    Visible = false,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ZIndex = 5
                };
                AddChild(proxy);
                _inactiveProxies.Enqueue(proxy);
            }
        }

        private void SyncSimulationEntities() {
            Simulation.CopyProjectilesTo(_projectiles);
            _seenProxyIDs.Clear();
            foreach (FighterProjectileComponent projectile in _projectiles) {
                ColorRect proxy = GetProxy(_projectileProxies, projectile.EntityID);
                ConfigureProxy(
                    proxy, projectile.Position, projectile.HalfExtents,
                    new Color(1f, 0.72f, 0.16f, 0.9f));
                _seenProxyIDs.Add(projectile.EntityID);
            }
            ReleaseMissing(_projectileProxies);

            Simulation.CopyPersistentObjectsTo(_persistentObjects);
            _seenProxyIDs.Clear();
            foreach (FighterPersistentObjectComponent persistent in _persistentObjects) {
                ColorRect proxy = GetProxy(_persistentProxies, persistent.EntityID);
                ConfigureProxy(
                    proxy, persistent.Position, persistent.HalfExtents,
                    new Color(0.1f, 0.9f, 0.95f, 0.82f));
                _seenProxyIDs.Add(persistent.EntityID);
            }
            ReleaseMissing(_persistentProxies);

            // Package 8 B6: a presentation-only clock. It advances with the driver's
            // sync pass, never feeds back into FighterSimulation, and drives only
            // proxy colour/rotation/scale.
            _proxyStyleFrame++;

            Simulation.CopyHazardsTo(_hazards);
            _seenProxyIDs.Clear();
            foreach (FighterHazardComponent hazard in _hazards) {
                ColorRect proxy = GetProxy(_hazardProxies, hazard.EntityID);
                ConfigureProxy(
                    proxy, hazard.Position, hazard.HalfExtents,
                    HazardColor(hazard.HazardTypeID, hazard.Phase == 0, _proxyStyleFrame),
                    FighterProxyStyle.HazardRotation(hazard.HazardTypeID, _proxyStyleFrame));
                _seenProxyIDs.Add(hazard.EntityID);
            }
            ReleaseMissing(_hazardProxies);

            Simulation.CopyOrbsTo(_orbs);
            _seenProxyIDs.Clear();
            foreach (FighterOrbComponent orb in _orbs) {
                ColorRect proxy = GetProxy(_orbProxies, orb.EntityID);
                ConfigureProxy(
                    proxy, orb.Position, orb.HalfExtents,
                    FighterProxyStyle.OrbColor(orb.EffectType, _proxyStyleFrame),
                    FighterProxyStyle.OrbRotation,
                    FighterProxyStyle.OrbScale(_proxyStyleFrame));
                _seenProxyIDs.Add(orb.EntityID);
            }
            ReleaseMissing(_orbProxies);

            Simulation.CopyZonesTo(_zones);
            _seenProxyIDs.Clear();
            foreach (FighterZoneComponent zone in _zones) {
                ColorRect proxy = GetProxy(_zoneProxies, zone.EntityID);
                ConfigureProxy(
                    proxy, zone.Position, zone.HalfExtents,
                    new Color(0.45f, 0.3f, 1f, FighterProxyStyle.ZoneAlpha(_proxyStyleFrame)));
                _seenProxyIDs.Add(zone.EntityID);
            }
            ReleaseMissing(_zoneProxies);
        }

        /// <summary>Presentation-only frame counter for proxy pulses (Package 8 B6).</summary>
        private int _proxyStyleFrame;

        private static Color HazardColor(int hazardTypeID, bool warning, int styleFrame) {
            Color active = hazardTypeID switch {
                1 => new Color(0.9f, 0.9f, 0.95f, 0.78f),
                2 => new Color(1f, 0.35f, 0.08f, 0.82f),
                3 => new Color(0.12f, 0.82f, 1f, 0.82f),
                4 => new Color(0.72f, 0.18f, 1f, 0.74f),
                5 => new Color(1f, 0.16f, 0.03f, 0.82f),
                6 => new Color(1f, 0.62f, 0.08f, 0.82f),
                7 => new Color(0.88f, 0.7f, 0.2f, 0.72f),
                8 => new Color(0.9f, 0.08f, 0.12f, 0.8f),
                9 => new Color(0.55f, 0.24f, 0.1f, 0.8f),
                _ => new Color(1f, 0.08f, 0.05f, 0.82f)
            };
            // Package 8 B6: a warning phase now blinks rather than sitting at a flat
            // low alpha, so a telegraph reads as "about to hurt".
            return warning
                ? new Color(active.R, active.G, active.B, FighterProxyStyle.HazardWarningAlpha(styleFrame))
                : active;
        }

        private ColorRect GetProxy(Dictionary<int, ColorRect> active, int entityID) {
            if (active.TryGetValue(entityID, out ColorRect proxy)) return proxy;
            if (_inactiveProxies.Count == 0) throw new InvalidOperationException("Fighter presentation proxy pool exhausted.");
            proxy = _inactiveProxies.Dequeue();
            proxy.Visible = true;
            active[entityID] = proxy;
            return proxy;
        }

        private static void ConfigureProxy(
            ColorRect proxy,
            xpTURN.Klotho.Deterministic.Math.FPVector2 position,
            xpTURN.Klotho.Deterministic.Math.FPVector2 halfExtents,
            Color color,
            float rotationRadians = 0f,
            float silhouetteScale = 1f) {
            Vector2 size = new(
                halfExtents.x.ToFloat() * PixelsPerUnit * 2f * silhouetteScale,
                halfExtents.y.ToFloat() * PixelsPerUnit * 2f * silhouetteScale);
            Vector2 center = WorldOrigin + new Vector2(
                position.x.ToFloat() * PixelsPerUnit,
                -position.y.ToFloat() * PixelsPerUnit);
            proxy.Size = size;
            proxy.Position = center - size / 2f;
            proxy.Color = color;
            // Package 8 B6 silhouette identity. Rotation is about the rect's centre,
            // so the proxy still occupies the position the simulation reported.
            proxy.PivotOffset = size / 2f;
            proxy.Rotation = rotationRadians;
        }

        private void ReleaseMissing(Dictionary<int, ColorRect> active) {
            _releaseProxyIDs.Clear();
            foreach (int entityID in active.Keys) {
                if (!_seenProxyIDs.Contains(entityID)) _releaseProxyIDs.Add(entityID);
            }
            foreach (int entityID in _releaseProxyIDs) {
                ColorRect proxy = active[entityID];
                active.Remove(entityID);
                proxy.Visible = false;
                proxy.Size = Vector2.Zero;
                // A recycled proxy must not inherit the previous entity's silhouette.
                proxy.Rotation = 0f;
                proxy.PivotOffset = Vector2.Zero;
                _inactiveProxies.Enqueue(proxy);
            }
        }

        // === Package 8 A3: presentation feedback state ===
        // Purely cosmetic diffing caches. Nothing here is read by the simulation,
        // and nothing here writes into scripts/FighterSim/ state.
        private readonly int[] _presentedStatus = { -1, -1 };
        private readonly bool[] _presentedHyperArmor = { false, false };
        private readonly bool[] _presentedInvulnerable = { false, false };
        private readonly int[] _presentedHP = { -1, -1 };
        private readonly int[] _presentedDazeFrames = { 0, 0 };
        private readonly bool[] _presentedMeleeActive = { false, false };
        private readonly int[] _presentedBlockCharges = { -1, -1 };
        private bool _slotIndicatorsPushed;

        private static readonly string[] BasicAttackAnimationNames = {
            "basic_attack_1", "basic_attack_2", "basic_attack_3"
        };

        private const float FighterHitShakeScale = 0.35f;
        private const float FighterHitShakeDuration = 0.12f;
        private const float FighterKnockoutShake = 14f;
        private const float FighterKnockoutShakeDuration = 0.45f;

        /// <summary>
        /// Pushes the persistent player-slot outline once the presentation bodies
        /// exist (design-godot.md: a subtle ownership indicator for the match).
        /// </summary>
        private void PushSlotIndicators() {
            if (_slotIndicatorsPushed) return;
            _slotIndicatorsPushed = true;
            _playerOne?.Glow?.SetSlotIndicator(0);
            _playerTwo?.Glow?.SetSlotIndicator(1);
        }

        /// <summary>
        /// Mirrors deterministic component state onto the shared glow arbiter and
        /// fires hit feedback. Reads simulation components; writes only Godot
        /// presentation nodes and the feedback autoloads.
        /// </summary>
        private void SyncPresentationFeedback(PlayerController player, int playerID,
            in FighterStateComponent state, in FighterRuntimeComponent runtime) {
            FTT.Combat.GlowPresentationController glow = player.Glow;
            if (glow != null) {
                if (_presentedStatus[playerID] != runtime.StatusType) {
                    _presentedStatus[playerID] = runtime.StatusType;
                    glow.SetStatus((StatusType)runtime.StatusType);
                }
                bool hyperArmor = state.HyperArmorFrames > 0;
                if (_presentedHyperArmor[playerID] != hyperArmor) {
                    _presentedHyperArmor[playerID] = hyperArmor;
                    glow.SetHyperArmor(hyperArmor);
                }
                bool invulnerable = state.InvulnerabilityFrames > 0;
                if (_presentedInvulnerable[playerID] != invulnerable) {
                    _presentedInvulnerable[playerID] = invulnerable;
                    glow.SetSpawnInvulnerability(invulnerable);
                }
            }

            int previousHP = _presentedHP[playerID];
            _presentedHP[playerID] = state.CurrentHP;
            if (previousHP >= 0 && state.CurrentHP < previousHP) {
                int damage = previousHP - state.CurrentHP;
                glow?.FlashHit();
                CameraShake.Instance?.Shake(damage * FighterHitShakeScale, FighterHitShakeDuration);
                HapticFeedbackManager.Instance?.VibrateForPlayer(playerID, 0.3f, 0.5f, 0.08f);
                // Same victim feedback Story shows on a hit: a floating number at
                // roughly mid-body. Presentation only.
                FTT.UI.FloatingDamageNumber.Show(
                    damage,
                    player.GlobalPosition + new Vector2(-10f, -46f),
                    GetParent());
            }

            // A blocked hit spends a charge without moving HP; give it Story's
            // small shake so absorbing a hit reads. Regen raises the count and
            // must stay silent, so only decreases outside a fresh daze count.
            int previousBlockCharges = _presentedBlockCharges[playerID];
            _presentedBlockCharges[playerID] = state.BlockCharges;
            if (previousBlockCharges > 0 && state.BlockCharges < previousBlockCharges
                && state.DazeFrames <= 0) {
                CameraShake.Instance?.Shake(3f, 0.08f);
            }

            // A fresh daze is the guard break: the sim has no block-broken event, so
            // the driver derives the beat from the daze edge for feedback only.
            int previousDaze = _presentedDazeFrames[playerID];
            _presentedDazeFrames[playerID] = state.DazeFrames;
            if (previousDaze <= 0 && state.DazeFrames > 0) {
                HapticFeedbackManager.Instance?.VibrateForPlayer(playerID, 0.8f, 0.9f, 0.2f);
            }
        }

        private void SyncPlayer(PlayerController player, int playerID) {
            if (player == null || !IsInstanceValid(player)
                || !Simulation.TryGetFighter(playerID, out FighterStateComponent state)
                || !Simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)) return;

            player.Position = WorldOrigin + new Vector2(
                state.Position.x.ToFloat() * PixelsPerUnit,
                -state.Position.y.ToFloat() * PixelsPerUnit);
            player.Velocity = new Vector2(
                state.Velocity.x.ToFloat() * PixelsPerUnit,
                -state.Velocity.y.ToFloat() * PixelsPerUnit);
            player.CurrentHP = state.CurrentHP;
            player.CurrentBlockCharges = state.BlockCharges;
            player.CurrentUltimateMeter = state.Influence.ToFloat();
            player.IsFacingRight = state.FacingRight != 0;
            player.SyncPresentationFacing();
            player.RemainingJumps = state.RemainingJumps;
            player.ComboCounter = runtime.ComboIndex;
            player.SpecialOneCooldownTimer = runtime.SpecialOneCooldownFrames / (float)FighterSimulation.TickRate;
            player.SpecialTwoCooldownTimer = runtime.SpecialTwoCooldownFrames / (float)FighterSimulation.TickRate;
            player.MovementAbilityCooldownTimer = runtime.MovementCooldownFrames / (float)FighterSimulation.TickRate;
            player.PlayPresentationAnimation(ResolvePresentationAnimation(in state, in runtime));

            // The melee active window shows the same yellow flash Story's
            // authored callbacks produce.
            bool meleeActive = runtime.AttackPhase == FighterBasicAttackRules.PhaseActive;
            if (_presentedMeleeActive[playerID] != meleeActive) {
                _presentedMeleeActive[playerID] = meleeActive;
                player.SetMeleePresentation(
                    meleeActive,
                    runtime.ComboIndex,
                    (runtime.AttackFlags & FighterBasicAttackRules.FlagAerial) != 0);
            }
            SyncPresentationFeedback(player, playerID, in state, in runtime);
        }

        /// <summary>
        /// Maps deterministic fighter state onto the shared placeholder animation
        /// set, one branch chain in priority order. Read-only over sim state.
        /// </summary>
        private static string ResolvePresentationAnimation(
            in FighterStateComponent state,
            in FighterRuntimeComponent runtime) {
            if (state.RespawnFramesRemaining > 0) return "respawn";
            if (state.HitstunFrames > 0) return "hitstun";
            if (state.DazeFrames > 0) return "dazed";
            if (FighterBasicAttackRules.IsSwinging(in runtime)) {
                int step = runtime.ComboIndex < 0 ? 0 : runtime.ComboIndex > 2 ? 2 : runtime.ComboIndex;
                return BasicAttackAnimationNames[step];
            }
            if (runtime.UniversalMovementState is (int)UniversalMovementPhase.RollStartup
                or (int)UniversalMovementPhase.RollTravel
                or (int)UniversalMovementPhase.RollRecovery) return "roll";
            if (FighterBasicAttackRules.IsBlockStance(in state, in runtime)) return "block";
            if (state.IsGrounded == 0) {
                return state.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero ? "jump" : "fall";
            }
            return xpTURN.Klotho.Deterministic.Math.FP64.Abs(state.Velocity.x)
                > xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(0.1)
                ? "run"
                : "idle";
        }

        private static void DisableNativeGameplay(PlayerController player) {
            player.ProcessMode = ProcessModeEnum.Disabled;
            DisableCollisionTree(player);
        }

        // === Match-flow presentation (countdown, stock loss, KO sequence) ===

        /// <summary>
        /// Attaches the shared Fighter-mode flow UI as driver children so every
        /// stage scene gets it without each stage controller wiring it up.
        /// </summary>
        private void AttachMatchFlowUI() {
            if (!IsInsideTree()) return;
            _camera = FindCamera(GetParent());
            if (GetNodeOrNull<FTT.UI.FighterPresentationOverlay>("FighterPresentationOverlay") == null) {
                AddChild(new FTT.UI.FighterPresentationOverlay { Name = "FighterPresentationOverlay" });
            }
            _pauseMenu = InstantiateUI<FTT.UI.LocalFighterPause>(PauseScenePath, "LocalFighterPause");
            _results = InstantiateUI<FTT.UI.MatchResults>(ResultsScenePath, "MatchResults");
            // Package 8 B2: the production HUD is attached here rather than authored
            // into each stage, so all ten stages and the Test Arena share one surface.
            _hud = InstantiateUI<FTT.UI.FighterHUD>(FTT.UI.FighterHUD.ScenePath, "FighterHUD");
            _hud?.Bind(this, _playerOne, _playerTwo);
        }

        private T InstantiateUI<T>(string scenePath, string nodeName) where T : Node {
            if (!ResourceLoader.Exists(scenePath)) return null;
            // Scenes are streamed presentation, never AuthoredResources cache entries.
            var packed = ResourceLoader.Load<PackedScene>(scenePath);
            if (packed?.Instantiate() is not T instance) return null;
            instance.Name = nodeName;
            AddChild(instance);
            return instance;
        }

        private static FTT.Combat.FighterCamera FindCamera(Node root) {
            if (root == null) return null;
            if (root is FTT.Combat.FighterCamera camera) return camera;
            foreach (Node child in root.GetChildren()) {
                if (child is FTT.Combat.FighterCamera found) return found;
            }
            return null;
        }

        /// <summary>Countdown ticks, the GO banner, and per-stock knockout beats.</summary>
        private void PublishMatchFlowPresentation() {
            FighterMatchComponent match = Simulation.GetMatchState();
            if (match.MatchState == FighterMatchStates.Countdown) {
                int digit = FighterMatchFlowRules.CountdownDigit(match.CountdownFramesRemaining);
                if (digit != _lastCountdownDigit) {
                    _lastCountdownDigit = digit;
                    Raise(new FighterPresentationPayload {
                        Phase = FighterPresentationPhase.Countdown,
                        WinnerPlayerID = -1,
                        SubjectPlayerID = -1,
                        CountdownValue = digit,
                        DurationSeconds = FighterMatchFlowRules.CountdownFramesPerDigit
                            / (float)FighterSimulation.TickRate
                    });
                }
                CaptureStocks();
                return;
            }

            if (!_matchStartRaised) {
                _matchStartRaised = true;
                Raise(new FighterPresentationPayload {
                    Phase = FighterPresentationPhase.MatchStart,
                    WinnerPlayerID = -1,
                    SubjectPlayerID = -1,
                    DurationSeconds = FighterMatchFlowRules.GoBannerFrames / (float)FighterSimulation.TickRate
                });
                // Package 8 B5: the match going live is the combat layer's cue. The
                // stage controller registered the set, which starts on Ambient under
                // the countdown.
                AudioManager.Instance?.SetIntensity(StemIntensity.Combat);
            }

            DetectStockLoss(match);
            UpdateLastStockClimax(in match);
        }

        // === Package 8 B5: match music intensity ===
        // Additive presentation only. Reads deterministic stock/mode fields and calls
        // the audio autoload; nothing here writes into scripts/FighterSim/ state.
        private bool _climaxEntered;

        /// <summary>
        /// Layers the climax stem in once a stock loss leaves either fighter on their
        /// last stock. One-way for the match: a Hybrid match cannot un-tense, and a
        /// fighter cannot regain a stock, so there is nothing to fall back from.
        /// The rule itself lives in <see cref="FighterAudioRules"/> so it is testable
        /// without a running match.
        /// </summary>
        private void UpdateLastStockClimax(in FighterMatchComponent match) {
            if (_climaxEntered) return;
            if (!FighterAudioRules.ModeUsesStocks(match.MatchMode)) return;
            if (!Simulation.TryGetFighter(0, out FighterStateComponent one)
                || !Simulation.TryGetFighter(1, out FighterStateComponent two)) return;
            if (!FighterAudioRules.IsLastStockClimax(one.Stocks, two.Stocks)) return;
            _climaxEntered = true;
            AudioManager.Instance?.SetIntensity(StemIntensity.Climax);
        }

        private void CaptureStocks() {
            for (int playerID = 0; playerID < 2; playerID++) {
                if (Simulation.TryGetFighter(playerID, out FighterStateComponent state)) {
                    _lastStocks[playerID] = state.Stocks;
                }
            }
        }

        private void DetectStockLoss(in FighterMatchComponent match) {
            for (int playerID = 0; playerID < 2; playerID++) {
                if (!Simulation.TryGetFighter(playerID, out FighterStateComponent state)) continue;
                int previous = _lastStocks[playerID];
                _lastStocks[playerID] = state.Stocks;
                if (previous < 0 || state.Stocks >= previous) continue;
                if (match.MatchState == FighterMatchStates.Complete) continue;
                _stockLossFreezeFrames = StockLossFreezeFrames;
                Raise(new FighterPresentationPayload {
                    Phase = FighterPresentationPhase.StockLost,
                    WinnerPlayerID = -1,
                    SubjectPlayerID = playerID,
                    DurationSeconds = StockLossFreezeFrames / (float)FighterSimulation.TickRate,
                    FocusPosition = PresentationPositionOf(playerID)
                });
                PlayCue(KnockoutStingerPath);
                CameraShake.Instance?.Shake(FighterKnockoutShake, FighterKnockoutShakeDuration);
                HapticFeedbackManager.Instance?.VibrateForPlayer(playerID, 0.9f, 1f, 0.35f);
            }
        }

        /// <summary>
        /// Drives the post-match cinematic. Hit-freeze defers advancing entirely
        /// and slow motion advances at a fractional rate; both only change *when*
        /// ticks are consumed, never what they contain.
        /// </summary>
        private void AdvanceKnockoutSequence(double delta) {
            _knockoutTimer += delta;
            switch (_knockoutStep) {
                case KnockoutStep.HitFreeze:
                    if (_knockoutTimer >= HitFreezeSeconds) EnterKnockoutStep(KnockoutStep.SlowMotion);
                    break;
                case KnockoutStep.SlowMotion:
                    _slowMotionCredit += SlowMotionRate;
                    while (_slowMotionCredit >= 1.0) {
                        _slowMotionCredit -= 1.0;
                        AdvanceInertFrame();
                    }
                    if (_knockoutTimer >= SlowMotionSeconds) EnterKnockoutStep(KnockoutStep.Stamp);
                    break;
                case KnockoutStep.Stamp:
                    if (_knockoutTimer >= StampSeconds) EnterKnockoutStep(KnockoutStep.WinnerPose);
                    break;
                case KnockoutStep.WinnerPose:
                    if (_knockoutTimer >= WinnerPoseSeconds) FinishKnockoutSequence();
                    break;
            }
        }

        /// <summary>
        /// One paced tick with neutral inputs. The match is already decided, so
        /// this cannot alter the outcome; it exists so lingering entities keep
        /// moving through the slow-motion beat.
        /// </summary>
        private void AdvanceInertFrame() {
            uint tick = unchecked((uint)Simulation.CurrentTick);
            Simulation.Advance(
                new PlayerInputFrame { Tick = tick },
                new PlayerInputFrame { Tick = tick });
            SyncPresentation();
        }

        private void BeginKnockoutSequence(in FighterMatchResult result) {
            _pendingResult = result;
            _slowMotionCredit = 0.0;
            EnterKnockoutStep(KnockoutStep.HitFreeze);
        }

        private void EnterKnockoutStep(KnockoutStep step) {
            _knockoutStep = step;
            _knockoutTimer = 0.0;
            int loser = _pendingResult.IsTrueTie
                ? -1
                : _pendingResult.WinnerPlayerID == 0 ? 1 : 0;
            switch (step) {
                case KnockoutStep.HitFreeze:
                    Raise(new FighterPresentationPayload {
                        Phase = FighterPresentationPhase.HitFreeze,
                        WinnerPlayerID = _pendingResult.WinnerPlayerID,
                        SubjectPlayerID = loser,
                        IsTrueTie = _pendingResult.IsTrueTie,
                        DurationSeconds = (float)HitFreezeSeconds,
                        FocusPosition = PresentationPositionOf(loser)
                    });
                    PlayCue(KnockoutStingerPath);
                    CameraShake.Instance?.Shake(FighterKnockoutShake, FighterKnockoutShakeDuration);
                    if (loser >= 0) {
                        HapticFeedbackManager.Instance?.VibrateForPlayer(loser, 1f, 1f, 0.5f);
                    }
                    if (_pendingResult.WinnerPlayerID >= 0) {
                        HapticFeedbackManager.Instance?.VibrateForPlayer(
                            _pendingResult.WinnerPlayerID, 0.3f, 0.4f, 0.25f);
                    }
                    break;
                case KnockoutStep.SlowMotion:
                    _camera?.FocusOn(PresentationPositionOf(loser), KnockoutFocusZoom);
                    Raise(new FighterPresentationPayload {
                        Phase = FighterPresentationPhase.SlowMotion,
                        WinnerPlayerID = _pendingResult.WinnerPlayerID,
                        SubjectPlayerID = loser,
                        IsTrueTie = _pendingResult.IsTrueTie,
                        DurationSeconds = (float)SlowMotionSeconds,
                        FocusPosition = PresentationPositionOf(loser)
                    });
                    break;
                case KnockoutStep.Stamp:
                    Raise(new FighterPresentationPayload {
                        Phase = FighterPresentationPhase.Spotlight,
                        WinnerPlayerID = _pendingResult.WinnerPlayerID,
                        SubjectPlayerID = loser,
                        IsTrueTie = _pendingResult.IsTrueTie,
                        DurationSeconds = (float)StampSeconds,
                        FocusPosition = PresentationPositionOf(_pendingResult.WinnerPlayerID)
                    });
                    Raise(new FighterPresentationPayload {
                        // A draw stamps "DRAW" and neither fighter plays a beat.
                        Phase = _pendingResult.IsTrueTie
                            ? FighterPresentationPhase.DrawStamp
                            : FighterPresentationPhase.KOStamp,
                        WinnerPlayerID = _pendingResult.WinnerPlayerID,
                        SubjectPlayerID = loser,
                        IsTrueTie = _pendingResult.IsTrueTie,
                        DurationSeconds = (float)StampSeconds
                    });
                    break;
                case KnockoutStep.WinnerPose:
                    if (!_pendingResult.IsTrueTie) {
                        _camera?.FocusOn(PresentationPositionOf(_pendingResult.WinnerPlayerID), KnockoutFocusZoom);
                        PlayCue(VictoryFanfarePath);
                    }
                    Raise(new FighterPresentationPayload {
                        Phase = FighterPresentationPhase.WinnerPose,
                        WinnerPlayerID = _pendingResult.WinnerPlayerID,
                        SubjectPlayerID = loser,
                        IsTrueTie = _pendingResult.IsTrueTie,
                        DurationSeconds = (float)WinnerPoseSeconds,
                        FocusPosition = PresentationPositionOf(_pendingResult.WinnerPlayerID)
                    });
                    break;
            }
        }

        private void FinishKnockoutSequence() {
            _knockoutStep = KnockoutStep.None;
            _knockoutTimer = 0.0;
            _camera?.ReleaseFocus();
            Raise(new FighterPresentationPayload {
                Phase = FighterPresentationPhase.Results,
                WinnerPlayerID = _pendingResult.WinnerPlayerID,
                SubjectPlayerID = -1,
                IsTrueTie = _pendingResult.IsTrueTie
            });
            if (_results != null && IsInstanceValid(_results)) _results.ShowResult(_pendingResult);
            MatchCompleted?.Invoke(_pendingResult);
        }

        private Vector2 PresentationPositionOf(int playerID) {
            PlayerController player = playerID == 0 ? _playerOne : playerID == 1 ? _playerTwo : null;
            if (player != null && IsInstanceValid(player)) return player.GlobalPosition;
            return WorldOrigin;
        }

        private static void Raise(FighterPresentationPayload payload) =>
            EventBus.Instance?.RaiseFighterPresentation(payload);

        /// <summary>
        /// Placeholder audio hook. The stems are Package 8 content; until they
        /// exist this resolves to null and AudioManager no-ops.
        /// </summary>
        private static void PlayCue(string streamPath) {
            if (!ResourceLoader.Exists(streamPath)) return;
            AudioManager.Instance?.PlaySFX(ResourceLoader.Load<AudioStream>(streamPath));
        }

        private void RaiseCompletionIfNeeded() {
            if (_completionRaised || Simulation == null) return;
            FighterMatchComponent match = Simulation.GetMatchState();
            if (match.MatchState != FighterMatchStates.Complete) return;
            _completionRaised = true;
            var result = new FighterMatchResult(
                match.WinnerPlayerID,
                match.IsTrueTie != 0,
                Simulation.CurrentTick,
                Simulation.CurrentHash);
            RecordStatistics(result);
            BeginKnockoutSequence(result);
        }

        private void RecordStatistics(in FighterMatchResult result) {
            SaveManager manager = SaveManager.Instance;
            if (manager == null) return;
            FighterMatchStatistics.Record(
                manager.GlobalData,
                _playerOne?.Data?.CharacterID,
                _playerTwo?.Data?.CharacterID,
                result.WinnerPlayerID,
                result.IsTrueTie);
            if (!result.IsTrueTie) manager.SaveGlobalData();
        }

        private static void DisableCollisionTree(Node node) {
            if (node is CollisionObject2D collisionObject) {
                collisionObject.CollisionLayer = 0;
                collisionObject.CollisionMask = 0;
            }
            if (node is Area2D area) {
                area.Monitoring = false;
                area.Monitorable = false;
            }
            foreach (Node child in node.GetChildren()) DisableCollisionTree(child);
        }
    }

    public readonly struct FighterMatchResult {
        public readonly int WinnerPlayerID;
        public readonly bool IsTrueTie;
        public readonly int CompletedTick;
        public readonly long FinalHash;

        public FighterMatchResult(int winnerPlayerID, bool isTrueTie, int completedTick, long finalHash) {
            WinnerPlayerID = winnerPlayerID;
            IsTrueTie = isTrueTie;
            CompletedTick = completedTick;
            FinalHash = finalHash;
        }
    }
}
