using FTT.Core;
using FTT.Characters;
using Godot;
using FTT.Combat;

namespace FTT.FighterSim {

    // FU-KNOCK (feel and UI update 2026-10-25, ruling R2): the wall bounce's
    // impact burst and camera shake, Fighter side. Presentation only - read-only
    // over component 323's wrapping bounce counter (bits 8-11); nothing here
    // writes FighterSim state. The cache is cosmetic, edge-detected like the
    // perfect-block flash (-1 until the first read, so a rematch or a restored
    // snapshot never replays an old bounce), and compared with != because the
    // counter wraps and a stock loss zeroes it.
    public partial class FighterSimulationDriver {

        private const string WallBounceImpactScenePath = "res://scenes/vfx/VfxImpact.tscn";
        private const float WallBounceShakeIntensity = 6f;
        private const float WallBounceShakeSeconds = 0.15f;
        private const float WallBounceChestOffsetPixels = 40f;
        private const float WallBounceWallInsetPixels = 24f;

        private readonly int[] _presentedWallBounceCount = { -1, -1 };
        private PackedScene _wallBounceImpactScene;

        /// <summary>
        /// Plays the impact burst at the wall contact (the fighter's chest, on the
        /// wall side - away from its rebound), a short camera shake through
        /// <c>CameraShake</c> (so the accessibility scale applies) and no audio cue.
        /// </summary>
        private void SyncWallBounce(PlayerController player, int playerID, in FighterStateComponent state) {
            if (Simulation == null || !Simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action)) return;
            int count = FighterKnockbackFeelRules.BounceCount(in action);
            int previous = _presentedWallBounceCount[playerID];
            _presentedWallBounceCount[playerID] = count;
            if (previous < 0 || count == previous) return;
            // A stock loss or respawn hold zeroes component 323; that is not a bounce.
            if (count == 0 && !FighterKnockbackFeelRules.HasBounced(in action)) return;

            // The rebound leaves the wall, so the wall is on the side opposite the velocity.
            float wallSide = state.Velocity.x.ToFloat() > 0f ? -1f : 1f;
            Vector2 contact = player.GlobalPosition
                + new Vector2(wallSide * WallBounceWallInsetPixels, -WallBounceChestOffsetPixels);
            _wallBounceImpactScene ??= ResourceLoader.Load<PackedScene>(WallBounceImpactScenePath);
            Node2D burst = VfxEmitter.EmitScene(_wallBounceImpactScene, contact, GetParent());
            if (burst != null) burst.Scale = new Vector2(-wallSide, 1f);
            CameraShake.Instance?.Shake(WallBounceShakeIntensity, WallBounceShakeSeconds);
            HapticFeedbackManager.Instance?.OnGuardImpact(playerID);
        }
    }
}
