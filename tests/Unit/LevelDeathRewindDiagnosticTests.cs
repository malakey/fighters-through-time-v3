using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Diagnostic: reproduce the in-game death-triggered rewind inside a real
/// campaign level with real engine frames (deferred calls flush, all scene
/// services present) — the harness-level DeathTriggeredRewindTests pass, but
/// the user reports a permanent freeze on death in actual play.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LevelDeathRewindDiagnosticTests {

    [TestCase(Timeout = 120000)]
    public async Task DeathInsideARealLevelRewindsAndRevivesThePlayer() {
        SessionData session = GameManager.Instance.CurrentSession;
        session.SelectedCharacterID = "einstein";
        GameManager.Instance.CurrentSession = session;
        StoryManager.Instance?.SetRewinds(3);

        ISceneRunner runner = ISceneRunner.Load("res://scenes/campaign/Level_02_Orleans.tscn", true);
        await runner.SimulateFrames(30);

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var player = tree.GetFirstNodeInGroup("StoryPlayer") as PlayerController;
        AssertObject(player)
            .OverrideFailureMessage("Level 2 booted without a StoryPlayer group member.")
            .IsNotNull();

        // Drive the entrance dialogue to completion (it pauses the tree), then
        // let the level settle and the rewind buffer record some frames.
        FTT.UI.DialogueManager dialogue = null;
        foreach (Node child in runner.Scene().GetChildren()) {
            if (child is FTT.UI.DialogueManager found) { dialogue = found; break; }
        }
        for (int i = 0; i < 200 && dialogue != null && dialogue.IsSequenceActive; i++) {
            dialogue.AdvanceLine();
            await runner.SimulateFrames(2);
        }
        GD.Print($"DIAG dialogueActive={dialogue?.IsSequenceActive} treePausedPreKill={tree.Paused}");
        await runner.SimulateFrames(90);
        GD.Print($"DIAG preKill state={player.CurrentState} pos={player.GlobalPosition} treePaused={tree.Paused}");

        var manager = runner.Scene().GetNodeOrNull<ChronalRewindManager>("ChronalRewindManager");
        AssertObject(manager)
            .OverrideFailureMessage("Level 2 has no ChronalRewindManager attached.")
            .IsNotNull();
        GD.Print($"DIAG rewinds={manager.RemainingRewinds} playerState={player.CurrentState} hp={player.CurrentHP}");

        // Kill exactly as gameplay does: inside the physics-callback guard.
        using (PhysicsCallbackGuard.Enter()) {
            player.ApplyDamage(9999);
        }
        GD.Print($"DIAG after kill: state={player.CurrentState} rewinding={manager.IsRewinding}");

        for (int i = 0; i < 4 && player.CurrentState == CharacterState.Dead; i++) {
            await runner.SimulateFrames(30);
            GD.Print($"DIAG t+{(i + 1) * 30}f state={player.CurrentState} rewinding={manager.IsRewinding} pos={player.GlobalPosition}"
                + $" treePaused={tree.Paused} managerCanProcess={manager.CanProcess()} managerMode={manager.ProcessMode}"
                + $" playerMode={player.ProcessMode} rootMode={runner.Scene().ProcessMode}");
        }

        AssertThat(manager.IsRewinding)
            .OverrideFailureMessage($"Rewind still running after 6 s (state={player.CurrentState}).")
            .IsFalse();
        AssertThat(player.CurrentState == CharacterState.Dead)
            .OverrideFailureMessage("Player stranded dead in a real level — in-game freeze reproduced.")
            .IsFalse();
    }
}
