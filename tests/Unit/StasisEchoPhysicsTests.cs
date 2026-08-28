using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 Stasis Echo physics: the Echo's body layer (Environment |
/// PersistentObject) makes the plate template's authored mask physically
/// receive it — these cases drive the REAL collision path (engine frames,
/// area detection) instead of the old RegisterBody bypass — and an Echo
/// interposed in a searchlight beam occludes it via the space-state ray.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StasisEchoPhysicsTests {

    private const string PlateTemplatePath = "res://scenes/templates/PressurePlateTemplate.tscn";

    [TestCase(Timeout = 30000)]
    public async Task TheEchoPhysicallyPressesThePlateTemplate() {
        ISceneRunner runner = ISceneRunner.Load(PlateTemplatePath, true);
        await runner.SimulateFrames(2);
        var plate = runner.Scene() as PressurePlate;
        AssertObject(plate)
            .OverrideFailureMessage("PressurePlateTemplate.tscn no longer roots a PressurePlate.")
            .IsNotNull();
        AssertThat(plate.IsPressed).IsFalse();

        var echo = new StasisEcho { Name = "PlatePhysicsEcho", Position = Vector2.Zero };
        echo.SetLifeSecondsForTesting(600f);
        plate.AddChild(echo);
        await runner.SimulateFrames(10);

        AssertThat(plate.IsPressed)
            .OverrideFailureMessage(
                "The plate's authored mask (Player | PersistentObject) must physically receive the Echo body.")
            .IsTrue();
        AssertThat(plate.CurrentWeight).IsEqual(plate.PlayerWeight);

        // Removing the Echo releases the plate through the same physics path.
        echo.QueueFree();
        await runner.SimulateFrames(10);
        AssertThat(plate.IsPressed).IsFalse();
    }

    [TestCase(Timeout = 30000)]
    public async Task TheEchoBlocksTheSearchlightBeamWhileItInterposes() {
        ISceneRunner runner = ISceneRunner.Load(PlateTemplatePath, true);
        await runner.SimulateFrames(2);
        Node2D host = (Node2D)runner.Scene();

        var zone = new SearchlightZone {
            Name = "OcclusionZone",
            Mode = SearchlightMode.DelayedStrike,
            Enabled = false, // exposure is driven manually below
            ExposureGraceSeconds = 0.5f,
            Position = new Vector2(0, -400)
        };
        host.AddChild(zone);
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        player.Position = new Vector2(600, -400);
        // Freeze the player in place: these frames are for the physics space,
        // not for gravity.
        player.ProcessMode = Node.ProcessModeEnum.Disabled;
        host.AddChild(player);
        var echo = new StasisEcho { Name = "BeamEcho", Position = new Vector2(300, -400) };
        echo.SetLifeSecondsForTesting(600f);
        host.AddChild(echo);
        try {
            await runner.SimulateFrames(5);

            AssertThat(zone.IsBeamOccludedForPlayer(player))
                .OverrideFailureMessage("An Echo between beam origin and player must occlude the beam.")
                .IsTrue();

            zone.AddPlayer(player);
            for (int tick = 0; tick < 4; tick++) zone.TickExposure(0.5f);
            AssertThat(zone.StrikeCount)
                .OverrideFailureMessage("Exposure must suspend while the Echo interposes.")
                .IsEqual(0);
            AssertThat(zone.ExposureFor(player)).IsEqual(0f);

            // The Echo expires: the beam finds the player again.
            echo.QueueFree();
            await runner.SimulateFrames(5);
            AssertThat(zone.IsBeamOccludedForPlayer(player)).IsFalse();
            for (int tick = 0; tick < 2; tick++) zone.TickExposure(0.5f);
            AssertThat(zone.StrikeCount > 0)
                .OverrideFailureMessage("With the Echo gone, the delayed strike must land past the grace.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        }
    }

    [TestCase(Timeout = 30000)]
    public async Task TheEchoExpiresAfterItsLifetime() {
        ISceneRunner runner = ISceneRunner.Load(PlateTemplatePath, true);
        await runner.SimulateFrames(2);
        var echo = new StasisEcho { Name = "ExpiringEcho" };
        echo.SetLifeSecondsForTesting(3f / 60f);
        runner.Scene().AddChild(echo);

        await runner.SimulateFrames(10);
        AssertThat(!GodotObject.IsInstanceValid(echo) || echo.IsQueuedForDeletion())
            .OverrideFailureMessage("The Echo must free itself when its lifetime expires.")
            .IsTrue();
    }
}
