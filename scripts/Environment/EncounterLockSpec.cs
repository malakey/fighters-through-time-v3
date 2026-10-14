using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>One authored member spawn of an encounter-lock wave.</summary>
    public sealed class EncounterLockSpawn {
        public string EnemyID = "";
        /// <summary>Where the member spawns, and the anchor the fall net and the stall watchdog return it to.</summary>
        public Vector2 Position;
        /// <summary>Optional patrol pair, used once an engaged member loses its target and returns to patrol.</summary>
        public Vector2? PatrolA;
        public Vector2? PatrolB;

        public EncounterLockSpawn() { }

        public EncounterLockSpawn(string enemyID, Vector2 position, Vector2? patrolA = null, Vector2? patrolB = null) {
            EnemyID = enemyID ?? "";
            Position = position;
            PatrolA = patrolA;
            PatrolB = patrolB;
        }
    }

    /// <summary>
    /// One wave of an encounter lock. Each wave is exactly one row of the
    /// level's reward manifest (<c>RequiredWaves</c>): the same <see cref="RowID"/>,
    /// the same enemy IDs in the same order, the same per-row difficulty prefix
    /// (<c>EncounterLockContentTests</c> pins the match), so the F05 ledger and the
    /// spawns can never drift.
    /// </summary>
    public sealed class EncounterLockWave {
        /// <summary>The manifest row this wave is, e.g. <c>level_02.room1</c> (without any <c>@n</c>).</summary>
        public string RowID = "";
        public EncounterLockSpawn[] Spawns = Array.Empty<EncounterLockSpawn>();
        public EncounterWaveTrigger Trigger = EncounterWaveTrigger.OnBuild;
        /// <summary>The threshold of <see cref="EncounterWaveTrigger.WhenAliveAtMost"/>.</summary>
        public int AliveAtMost;
        /// <summary>
        /// The per-row difficulty prefix applies (the manifest row carries no
        /// <c>@0</c>). False spawns the whole table on every difficulty.
        /// </summary>
        public bool Scaled = true;
        /// <summary>
        /// The count the prefix scales from: the manifest row's <c>@n</c>, or
        /// -1 for the table length (a table with Hard-only extras authors fewer).
        /// </summary>
        public int AuthoredCount = -1;

        /// <summary>
        /// A wave from one of the level controllers' authored spawn tables, the
        /// <c>(EnemyID, Position, PatrolA, PatrolB)</c> shape Levels 2–11 use.
        /// </summary>
        public static EncounterLockWave FromTable(
            string rowID,
            (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] table,
            EncounterWaveTrigger trigger = EncounterWaveTrigger.OnBuild) {
            var spawns = new List<EncounterLockSpawn>();
            foreach ((string enemyID, Vector2 position, Vector2? patrolA, Vector2? patrolB) in table ?? Array.Empty<(string, Vector2, Vector2?, Vector2?)>()) {
                spawns.Add(new EncounterLockSpawn(enemyID, position, patrolA, patrolB));
            }
            return new EncounterLockWave { RowID = rowID ?? "", Spawns = spawns.ToArray(), Trigger = trigger };
        }

        /// <summary>The enemy IDs of the table, in order. Content validation seam.</summary>
        public string[] EnemyIDs {
            get {
                var ids = new string[Spawns?.Length ?? 0];
                for (int index = 0; index < ids.Length; index++) ids[index] = Spawns[index]?.EnemyID ?? "";
                return ids;
            }
        }
    }

    /// <summary>
    /// The authoring description of one <see cref="EncounterLock"/> (plan §2.3).
    /// Built by a level in <c>BuildLevel</c> and handed to
    /// <c>StoryLevelControllerBase.BuildEncounterLock</c>, or to
    /// <see cref="EncounterLock.Create"/> by a level that does not extend the base
    /// (Florence, the Tutorial).
    /// </summary>
    public sealed class EncounterLockSpec {
        /// <summary>
        /// The stable lock ID. It doubles as an F10 encounter ID: every
        /// checkpoint after the lock lists it in its baseline, and a resume past
        /// it builds the lock Cleared. For a base level it starts with the level
        /// ID (<c>EncounterBaselineTests</c>), e.g. <c>level_02_orleans_lock_vanguard</c>.
        /// </summary>
        public string LockID = "";

        /// <summary>
        /// The area the hero must clear, global. Only its x-span confines the
        /// members; its y-span is the hero containment band (author it from
        /// <see cref="StoryGateRules.ColumnTopY"/> to the level bottom with
        /// <see cref="Span"/> unless the room is stacked).
        /// </summary>
        public Rect2 Area;

        /// <summary>
        /// The camera rect while the lock is Engaged; null derives it from the
        /// area, expanded to the visible view and clipped to <see cref="LevelBounds"/>.
        /// </summary>
        public Rect2? CameraBounds;

        /// <summary>The level's whole bounds, for the derived camera rect. The base builder fills it in.</summary>
        public Rect2? LevelBounds;

        /// <summary>Centre x of the barrier on the hero's way in; null for none (an area entered from a wall).</summary>
        public float? EntryBarrierX;
        /// <summary>Centre x of the barrier on the way out; null for none (a lock that only bars the way back).</summary>
        public float? ExitBarrierX;
        /// <summary>The entry barrier's top and bottom y; null runs from <see cref="StoryGateRules.ColumnTopY"/> to <see cref="BarrierFloorY"/>.</summary>
        public Vector2? EntryBarrierYRange;
        /// <summary>
        /// The exit barrier's top and bottom y. A lock whose exit sits at a gate
        /// with a gate column runs it from the column's foot to the floor, so it
        /// fills the gate without standing beside the column as a second wall.
        /// </summary>
        public Vector2? ExitBarrierYRange;
        /// <summary>The floor top under the barriers, their default bottom.</summary>
        public float BarrierFloorY;
        public float BarrierWidth = EncounterLockRules.DefaultBarrierWidthPixels;

        public EncounterLockWave[] Waves = Array.Empty<EncounterLockWave>();

        /// <summary>
        /// Puzzles that must be solved (live or persisted) before the lock clears.
        /// FIX-OBJECTIVE: one-way. Each latches the first time the lock sees it
        /// solved, and its manager then keeps the solve through rewinds (the rule is
        /// in <see cref="EncounterLock"/>'s conditions region). The level keeps the
        /// puzzle's props (gears, coils, relays) solved through rewinds from the
        /// solve on, so the world agrees with the lock.
        /// </summary>
        public string[] RequiredPuzzleIDs = Array.Empty<string>();

        /// <summary>
        /// Breakables (a generator, a rockfall) that must be destroyed before the
        /// lock clears. FIX-OBJECTIVE: one-way. Each latches the first time the lock
        /// sees it destroyed and then stays destroyed through rewinds.
        /// </summary>
        public DamageableEnvironmentObject[] RequiredBreakables = Array.Empty<DamageableEnvironmentObject>();

        /// <summary>Objective posted while the lock is Engaged; empty keeps the level's own.</summary>
        public string ObjectiveKey = "";

        /// <summary>Where the HUD's GO prompt points once the lock clears.</summary>
        public EncounterGoDirection GoDirection = EncounterGoDirection.Right;

        /// <summary>
        /// An area from <paramref name="minX"/> to <paramref name="maxX"/>, running
        /// from <see cref="StoryGateRules.ColumnTopY"/> (so no jump ever leaves it
        /// upward) down to <paramref name="bottomY"/> (normally the level bottom).
        /// </summary>
        public static Rect2 Span(float minX, float maxX, float bottomY) =>
            new(Mathf.Min(minX, maxX), StoryGateRules.ColumnTopY,
                Mathf.Abs(maxX - minX), bottomY - StoryGateRules.ColumnTopY);
    }
}
