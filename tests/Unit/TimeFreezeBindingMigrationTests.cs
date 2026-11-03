using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A2 (F03 / C-2): the saved-binding key rename from the retired
/// <c>gameplay_rewind</c> action onto <c>gameplay_time_freeze</c>.
///
/// <para><b>Why a migration is needed at all.</b> Time Freeze inherited the
/// retired action's default events unchanged (<c>R</c> / gamepad Back-Select),
/// so a player who never remapped it has nothing stored — the override set only
/// records rows that differ from <c>project.godot</c>. A player who <i>did</i>
/// remap it has a row keyed by an action name that no longer exists, and the
/// restore path drops unknown names on purpose (audit §5.2). Without this, a
/// remapped Time Freeze silently reverts to <c>R</c>.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TimeFreezeBindingMigrationTests {

    private static InputBindingSet SetWith(string action, InputBindingEvent binding) {
        var set = new InputBindingSet();
        set.Set(action, new[] { binding });
        return set;
    }

    [TestCase]
    public void ACustomizedRewindBindingMovesOntoTheTimeFreezeAction() {
        // The player had rebound the old verb to F (physical keycode 70).
        var binding = new InputBindingEvent(InputBindingKind.Key, 70);
        InputBindingSet set = SetWith(InputManager.Actions.LegacyRewind, binding);

        AssertThat(set.MigrateLegacyRewindAction())
            .OverrideFailureMessage("A stored legacy row must report that it migrated.")
            .IsTrue();

        AssertThat(set.HasOverride(InputManager.Actions.LegacyRewind))
            .OverrideFailureMessage("The dead action name must not survive the migration.")
            .IsFalse();
        AssertThat(set.HasOverride(InputManager.Actions.TimeFreeze)).IsTrue();
        AssertThat(set.For(InputManager.Actions.TimeFreeze).Count).IsEqual(1);
        AssertThat(set.For(InputManager.Actions.TimeFreeze)[0].Matches(binding))
            .OverrideFailureMessage("The player's chosen event must arrive unchanged.")
            .IsTrue();

        // Gameplay review 2026-11-03 (10b++): the verb is off, so neither name is remappable. The migrated
        // row stays in the set (a re-enable restores it) but the restore filter ignores it.
        AssertThat(System.Array.IndexOf(InputManager.RemappableActions, InputManager.Actions.TimeFreeze) >= 0)
            .IsFalse();
        AssertThat(System.Array.IndexOf(InputManager.RemappableActions, InputManager.Actions.LegacyRewind) >= 0)
            .IsFalse();

        // Idempotent: a second pass has nothing left to move.
        AssertThat(set.MigrateLegacyRewindAction()).IsFalse();
        AssertThat(set.For(InputManager.Actions.TimeFreeze)[0].Matches(binding)).IsTrue();
    }

    [TestCase]
    public void AnExplicitTimeFreezeBindingIsNeverOverwrittenByTheLegacyRow() {
        // The player rebound the NEW action to G (71) and still carries a stale
        // legacy row bound to F (70) from before the rename. The newer, explicit
        // choice must win, and the stale row must be dropped rather than applied.
        var legacy = new InputBindingEvent(InputBindingKind.Key, 70);
        var chosen = new InputBindingEvent(InputBindingKind.Key, 71);
        var set = new InputBindingSet();
        set.Set(InputManager.Actions.LegacyRewind, new[] { legacy });
        set.Set(InputManager.Actions.TimeFreeze, new[] { chosen });

        AssertThat(set.MigrateLegacyRewindAction()).IsTrue();

        AssertThat(set.HasOverride(InputManager.Actions.LegacyRewind)).IsFalse();
        AssertThat(set.For(InputManager.Actions.TimeFreeze).Count).IsEqual(1);
        AssertThat(set.For(InputManager.Actions.TimeFreeze)[0].Matches(chosen))
            .OverrideFailureMessage("An explicit newer Time Freeze bind must not be overwritten.")
            .IsTrue();

        // A payload with no legacy row at all is left completely alone.
        InputBindingSet untouched = SetWith(InputManager.Actions.TimeFreeze, chosen);
        AssertThat(untouched.MigrateLegacyRewindAction()).IsFalse();
        AssertThat(untouched.For(InputManager.Actions.TimeFreeze)[0].Matches(chosen)).IsTrue();
    }

    /// <summary>
    /// Meter and Defy update (VERIFY-TIME-FREEZE-ON-BACK-SHARED): a saved Time Freeze
    /// row on Back now shares it with the Dialogue Log. No repair touches it.
    /// </summary>
    [TestCase]
    public void ASavedTimeFreezeRowOnBackIsKeptByEveryRepair() {
        var set = new InputBindingSet();
        var key = new InputBindingEvent(InputBindingKind.Key, 82);
        var back = new InputBindingEvent(InputBindingKind.JoyButton, 4);
        set.Set(InputManager.Actions.TimeFreeze, new[] { key, back });
        var defaults = new InputBindingSet();
        defaults.Set(InputManager.Actions.TimeFreeze, new[] { key, new InputBindingEvent(InputBindingKind.JoyButton, 7) });
        defaults.Set(InputManager.Actions.Defy, new[] { new InputBindingEvent(InputBindingKind.Key, 70), new InputBindingEvent(InputBindingKind.JoyButton, 8) });

        AssertThat(set.RepairGodot3ShoulderRows(defaults)).IsFalse();
        AssertThat(set.RepairDefyPadDefaultCollision(defaults)).IsFalse();
        AssertThat(set.For(InputManager.Actions.TimeFreeze).Count).IsEqual(2);
        AssertThat(set.For(InputManager.Actions.TimeFreeze)[1].Matches(back)).IsTrue();
        AssertThat(set.IsUnbound(InputManager.Actions.Defy, InputDeviceKind.Joypad)).IsFalse();
    }
}
