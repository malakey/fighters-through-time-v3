using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// V7.6 (Package 11 A7a): the campaign's two behaviour-variety elites have
/// authored PLACEMENT rules, and until now nothing enforced them — there was no
/// encounter-composition validator at all, and the Chrono-Warden had never been
/// placed in any level despite V7.1 design law ("first appearance Level 6, then
/// salted through Levels 7-15 alongside the Tech-Enforcer").
///
/// <para>Two rules live here:</para>
/// <list type="number">
///   <item>The Eraser is never authored into the same room as a Chrono-Warden
///     before Act III. A room whose Dilation Field pins the player while a
///     Siphon tether empties the meter that would have answered it is an Act III
///     composition, and meeting it in Act II reads as a difficulty spike rather
///     than an escalation.</item>
///   <item>The Chrono-Warden actually appears, from Level 6 onward.</item>
/// </list>
///
/// <para>"Room" is read as the authored wave/room group each level controller
/// exposes, because that is the unit a level actually spawns and the unit a
/// player experiences as one fight.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterCompositionTests {
    private const string EraserID = "unbound_eraser";
    private const string WardenID = "chrono_warden";

    /// <summary>The first level at which the two elites may share a room.</summary>
    private const int ActThreeFirstLevel = 13;

    /// <summary>One authored room: the level it belongs to and what spawns in it.</summary>
    private readonly record struct Room(int Level, string Name, IReadOnlyList<string> EnemyIDs);

    [TestCase]
    public void NoRoomPairsAnEraserWithAChronoWardenBeforeActThree() {
        var violations = new List<string>();
        foreach (Room room in AllAuthoredRooms()) {
            bool hasEraser = room.EnemyIDs.Contains(EraserID);
            bool hasWarden = room.EnemyIDs.Contains(WardenID);
            if (hasEraser && hasWarden && room.Level < ActThreeFirstLevel) {
                violations.Add($"Level {room.Level} room '{room.Name}' pairs an Eraser with a Chrono-Warden");
            }
        }
        if (violations.Count > 0) AssertThat(string.Join(" | ", violations)).IsEqual("");

        // Guard against a silently empty sweep passing vacuously: the rule is only
        // meaningful if both elites are actually placed somewhere.
        var everyEnemy = AllAuthoredRooms().SelectMany(room => room.EnemyIDs).ToList();
        AssertThat(everyEnemy.Contains(EraserID))
            .OverrideFailureMessage("No Eraser is placed anywhere; the pairing rule would pass vacuously.")
            .IsTrue();
        AssertThat(everyEnemy.Contains(WardenID)).IsTrue();
    }

    [TestCase]
    public void TheChronoWardenAppearsFromLevelSixOnwardAndTheEraserFromLevelSeven() {
        List<Room> rooms = AllAuthoredRooms().ToList();

        // V7.1 design law, finally executed: the Warden debuts at Level 6.
        List<int> wardenLevels = rooms.Where(room => room.EnemyIDs.Contains(WardenID))
            .Select(room => room.Level).Distinct().OrderBy(level => level).ToList();
        AssertThat(wardenLevels.Count)
            .OverrideFailureMessage("The Chrono-Warden must appear in at least one room from Level 6 onward.")
            .IsGreater(0);
        AssertThat(wardenLevels[0])
            .OverrideFailureMessage($"The Warden's authored debut is Level 6; the earliest placement is {wardenLevels[0]}.")
            .IsEqual(6);

        // The Eraser is salted through 7-15 and debuts before that at Level 4A,
        // which lives on its own route trigger rather than in a spawn table.
        List<int> eraserLevels = rooms.Where(room => room.EnemyIDs.Contains(EraserID))
            .Select(room => room.Level).Distinct().OrderBy(level => level).ToList();
        AssertThat(eraserLevels.Count)
            .OverrideFailureMessage("The Eraser must be salted through the late campaign.")
            .IsGreaterEqual(4);
        AssertThat(eraserLevels[0]).IsGreaterEqual(7);
        AssertThat(eraserLevels[^1]).IsEqual(15);

        // Act III opens with the pair, which is the beat the naming line sits on.
        AssertThat(Level13Controller.EraserCount)
            .OverrideFailureMessage("Level 13's scripted ambush is a PAIR of Erasers.")
            .IsEqual(2);

        // And the 4A debut trigger points at the real enemy, not the interim body.
        // Every one of the nine Legacy variants builds its trigger through
        // LegacyLevelControllerBase.BuildEraserDebut(), which never sets EnemyID -
        // so the DEFAULT is what each variant actually spawns, and flipping it is
        // what re-points all nine at once.
        AssertString(EraserDebutTrigger.EraserEnemyID).IsEqual(EraserID);
        var defaultTrigger = AutoFree(new EraserDebutTrigger())!;
        AssertString(defaultTrigger.EnemyID)
            .OverrideFailureMessage("Every Level 4A variant must get the real Eraser by default.")
            .IsEqual(EraserID);
        foreach (string hero in new[] {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
        }) {
            AssertThat(FileAccess.FileExists($"res://scenes/campaign/Level_04A_{hero}.tscn"))
                .OverrideFailureMessage($"Level 4A variant for '{hero}' is missing.")
                .IsTrue();
        }
    }

    // === The authored room tables ===

    /// <summary>
    /// Every authored room in the campaign that spawns enemies from a static
    /// table. Levels 0-5 are not enumerated: neither elite is placed before
    /// Level 6, and the Tutorial and Florence predate the spawn-table convention.
    /// </summary>
    private static IEnumerable<Room> AllAuthoredRooms() {
        // Level 6 groups its spawns by room array rather than by wave index.
        yield return new Room(6, "forum", IDsOf(Level06Controller.Room1Spawns));
        yield return new Room(6, "vault", IDsOf(Level06Controller.Room2Spawns));
        yield return new Room(6, "ash_road", IDsOf(Level06Controller.Room3Spawns));

        foreach (Room room in Waves(7, Level07Controller.SpawnTable)) yield return room;
        foreach (Room room in Waves(9, Level09Controller.SpawnTable)) yield return room;
        foreach (Room room in Waves(12, Level12Controller.SpawnTable)) yield return room;
        foreach (Room room in Waves(13, Level13Controller.SpawnTable)) yield return room;
        foreach (Room room in Waves(14, Level14Controller.SpawnTable)) yield return room;
        foreach (Room room in Waves(15, Level15Controller.SpawnTable)) yield return room;
    }

    private static IEnumerable<Room> Waves(int level, (string EnemyID, int Wave, Vector2 Position)[] table) {
        foreach (IGrouping<int, (string EnemyID, int Wave, Vector2 Position)> wave
                 in table.GroupBy(entry => entry.Wave)) {
            yield return new Room(level, $"wave_{wave.Key}", wave.Select(entry => entry.EnemyID).ToList());
        }
    }

    private static IReadOnlyList<string> IDsOf(
        (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] spawns) =>
        spawns.Select(spawn => spawn.EnemyID).ToList();
}
