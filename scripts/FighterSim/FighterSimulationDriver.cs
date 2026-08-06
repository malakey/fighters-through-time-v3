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

        private PlayerController _playerOne;
        private PlayerController _playerTwo;
        private FighterCpuController _cpuController;
        private PlayerInputFrame _previousCpuFrame;
        private bool _completionRaised;
        private readonly Queue<ColorRect> _inactiveProxies = new();
        private readonly Dictionary<int, ColorRect> _projectileProxies = new();
        private readonly Dictionary<int, ColorRect> _persistentProxies = new();
        private readonly Dictionary<int, ColorRect> _hazardProxies = new();
        private readonly Dictionary<int, ColorRect> _orbProxies = new();
        private readonly HashSet<int> _seenProxyIDs = new();
        private readonly List<int> _releaseProxyIDs = new();
        private readonly List<FighterProjectileComponent> _projectiles = new(64);
        private readonly List<FighterPersistentObjectComponent> _persistentObjects = new(16);
        private readonly List<FighterHazardComponent> _hazards = new(16);
        private readonly List<FighterOrbComponent> _orbs = new(16);

        public event Action<FighterMatchResult> MatchCompleted;

        public FighterSimulation Simulation { get; private set; }
        public bool IsInitialized => Simulation != null;
        public long CurrentHash => Simulation?.CurrentHash ?? 0L;
        public int CurrentTick => Simulation?.CurrentTick ?? 0;

        public void Initialize(
            PlayerController playerOne,
            PlayerController playerTwo,
            MatchSettings settings,
            int stageHazardTypeID = 1) {
            if (Simulation != null) throw new InvalidOperationException("Fighter simulation is already initialized.");
            _playerOne = playerOne ?? throw new ArgumentNullException(nameof(playerOne));
            _playerTwo = playerTwo ?? throw new ArgumentNullException(nameof(playerTwo));
            if (_playerOne.Data == null || _playerTwo.Data == null) {
                throw new InvalidOperationException("Both presentation fighters require CharacterData.");
            }

            DisableNativeGameplay(_playerOne);
            DisableNativeGameplay(_playerTwo);
            int matchSeconds = Math.Max(1, Mathf.RoundToInt(settings.TimeLimit));
            Simulation = new FighterSimulation(
                FighterLoadoutFactory.FromCharacterData(_playerOne.Data),
                FighterLoadoutFactory.FromCharacterData(_playerTwo.Data),
                Math.Max(1, settings.StockCount),
                matchSeconds,
                rules: new FighterMatchRules(
                    (int)settings.Mode,
                    settings.ItemsEnabled,
                    (int)settings.ItemSpawnRate,
                    settings.StageHazardsEnabled,
                    (int)settings.HazardRate,
                    stageHazardTypeID));
            SessionData session = GameManager.Instance?.CurrentSession ?? default;
            if (session.FighterOpponentType == FighterOpponentType.Cpu) {
                _cpuController = new FighterCpuController(session.CpuDifficulty, 2026);
            }
            WarmPresentationProxies(112);
            AddToGroup("FighterSimulation");
            SyncPresentation();
        }

        public override void _PhysicsProcess(double delta) {
            if (Simulation == null) return;
            FighterMatchComponent match = Simulation.GetMatchState();
            if (match.MatchState != 1) return;

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
            if (runtime.UniversalMovementState is (int)UniversalMovementPhase.RollStartup
                or (int)UniversalMovementPhase.RollTravel
                or (int)UniversalMovementPhase.RollRecovery) return "Rolling";
            if (runtime.UniversalMovementState == (int)UniversalMovementPhase.Dash) return "Dashing";
            if (state.InvulnerabilityFrames > 0) return "Respawning";
            if (state.DazeFrames > 0) return "Dazed";
            if (state.HitstunFrames > 0) return "Stunned";
            if ((runtime.HeldButtons & (int)GameplayButtons.Block) != 0) return "Blocking";
            return state.IsGrounded != 0 ? "Grounded" : "Airborne";
        }

        private void SyncPresentation() {
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

            Simulation.CopyHazardsTo(_hazards);
            _seenProxyIDs.Clear();
            foreach (FighterHazardComponent hazard in _hazards) {
                ColorRect proxy = GetProxy(_hazardProxies, hazard.EntityID);
                ConfigureProxy(
                    proxy, hazard.Position, hazard.HalfExtents,
                    HazardColor(hazard.HazardTypeID, hazard.Phase == 0));
                _seenProxyIDs.Add(hazard.EntityID);
            }
            ReleaseMissing(_hazardProxies);

            Simulation.CopyOrbsTo(_orbs);
            _seenProxyIDs.Clear();
            foreach (FighterOrbComponent orb in _orbs) {
                ColorRect proxy = GetProxy(_orbProxies, orb.EntityID);
                ConfigureProxy(
                    proxy, orb.Position, orb.HalfExtents,
                    orb.EffectType switch {
                        0 => new Color(0.2f, 1f, 0.35f, 0.95f),
                        1 => new Color(1f, 0.85f, 0.1f, 0.95f),
                        2 => new Color(0.4f, 0.75f, 1f, 0.95f),
                        _ => new Color(0.75f, 0.35f, 1f, 0.95f)
                    });
                _seenProxyIDs.Add(orb.EntityID);
            }
            ReleaseMissing(_orbProxies);
        }

        private static Color HazardColor(int hazardTypeID, bool warning) {
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
            return warning ? new Color(active.R, active.G, active.B, 0.3f) : active;
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
            Color color) {
            Vector2 size = new(
                halfExtents.x.ToFloat() * PixelsPerUnit * 2f,
                halfExtents.y.ToFloat() * PixelsPerUnit * 2f);
            Vector2 center = WorldOrigin + new Vector2(
                position.x.ToFloat() * PixelsPerUnit,
                -position.y.ToFloat() * PixelsPerUnit);
            proxy.Size = size;
            proxy.Position = center - size / 2f;
            proxy.Color = color;
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
                _inactiveProxies.Enqueue(proxy);
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
            player.RemainingJumps = state.RemainingJumps;
            player.ComboCounter = runtime.ComboIndex;
            player.SpecialOneCooldownTimer = runtime.SpecialOneCooldownFrames / (float)FighterSimulation.TickRate;
            player.SpecialTwoCooldownTimer = runtime.SpecialTwoCooldownFrames / (float)FighterSimulation.TickRate;
            player.MovementAbilityCooldownTimer = runtime.MovementCooldownFrames / (float)FighterSimulation.TickRate;
            if (runtime.UniversalMovementState == (int)UniversalMovementPhase.Dash) {
                player.PlayPresentationAnimation("dash");
            } else if (runtime.UniversalMovementState is (int)UniversalMovementPhase.RollStartup
                or (int)UniversalMovementPhase.RollTravel
                or (int)UniversalMovementPhase.RollRecovery) {
                player.PlayPresentationAnimation("roll");
            }
        }

        private static void DisableNativeGameplay(PlayerController player) {
            player.ProcessMode = ProcessModeEnum.Disabled;
            DisableCollisionTree(player);
        }

        private void RaiseCompletionIfNeeded() {
            if (_completionRaised || Simulation == null) return;
            FighterMatchComponent match = Simulation.GetMatchState();
            if (match.MatchState != 2) return;
            _completionRaised = true;
            var result = new FighterMatchResult(
                match.WinnerPlayerID,
                match.IsTrueTie != 0,
                Simulation.CurrentTick,
                Simulation.CurrentHash);
            RecordStatistics(result);
            MatchCompleted?.Invoke(result);
        }

        private static void RecordStatistics(in FighterMatchResult result) {
            SaveManager manager = SaveManager.Instance;
            if (manager == null || result.IsTrueTie) return;
            if (result.WinnerPlayerID == 0) manager.GlobalData.TotalWins++;
            else manager.GlobalData.TotalLosses++;
            manager.SaveGlobalData();
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
