using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-11 (ledger <c>PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION</c>): the
/// load-time repair of saved binding rows that still carry the Godot 3 shoulder
/// numbers (4 = Back, 5 = Guide in Godot 4) after the gamepad defaults moved to
/// LB (9) / RB (10).
///
/// <para>A row is repaired only when the Controls tab could not have produced it
/// on purpose: an Ultimate gamepad half of exactly Back + Guide or, since 9.2
/// (2026-10-16), LB + RB — dropped to the now-empty pad default (a capture writes
/// one event, there is no chord editor), or a Movement Ability on Back / Special 2
/// on Guide while another action also holds that button (a capture of a held
/// button is refused). Anything else may be a deliberate bind and stays.</para>
///
/// <para>Pure C#: the defaults are a synthetic set mirroring the authored
/// post-fix layout, so no engine call is made. Raw joypad numbers are used on
/// purpose (no <c>Godot.JoyButton</c>), which keeps the suite free of engine types;
/// <c>GamepadDefaultBindingTests</c> pins the authored file and the enum.</para>
/// </summary>
[TestSuite]
public class GamepadOverrideRepairTests {

    private const int Back = 4;
    private const int Guide = 5;
    private const int LeftStick = 7;
    private const int LeftShoulder = 9;
    private const int RightShoulder = 10;

    // Physical keycodes (Godot's Key enum values) used by the synthetic rows.
    private const int KeyF = 70;
    private const int KeyK = 75;
    private const int KeyL = 76;
    private const int KeyR = 82;
    private const int KeyT = 84;
    private const int KeyU = 85;
    private const int KeyY = 89;
    private const int KeyShift = 4194325;
    private const int MouseMiddle = 3;

    private static InputBindingEvent Key(int code) => new(InputBindingKind.Key, code);
    private static InputBindingEvent Mouse(int code) => new(InputBindingKind.MouseButton, code);
    private static InputBindingEvent Pad(int code) => new(InputBindingKind.JoyButton, code);

    /// <summary>
    /// The authored post-fix defaults of the actions the repair reads, including
    /// Time Freeze on L3 since D11 (2026-10-11, <c>USER-GAMEPAD-TIME-FREEZE-L3</c>)
    /// and, since 9.2 (2026-10-16), the Ultimate's empty gamepad direct slot: the
    /// pad Ultimate is the Movement Ability + Special 2 recipe chord.
    /// </summary>
    private static InputBindingSet PostFixDefaults() {
        var defaults = new InputBindingSet();
        defaults.Set(InputManager.Actions.MovementAbility, new[] { Key(KeyShift), Pad(LeftShoulder) });
        defaults.Set(InputManager.Actions.Special2, new[] { Key(KeyL), Mouse(MouseMiddle), Pad(RightShoulder) });
        defaults.Set(InputManager.Actions.Ultimate, new[] { Key(KeyU) });
        defaults.Set(InputManager.Actions.TimeFreeze, new[] { Key(KeyR), Pad(LeftStick) });
        defaults.Set(InputManager.Actions.Jump, new[] { Key(32), Pad(0) });
        return defaults;
    }

    private static string Describe(IEnumerable<InputBindingEvent> events) =>
        "[" + string.Join(", ", events.Select(binding => binding.Signature)) + "]";

    private static void AssertRow(InputBindingSet set, string action, params InputBindingEvent[] expected) {
        AssertThat(Describe(set.For(action)))
            .OverrideFailureMessage($"{action} is {Describe(set.For(action))}, expected {Describe(expected)}.")
            .IsEqual(Describe(expected));
    }

    [TestCase]
    public void AStaleGodot3UltimateChordBesideAReboundKeyIsDroppedToTheEmptyPadDefault() {
        // A keyboard-only rebind of the Ultimate (U -> Y) before the fix saved the
        // whole row, freezing the old Back + Guide chord into the payload. Since
        // 9.2 the pad default is empty (the recipe chord), so the stale pair is
        // dropped rather than moved onto the shoulders.
        var saved = new InputBindingSet();
        saved.Set(InputManager.Actions.Ultimate, new[] { Key(KeyY), Pad(Back), Pad(Guide) });

        AssertThat(saved.RepairGodot3ShoulderRows(PostFixDefaults())).IsTrue();
        AssertRow(saved, InputManager.Actions.Ultimate, Key(KeyY));
        // Nothing else gained a row: an untouched action keeps inheriting its default.
        AssertThat(saved.Actions.Count).IsEqual(1);

        // Idempotent: a second load finds nothing to repair and changes nothing.
        AssertThat(saved.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertRow(saved, InputManager.Actions.Ultimate, Key(KeyY));
    }

    /// <summary>
    /// 9.2 (2026-10-16, amends <c>PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION</c> (1)):
    /// the 2026-10-11 default LB + RB (9 + 10) frozen into a saved Ultimate row is
    /// dropped exactly like the Godot 3 Back + Guide; the keyboard half is kept,
    /// whatever it is, and a single captured pad button is never touched.
    /// </summary>
    [TestCase]
    public void StaleShoulderAndGodot3UltimateRowsAreDroppedAndTheKeyboardIsKept() {
        var shoulders = new InputBindingSet();
        shoulders.Set(InputManager.Actions.Ultimate, new[] { Key(KeyY), Pad(LeftShoulder), Pad(RightShoulder) });
        AssertThat(shoulders.RepairGodot3ShoulderRows(PostFixDefaults())).IsTrue();
        AssertRow(shoulders, InputManager.Actions.Ultimate, Key(KeyY));
        AssertThat(shoulders.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();

        // The default keyboard key beside the old default pad pair: only the pair goes.
        var defaultKey = new InputBindingSet();
        defaultKey.Set(InputManager.Actions.Ultimate, new[] { Key(KeyU), Pad(RightShoulder), Pad(LeftShoulder) });
        AssertThat(defaultKey.RepairGodot3ShoulderRows(PostFixDefaults())).IsTrue();
        AssertRow(defaultKey, InputManager.Actions.Ultimate, Key(KeyU));

        var godot3 = new InputBindingSet();
        godot3.Set(InputManager.Actions.Ultimate, new[] { Key(KeyU), Pad(Guide), Pad(Back) });
        AssertThat(godot3.RepairGodot3ShoulderRows(PostFixDefaults())).IsTrue();
        AssertRow(godot3, InputManager.Actions.Ultimate, Key(KeyU));

        // One captured pad button is a deliberate direct bind (C01c capture stays legal).
        var direct = new InputBindingSet();
        direct.Set(InputManager.Actions.Ultimate, new[] { Key(KeyU), Pad(3) });
        AssertThat(direct.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertRow(direct, InputManager.Actions.Ultimate, Key(KeyU), Pad(3));
    }

    [TestCase]
    public void AStaleMovementAbilityOnBackSharedWithTimeFreezeMovesToTheLeftShoulder() {
        // Shift -> F on the keyboard before the fix left the Movement Ability on
        // Back. Time Freeze's default has been L3 since D11, so Back is shared
        // only with a saved Time Freeze row that froze the old Back: R -> T on the
        // keyboard before D11 stored the whole row. One press, two verbs.
        var saved = new InputBindingSet();
        saved.Set(InputManager.Actions.MovementAbility, new[] { Key(KeyF), Pad(Back) });
        saved.Set(InputManager.Actions.TimeFreeze, new[] { Key(KeyT), Pad(Back) });

        AssertThat(saved.RepairGodot3ShoulderRows(PostFixDefaults())).IsTrue();
        AssertRow(saved, InputManager.Actions.MovementAbility, Key(KeyF), Pad(LeftShoulder));
        // The saved Time Freeze row is the player's and is kept exactly: Back is a
        // valid Godot 4 binding, and nothing migrates it to L3.
        AssertRow(saved, InputManager.Actions.TimeFreeze, Key(KeyT), Pad(Back));
        AssertThat(saved.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();

        // A stale Ultimate chord of Back + Guide also holds Back as loaded, so it
        // still marks a Movement Ability on Back as stale with no Time Freeze row;
        // both rows are repaired in the same pass.
        var chord = new InputBindingSet();
        chord.Set(InputManager.Actions.MovementAbility, new[] { Key(KeyF), Pad(Back) });
        chord.Set(InputManager.Actions.Ultimate, new[] { Key(KeyY), Pad(Back), Pad(Guide) });

        AssertThat(chord.RepairGodot3ShoulderRows(PostFixDefaults())).IsTrue();
        AssertRow(chord, InputManager.Actions.MovementAbility, Key(KeyF), Pad(LeftShoulder));
        AssertRow(chord, InputManager.Actions.Ultimate, Key(KeyY));
        AssertThat(chord.HasOverride(InputManager.Actions.TimeFreeze)).IsFalse();
        AssertThat(chord.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
    }

    [TestCase]
    public void AStaleSpecialTwoOnGuideIsRepairedBesideTheStaleUltimateChord() {
        // Both rows rebound on the keyboard before the fix: Special 2 still shares
        // Guide with the stale chord, which no capture could have produced.
        var saved = new InputBindingSet();
        saved.Set(InputManager.Actions.Special2, new[] { Key(KeyK), Mouse(MouseMiddle), Pad(Guide) });
        saved.Set(InputManager.Actions.Ultimate, new[] { Key(KeyY), Pad(Back), Pad(Guide) });

        AssertThat(saved.RepairGodot3ShoulderRows(PostFixDefaults())).IsTrue();
        AssertRow(saved, InputManager.Actions.Special2, Key(KeyK), Mouse(MouseMiddle), Pad(RightShoulder));
        AssertRow(saved, InputManager.Actions.Ultimate, Key(KeyY));
    }

    [TestCase]
    public void ABackOrGuideBindNoOtherActionHoldsIsLeftAsADeliberateChoice() {
        // Time Freeze's gamepad slot was cleared, so Back is free: a Movement
        // Ability on Back may have been captured on purpose after the fix.
        var saved = new InputBindingSet();
        saved.Set(InputManager.Actions.MovementAbility, new[] { Key(KeyF), Pad(Back) });
        saved.SetUnbound(InputManager.Actions.TimeFreeze, InputDeviceKind.Joypad, unbound: true);
        // Guide is free after the fix, so a lone Special 2 on Guide is the same.
        saved.Set(InputManager.Actions.Special2, new[] { Key(KeyK), Pad(Guide) });

        AssertThat(saved.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertRow(saved, InputManager.Actions.MovementAbility, Key(KeyF), Pad(Back));
        AssertRow(saved, InputManager.Actions.Special2, Key(KeyK), Pad(Guide));

        // A direct Ultimate on Back alone is one captured event, never the old chord.
        var direct = new InputBindingSet();
        direct.Set(InputManager.Actions.Ultimate, new[] { Key(KeyU), Pad(Back) });
        direct.SetUnbound(InputManager.Actions.TimeFreeze, InputDeviceKind.Joypad, unbound: true);
        AssertThat(direct.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertRow(direct, InputManager.Actions.Ultimate, Key(KeyU), Pad(Back));

        // Time Freeze's default moved to L3 (D11), so Back is free; this is also the
        // shape of a pre-fix keyboard-only Movement rebind, which is no longer
        // repaired (PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION (ii)).
        var lone = new InputBindingSet();
        lone.Set(InputManager.Actions.MovementAbility, new[] { Key(KeyF), Pad(Back) });
        AssertThat(lone.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertRow(lone, InputManager.Actions.MovementAbility, Key(KeyF), Pad(Back));
        AssertThat(lone.Actions.Count).IsEqual(1);
    }

    [TestCase]
    public void CurrentRowsEmptyPayloadsAndOldNumberDefaultsAreLeftAlone() {
        // An empty payload and a missing defaults set are no-ops.
        var empty = new InputBindingSet();
        AssertThat(empty.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertThat(empty.Actions.Count).IsEqual(0);

        var current = new InputBindingSet();
        current.Set(InputManager.Actions.MovementAbility, new[] { Key(KeyF), Pad(LeftShoulder) });
        current.Set(InputManager.Actions.Ultimate, new[] { Key(KeyY) });
        AssertThat(current.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertThat(current.RepairGodot3ShoulderRows(null)).IsFalse();
        AssertRow(current, InputManager.Actions.MovementAbility, Key(KeyF), Pad(LeftShoulder));

        // A cleared gamepad Ultimate slot stays cleared.
        var cleared = new InputBindingSet();
        cleared.Set(InputManager.Actions.Ultimate, new[] { Key(KeyY) });
        cleared.SetUnbound(InputManager.Actions.Ultimate, InputDeviceKind.Joypad, unbound: true);
        AssertThat(cleared.RepairGodot3ShoulderRows(PostFixDefaults())).IsFalse();
        AssertThat(cleared.IsUnbound(InputManager.Actions.Ultimate, InputDeviceKind.Joypad)).IsTrue();
        AssertRow(cleared, InputManager.Actions.Ultimate, Key(KeyY));

        // A project file that still ships the old numbers has nothing to repair towards.
        var oldDefaults = new InputBindingSet();
        oldDefaults.Set(InputManager.Actions.Ultimate, new[] { Key(KeyU), Pad(Back), Pad(Guide) });
        var stale = new InputBindingSet();
        stale.Set(InputManager.Actions.Ultimate, new[] { Key(KeyY), Pad(Back), Pad(Guide) });
        AssertThat(stale.RepairGodot3ShoulderRows(oldDefaults)).IsFalse();
        AssertRow(stale, InputManager.Actions.Ultimate, Key(KeyY), Pad(Back), Pad(Guide));
    }
}
