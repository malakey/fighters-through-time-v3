using System;
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

        public FighterSimulation Simulation { get; private set; }
        public bool IsInitialized => Simulation != null;
        public long CurrentHash => Simulation?.CurrentHash ?? 0L;
        public int CurrentTick => Simulation?.CurrentTick ?? 0;

        public void Initialize(
            PlayerController playerOne,
            PlayerController playerTwo,
            MatchSettings settings) {
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
                matchSeconds);
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
            PlayerInputFrame playerTwoInput = InputManager.Instance?.GetFrame(1) ?? default;
            playerOneInput.Tick = tick;
            playerTwoInput.Tick = tick;
            Simulation.Advance(playerOneInput, playerTwoInput);
            SyncPresentation();
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
            if (state.InvulnerabilityFrames > 0) return "Respawning";
            if (state.DazeFrames > 0) return "Dazed";
            if (state.HitstunFrames > 0) return "Stunned";
            if ((runtime.HeldButtons & (int)GameplayButtons.Block) != 0) return "Blocking";
            return state.IsGrounded != 0 ? "Grounded" : "Airborne";
        }

        private void SyncPresentation() {
            SyncPlayer(_playerOne, 0);
            SyncPlayer(_playerTwo, 1);
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
        }

        private static void DisableNativeGameplay(PlayerController player) {
            player.ProcessMode = ProcessModeEnum.Disabled;
            DisableCollisionTree(player);
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
}
